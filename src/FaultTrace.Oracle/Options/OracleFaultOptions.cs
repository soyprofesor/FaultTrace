using System;
using System.Collections.Generic;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Model;
using FaultTrace.Oracle.Classification;

namespace FaultTrace.Oracle.Options;

/// <summary>
/// Settings of the Oracle provider.
/// </summary>
public sealed class OracleFaultOptions
{
    /// <summary>
    /// Packages hidden from the origin and the chain, for example a project's error helper. The raw message stays complete.
    /// A name can be <c>ERROR_PACK</c> or <c>CORE.ERROR_PACK</c>.
    /// </summary>
    public ICollection<string> IgnoredPackages { get; } = [];

    internal Dictionary<string, Dictionary<string, string>> Columns { get; } =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    internal Dictionary<string, Dictionary<string, string>> Constraints { get; } =
        new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    internal Dictionary<int, OracleClassification> CodeOverrides { get; } = [];

    internal List<OracleFaultRule> Rules { get; } = [];

    /// <summary>
    /// Sets the friendly name of a column, used by the <c>required_field</c> and <c>value_too_long</c> messages.
    /// </summary>
    /// <param name="column">The column name.</param>
    /// <param name="names">Culture to friendly name.</param>
    /// <returns>These options, for chaining.</returns>
    public OracleFaultOptions MapColumn(string column, IReadOnlyDictionary<string, string> names)
    {
        Columns[column] = NormalizeCultures(names);
        return this;
    }

    /// <summary>
    /// Sets the message of a constraint, used by the <c>duplicate</c>, <c>reference_not_found</c> and <c>has_dependents</c> faults.
    /// </summary>
    /// <param name="constraint">The constraint name.</param>
    /// <param name="messages">Culture to message.</param>
    /// <returns>These options, for chaining.</returns>
    public OracleFaultOptions MapConstraint(string constraint, IReadOnlyDictionary<string, string> messages)
    {
        Constraints[constraint] = NormalizeCultures(messages);
        return this;
    }

    /// <summary>
    /// Changes how an Oracle error number is classified.
    /// </summary>
    /// <param name="number">The error number, for example <c>1403</c>.</param>
    /// <param name="category">The fault category.</param>
    /// <param name="code">The fault code. See <see cref="FaultCodes"/>.</param>
    /// <param name="status">The HTTP status.</param>
    /// <param name="isTransient">Whether retrying makes sense.</param>
    /// <returns>These options, for chaining.</returns>
    public OracleFaultOptions OverrideCode(int number, FaultCategory category, string code, int status, bool isTransient = false)
    {
        CodeOverrides[number] = new OracleClassification(category, code, status, isTransient, isMapped: true);
        return this;
    }

    /// <summary>
    /// Adds a rule that decides the fault when its predicate matches. Rules run in order, before any other classification.
    /// </summary>
    /// <param name="predicate">Decides whether the rule applies. A predicate that throws counts as no match.</param>
    /// <param name="category">The fault category.</param>
    /// <param name="code">The fault code. See <see cref="FaultCodes"/>.</param>
    /// <param name="status">The HTTP status.</param>
    /// <param name="isTransient">Whether retrying makes sense.</param>
    /// <param name="messages">Culture to message. Without one, the generic text of <paramref name="code"/> is used.</param>
    /// <returns>These options, for chaining.</returns>
    public OracleFaultOptions AddRule(
        Func<OracleErrorInfo, bool> predicate,
        FaultCategory category,
        string code,
        int status,
        bool isTransient = false,
        IReadOnlyDictionary<string, string>? messages = null)
    {
        Rules.Add(new OracleFaultRule(
            predicate,
            new OracleClassification(category, code, status, isTransient, isMapped: true),
            messages == null ? null : NormalizeCultures(messages)));
        return this;
    }

    private static Dictionary<string, string> NormalizeCultures(IReadOnlyDictionary<string, string> values)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            result[CultureNormalizer.Normalize(value.Key) ?? value.Key] = value.Value;
        }

        return result;
    }
}
