namespace FaultTrace.Core.Localization;

/// <summary>
/// Supplies library-generated texts. Business messages from providers don't go through here.
/// </summary>
public interface IFaultMessageProvider
{
    /// <summary>
    /// Resolves a requested culture to a supported one.
    /// </summary>
    /// <param name="culture">The requested culture, or <see langword="null"/> for the default culture.</param>
    /// <returns>A supported culture. An unsupported request gives the default culture.</returns>
    string ResolveCulture(string? culture);

    /// <summary>
    /// Finds the supported culture a value means, without falling back to the default culture.
    /// </summary>
    /// <param name="culture">The value, for example <c>uz-UZ</c> or a project's alias.</param>
    /// <returns>A supported culture, or <see langword="null"/> if the value doesn't match one.</returns>
    string? FindCulture(string? culture);

    /// <summary>
    /// Gets the texts for a fault code. Falls back to the default culture, then to English, and never returns empty text.
    /// </summary>
    /// <param name="code">The fault code. See <see cref="Model.FaultCodes"/>.</param>
    /// <param name="culture">A culture returned by <see cref="ResolveCulture"/>.</param>
    /// <returns>The texts.</returns>
    FaultMessage Get(string code, string culture);
}
