using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using FaultTrace.Core.Model;
using FaultTrace.Oracle.Classification;

namespace FaultTrace.Oracle.Parsing;

// Everything here is language-independent: message text depends on NLS_LANGUAGE, so only codes,
// quotes and numbers are matched.
internal static class OracleErrorParser
{
    private const string FrameCode = "ORA-06512:";
    private const string BacktraceLabel = "BACKTRACE";
    private const RegexOptions Options = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private static readonly Regex SegmentPattern = new Regex(@"^ORA-(\d{5}):\s*(.*)$", Options, MatchTimeout);

    // Matches: ORA-06512: at "APP.ORDER_PACK", line 775 and ORA-06512: at line 1
    private static readonly Regex FramePattern = new Regex(@"^ORA-06512:[^""]*?(?:""([^""]+)""\D*)?(\d+)\s*$", Options, MatchTimeout);

    private static readonly Regex HelpUrlPattern = new Regex(@"(?:Help:\s*)?https://docs\.oracle\.com/error-help/\S*", Options, MatchTimeout);

    private static readonly Regex ColumnPattern = new Regex(@"""([^""]+)""\.""([^""]+)""\.""([^""]+)""", Options, MatchTimeout);

    private static readonly Regex ConstraintPattern = new Regex(@"\(([\w$#]+)\.([\w$#]+)\)", Options, MatchTimeout);

    private static readonly Regex OracleCodePattern = new Regex(@"ORA-\d{5}:?\s*", Options, MatchTimeout);

    /// <summary>Parses a raw Oracle message. Never throws.</summary>
    public static ParsedOracleError Parse(string? message, IEnumerable<string>? ignoredPackages = null)
    {
        if (message == null)
        {
            return ParsedOracleError.Empty;
        }

        try
        {
            return ParseCore(message, ignoredPackages?.ToArray() ?? Array.Empty<string>());
        }
        catch (Exception)
        {
            return ParsedOracleError.Failed;
        }
    }

    private static ParsedOracleError ParseCore(string message, string[] ignoredPackages)
    {
        var segments = ReadSegments(message);
        if (segments.Count == 0)
        {
            return ParsedOracleError.Empty;
        }

        var first = segments[0];
        var businessMessage = IsBusiness(first) ? CleanBusinessMessage(first.Text) : null;
        var outerIndex = FindOuterSegment(segments);

        return new ParsedOracleError(
            first.Number,
            first.NativeCode,
            businessMessage,
            FindOrigin(segments, outerIndex, ignoredPackages),
            FindEntry(segments, outerIndex, ignoredPackages),
            segments.Select(segment => segment.ToFaultSegment(ignoredPackages)).ToArray(),
            ReadDetails(segments));
    }

    private static List<SegmentBuilder> ReadSegments(string message)
    {
        var segments = new List<SegmentBuilder>();
        SegmentBuilder? current = null;

        // A backtrace appended without a line break still has to become frames, not user-visible text.
        var lines = message
            .Replace(FrameCode, "\n" + FrameCode)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

        foreach (var rawLine in lines)
        {
            var line = HelpUrlPattern.Replace(rawLine, string.Empty).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith(FrameCode, StringComparison.Ordinal))
            {
                if (current == null)
                {
                    current = new SegmentBuilder(null, FrameCode.TrimEnd(':'), string.Empty);
                    segments.Add(current);
                }

                var frame = FramePattern.Match(line);
                if (frame.Success && int.TryParse(frame.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var frameLine))
                {
                    current.Frames.Add(new FaultFrame(frame.Groups[1].Success ? frame.Groups[1].Value : null, frameLine));
                }

                continue;
            }

            var segment = SegmentPattern.Match(line);
            if (segment.Success)
            {
                var code = segment.Groups[1].Value;
                current = new SegmentBuilder(
                    int.Parse(code, NumberStyles.None, CultureInfo.InvariantCulture),
                    "ORA-" + code,
                    segment.Groups[2].Value);
                segments.Add(current);
                continue;
            }

            if (current == null)
            {
                continue;
            }

            if (line.IndexOf(BacktraceLabel, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                current.BacktraceStart = current.Frames.Count;
                continue;
            }

            current.Text = current.Text.Length == 0 ? line : current.Text + "\n" + line;
        }

        return segments;
    }

    // The first code alone decides that this is a business error. Codes inside the text are only cut out.
    private static string? CleanBusinessMessage(string text)
    {
        var cleaned = OracleCodePattern.Replace(text, string.Empty).Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    // The outermost call from .NET is the segment that ends with an anonymous block ("at line N").
    private static int FindOuterSegment(List<SegmentBuilder> segments)
    {
        for (var i = segments.Count - 1; i >= 0; i--)
        {
            var frames = segments[i].Frames;
            if (frames.Count > 0 && frames[frames.Count - 1].Unit == null)
            {
                return i;
            }
        }

        return segments.Count - 1;
    }

    private static FaultFrame? FindOrigin(List<SegmentBuilder> segments, int outerIndex, string[] ignoredPackages)
    {
        foreach (var segment in segments)
        {
            var backtrace = segment.BacktraceFrames().FirstOrDefault(frame => IsVisible(frame, ignoredPackages));
            if (backtrace != null)
            {
                return backtrace;
            }
        }

        var originSegment = IsNewestFirst(segments, outerIndex) ? segments[segments.Count - 1] : segments[0];

        return originSegment.Frames.FirstOrDefault(frame => IsVisible(frame, ignoredPackages))
            ?? segments.SelectMany(segment => segment.Frames).FirstOrDefault(frame => IsVisible(frame, ignoredPackages))
            ?? segments.SelectMany(segment => segment.Frames).FirstOrDefault();
    }

    private static FaultFrame? FindEntry(List<SegmentBuilder> segments, int outerIndex, string[] ignoredPackages)
    {
        return segments[outerIndex].Frames.LastOrDefault(frame => IsVisible(frame, ignoredPackages))
            ?? segments.SelectMany(segment => segment.Frames).LastOrDefault(frame => IsVisible(frame, ignoredPackages));
    }

    // RAISE_APPLICATION_ERROR(..., TRUE) puts the new error on top of the original. Either the top segment
    // ends with the anonymous block, or a business error sits above a technical one.
    private static bool IsNewestFirst(List<SegmentBuilder> segments, int outerIndex)
    {
        if (segments.Count < 2)
        {
            return false;
        }

        return outerIndex == 0 || (IsBusiness(segments[0]) && !IsBusiness(segments[segments.Count - 1]));
    }

    private static bool IsBusiness(SegmentBuilder segment) =>
        segment.Number.HasValue && OracleErrorClassifier.IsBusiness(segment.Number.Value);

    private static bool IsVisible(FaultFrame frame, string[] ignoredPackages) =>
        frame.Unit != null && !IsIgnored(frame, ignoredPackages);

    private static bool IsIgnored(FaultFrame frame, string[] ignoredPackages)
    {
        if (frame.Unit == null)
        {
            return false;
        }

        // "ERROR_PACK" and "CORE.ERROR_PACK" both match the unit "CORE.ERROR_PACK".
        var unit = "." + frame.Unit + ".";
        return ignoredPackages.Any(package => unit.IndexOf("." + package + ".", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static IReadOnlyDictionary<string, string> ReadDetails(List<SegmentBuilder> segments)
    {
        var details = new Dictionary<string, string>();

        foreach (var segment in segments.Where(segment => !IsBusiness(segment)))
        {
            var column = ColumnPattern.Match(segment.Text);
            if (column.Success)
            {
                details[OracleDetailKeys.Schema] = column.Groups[1].Value;
                details[OracleDetailKeys.Table] = column.Groups[2].Value;
                details[OracleDetailKeys.Column] = column.Groups[3].Value;
                break;
            }

            var constraint = ConstraintPattern.Match(segment.Text);
            if (constraint.Success)
            {
                details[OracleDetailKeys.Schema] = constraint.Groups[1].Value;
                details[OracleDetailKeys.Constraint] = constraint.Groups[2].Value;
                break;
            }
        }

        return details;
    }

    private sealed class SegmentBuilder
    {
        public SegmentBuilder(int? number, string nativeCode, string text)
        {
            Number = number;
            NativeCode = nativeCode;
            Text = text;
        }

        public int? Number { get; }

        public string NativeCode { get; }

        public string Text { get; set; }

        public List<FaultFrame> Frames { get; } = new List<FaultFrame>();

        public int? BacktraceStart { get; set; }

        // A labelled section, or a first stack that already reached the anonymous block before more frames follow.
        public IEnumerable<FaultFrame> BacktraceFrames()
        {
            if (BacktraceStart.HasValue)
            {
                return Frames.Skip(BacktraceStart.Value);
            }

            var firstAnonymous = Frames.FindIndex(frame => frame.Unit == null);
            return firstAnonymous >= 0 && firstAnonymous < Frames.Count - 1
                ? Frames.Take(firstAnonymous)
                : Enumerable.Empty<FaultFrame>();
        }

        public FaultSegment ToFaultSegment(string[] ignoredPackages) =>
            new FaultSegment(NativeCode, Text, Frames.Where(frame => !IsIgnored(frame, ignoredPackages)));
    }
}
