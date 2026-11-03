using System;
using System.Collections.Generic;
using System.Globalization;
using FaultTrace.Core.Abstractions;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Model;
using FaultTrace.Oracle.Classification;
using FaultTrace.Oracle.Detection;
using FaultTrace.Oracle.Options;
using FaultTrace.Oracle.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FaultTrace.Oracle;

internal sealed class OracleFaultTranslator : IFaultProvider
{
    private const string ProviderName = "Oracle";

    private readonly IFaultMessageProvider _messages;
    private readonly OracleFaultOptions _options;
    private readonly ILogger _logger;

    public OracleFaultTranslator(IFaultMessageProvider messages, IOptions<OracleFaultOptions> options, ILoggerFactory? loggerFactory = null)
    {
        _messages = messages;
        _options = options.Value;
        _logger = loggerFactory?.CreateLogger("FaultTrace") ?? NullLogger.Instance;
    }

    public Fault? TryTranslate(Exception exception, FaultContext context)
    {
        var error = OracleExceptionReader.Find(exception);
        if (error == null)
        {
            return null;
        }

        var parsed = OracleErrorParser.Parse(error.Message, _options.IgnoredPackages);
        if (parsed.ParseFailed)
        {
            _logger.LogWarning("FaultTrace could not parse an Oracle message, so fault {ErrorId} has no stack details", context.ErrorId);
        }

        var number = error.Number != 0 && error.Number.HasValue ? error.Number : parsed.Number;
        var nativeCode = number.HasValue
            ? GetNativeCode(number.Value)
            : parsed.NativeCode;

        var culture = context.Culture ?? _messages.ResolveCulture(null);
        var rule = FindRule(number, parsed, error.Message);
        var classification = rule?.Classification ?? Classify(number);

        return new Fault(
            context.ErrorId,
            classification.Category,
            classification.Code,
            GetUserMessage(classification, parsed, rule, culture),
            classification.Status,
            classification.IsTransient,
            context.TraceId,
            provider: new ProviderFaultDetails(
                ProviderName,
                nativeCode,
                parsed.Origin,
                parsed.Entry,
                parsed.Chain,
                parsed.Details,
                error.Message,
                !classification.IsMapped));
    }

    // A negative number comes from the driver and has no ORA code.
    private static string GetNativeCode(int number) =>
        (number < 0 ? "ODP" : "ORA-") + number.ToString("D5", CultureInfo.InvariantCulture);

    private OracleClassification Classify(int? number)
    {
        return number.HasValue && _options.CodeOverrides.TryGetValue(number.Value, out var overridden)
            ? overridden
            : OracleErrorClassifier.Classify(number);
    }

    private OracleFaultRule? FindRule(int? number, ParsedOracleError parsed, string rawMessage)
    {
        if (_options.Rules.Count == 0)
        {
            return null;
        }

        var info = new OracleErrorInfo(number, parsed.Origin?.ToString(), parsed.Entry?.ToString(), parsed.Details, rawMessage);
        foreach (var rule in _options.Rules)
        {
            try
            {
                if (rule.Predicate(info))
                {
                    return rule;
                }
            }
            catch (Exception failure)
            {
                // A broken project rule must not change how the error is handled: it counts as no match.
                _logger.LogError(failure, "FaultTrace Oracle rule failed for error {Number}", number);
            }
        }

        return null;
    }

    private string GetUserMessage(OracleClassification classification, ParsedOracleError parsed, OracleFaultRule? rule, string culture)
    {
        if (rule?.Messages != null && TryGetForCulture(rule.Messages, culture, out var ruleMessage))
        {
            return ruleMessage;
        }

        var code = classification.Code;
        if (code == FaultCodes.Business && parsed.BusinessMessage != null)
        {
            return parsed.BusinessMessage;
        }

        var message = _messages.Get(code, culture);

        if ((code == FaultCodes.RequiredField || code == FaultCodes.ValueTooLong)
            && message.FieldDetail != null
            && parsed.Details.TryGetValue(OracleDetailKeys.Column, out var column)
            && _options.Columns.TryGetValue(column, out var names)
            && TryGetForCulture(names, culture, out var name))
        {
            return message.FieldDetail.Replace("{0}", name);
        }

        if ((code == FaultCodes.Duplicate || code == FaultCodes.ReferenceNotFound || code == FaultCodes.HasDependents)
            && parsed.Details.TryGetValue(OracleDetailKeys.Constraint, out var constraint)
            && _options.Constraints.TryGetValue(constraint, out var constraintMessages)
            && TryGetForCulture(constraintMessages, culture, out var constraintMessage))
        {
            return constraintMessage;
        }

        return message.Detail;
    }

    // The same fallback as library-generated texts: requested culture, default culture, English.
    private bool TryGetForCulture(Dictionary<string, string> values, string culture, out string value)
    {
        return values.TryGetValue(culture, out value!)
            || values.TryGetValue(_messages.ResolveCulture(null), out value!)
            || values.TryGetValue(CultureNormalizer.English, out value!);
    }
}
