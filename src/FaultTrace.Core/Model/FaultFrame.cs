using System.Globalization;

namespace FaultTrace.Core.Model;

/// <summary>
/// One location in a provider's call stack, for example a PL/SQL package line.
/// </summary>
public sealed class FaultFrame
{
    /// <summary>
    /// Creates a frame.
    /// </summary>
    /// <param name="unit">The named unit, or <see langword="null"/> for an anonymous block.</param>
    /// <param name="line">The line number inside the unit.</param>
    public FaultFrame(string? unit, int line)
    {
        Unit = unit;
        Line = line;
    }

    /// <summary>The named unit, or <see langword="null"/> for an anonymous block.</summary>
    public string? Unit { get; }

    /// <summary>The line number inside the unit.</summary>
    public int Line { get; }

    /// <summary>Returns the frame as <c>UNIT:LINE</c>, with <c>anonymous</c> for an unnamed unit.</summary>
    /// <returns>The text form of the frame.</returns>
    public override string ToString() =>
        (Unit ?? "anonymous") + ":" + Line.ToString(CultureInfo.InvariantCulture);
}
