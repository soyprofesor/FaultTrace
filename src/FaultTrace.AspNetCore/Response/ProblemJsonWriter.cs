using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using FaultTrace.Core.Model;

namespace FaultTrace.AspNetCore.Response;

// Writes the JSON by hand, so the field order and the "nulls omitted" rule are the same on every target framework
// and nothing depends on the consumer's serializer settings.
internal static class ProblemJsonWriter
{
    // The body is never embedded in HTML, and the response has its own content type, so text stays readable:
    // Uzbek apostrophes and Cyrillic aren't escaped.
    private static readonly JsonWriterOptions WriterOptions = new JsonWriterOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions ValueOptions = new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
#if NETCOREAPP3_1
        IgnoreNullValues = true,
#else
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
#endif
    };

    private static readonly HashSet<string> ReservedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "type", "title", "status", "detail", "instance", "code", "errorId", "traceId", "timestamp", "errors", "debug",
    };

    public static byte[] Write(Problem problem, IReadOnlyDictionary<string, object?> extensions, Action<string, Exception> onFieldFailure)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            var fault = problem.Fault;

            writer.WriteStartObject();
            writer.WriteString("type", problem.Type);
            writer.WriteString("title", problem.Title);
            writer.WriteNumber("status", fault.Status);
            writer.WriteString("detail", fault.UserMessage);
            writer.WriteString("instance", problem.Instance);
            writer.WriteString("code", fault.Code);
            writer.WriteString("errorId", fault.ErrorId);
            WriteIfPresent(writer, "traceId", problem.TraceId);
            writer.WriteString("timestamp", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));

            if (fault.Errors != null)
            {
                WriteErrors(writer, fault.Errors);
            }

            foreach (var extension in extensions)
            {
                WriteExtension(writer, extension.Key, extension.Value, onFieldFailure);
            }

            if (problem.ShowDebug)
            {
                WriteDebug(writer, fault.Provider, problem.Exception);
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void WriteErrors(Utf8JsonWriter writer, IReadOnlyDictionary<string, IReadOnlyList<string>> errors)
    {
        writer.WriteStartObject("errors");
        foreach (var field in errors)
        {
            WriteStrings(writer, field.Key, field.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteExtension(Utf8JsonWriter writer, string key, object? value, Action<string, Exception> onFailure)
    {
        if (value == null || ReservedFields.Contains(key))
        {
            return;
        }

        // Serialize to a separate document first: a value that fails halfway must not corrupt the response.
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), ValueOptions));
        }
        catch (Exception failure)
        {
            onFailure(key, failure);
            return;
        }

        using (document)
        {
            writer.WritePropertyName(key);
            document.RootElement.WriteTo(writer);
        }
    }

    private static void WriteDebug(Utf8JsonWriter writer, ProviderFaultDetails? provider, Exception? exception)
    {
        writer.WriteStartObject("debug");
        WriteIfPresent(writer, "provider", provider?.Provider);
        WriteIfPresent(writer, "nativeCode", provider?.NativeCode);
        WriteIfPresent(writer, "message", provider?.RawMessage ?? exception?.Message);
        WriteIfPresent(writer, "origin", provider?.Origin?.ToString());
        WriteIfPresent(writer, "entry", provider?.Entry?.ToString());

        if (provider != null && provider.Chain.Count > 0)
        {
            WriteChain(writer, provider.Chain);
        }

        if (provider != null && provider.Details.Count > 0)
        {
            writer.WriteStartObject("details");
            foreach (var detail in provider.Details)
            {
                writer.WriteString(detail.Key, detail.Value);
            }

            writer.WriteEndObject();
        }

        // A fault returned from a local catch has no exception here.
        if (exception != null)
        {
            var lines = exception.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim());
            WriteStrings(writer, "dotnet", lines);
        }

        writer.WriteEndObject();
    }

    private static void WriteChain(Utf8JsonWriter writer, IReadOnlyList<FaultSegment> chain)
    {
        writer.WriteStartArray("chain");
        foreach (var segment in chain)
        {
            writer.WriteStartObject();
            writer.WriteString("nativeCode", segment.NativeCode);
            WriteIfPresent(writer, "message", segment.Message);
            WriteStrings(writer, "frames", segment.Frames.Select(frame => frame.ToString()));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteStrings(Utf8JsonWriter writer, string name, IEnumerable<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static void WriteIfPresent(Utf8JsonWriter writer, string name, string? value)
    {
        if (value != null)
        {
            writer.WriteString(name, value);
        }
    }

    internal sealed class Problem
    {
        public Problem(string type, string title, Fault fault, string instance, string? traceId, Exception? exception, bool showDebug)
        {
            Type = type;
            Title = title;
            Fault = fault;
            Instance = instance;
            TraceId = traceId;
            Exception = exception;
            ShowDebug = showDebug;
        }

        public string Type { get; }

        public string Title { get; }

        public Fault Fault { get; }

        public string Instance { get; }

        public string? TraceId { get; }

        public Exception? Exception { get; }

        public bool ShowDebug { get; }
    }
}
