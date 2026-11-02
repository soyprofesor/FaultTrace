using System;

namespace FaultTrace.Core.Localization;

/// <summary>
/// Maps BCP 47 culture values to the built-in cultures. Works from a whitelist and never constructs a
/// <see cref="System.Globalization.CultureInfo"/> from its input.
/// </summary>
public static class CultureNormalizer
{
    /// <summary>The English culture.</summary>
    public const string English = "en";

    /// <summary>The Uzbek culture in Latin script.</summary>
    public const string UzbekLatin = "uz-Latn";

    /// <summary>The Uzbek culture in Cyrillic script.</summary>
    public const string UzbekCyrillic = "uz-Cyrl";

    /// <summary>The Russian culture.</summary>
    public const string Russian = "ru";

    private const int MaxLength = 35;

    /// <summary>
    /// Maps a culture value to a built-in culture. Case-insensitive, and <c>_</c> is accepted as <c>-</c>.
    /// </summary>
    /// <param name="value">The value, for example <c>uz-UZ</c> or <c>ru_RU</c>.</param>
    /// <returns>The built-in culture, or <see langword="null"/> if the value doesn't match one.</returns>
    public static string? Normalize(string? value)
    {
        if (value == null || value.Length == 0 || value.Length > MaxLength)
        {
            return null;
        }

        var culture = value.Replace('_', '-').ToLowerInvariant();

        if (culture == "uz" || culture == "uz-uz" || IsOrStartsWith(culture, "uz-latn"))
        {
            return UzbekLatin;
        }

        if (IsOrStartsWith(culture, "uz-cyrl"))
        {
            return UzbekCyrillic;
        }

        if (IsOrStartsWith(culture, "ru"))
        {
            return Russian;
        }

        return IsOrStartsWith(culture, "en") ? English : null;
    }

    private static bool IsOrStartsWith(string culture, string prefix) =>
        culture == prefix || culture.StartsWith(prefix + "-", StringComparison.Ordinal);
}
