using System;
using System.Linq;
using FaultTrace.Core.Model;
using Microsoft.Extensions.Logging;

namespace FaultTrace.Core.Logging;

// Log text is always English, whatever culture the user gets.
internal static class FaultLogger
{
    private const string Template =
        "Fault {ErrorId}: {Code} ({Status}) from {Origin}. " +
        "Category={Category} TraceId={TraceId} NativeCode={NativeCode} Entry={Entry} Unmapped={Unmapped} " +
        "Culture={Culture} Chain={Chain} Details={Details} RawMessage={RawMessage}";

    public static void Log(ILogger logger, Fault fault, Exception exception, string? culture)
    {
        var provider = fault.Provider;

        logger.Log(
            GetLevel(fault.Category),
            exception,
            Template,
            fault.ErrorId,
            fault.Code,
            fault.Status,
            provider?.Origin?.ToString(),
            fault.Category,
            fault.TraceId,
            provider?.NativeCode,
            provider?.Entry?.ToString(),
            provider?.IsUnmapped ?? false,
            culture,
            provider?.Chain.Select(FormatSegment).ToArray(),
            provider?.Details,
            provider?.RawMessage);
    }

    internal static LogLevel GetLevel(FaultCategory category)
    {
        switch (category)
        {
            case FaultCategory.Internal:
                return LogLevel.Error;
            case FaultCategory.Unauthorized:
            case FaultCategory.Forbidden:
            case FaultCategory.NotFound:
            case FaultCategory.ClientClosed:
                return LogLevel.Information;
            default:
                return LogLevel.Warning;
        }
    }

    private static string FormatSegment(FaultSegment segment) =>
        segment.NativeCode + " [" + string.Join(" > ", segment.Frames) + "]";
}
