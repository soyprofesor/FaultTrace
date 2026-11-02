using FaultTrace.Core.Localization;
using Shouldly;
using Xunit;

namespace FaultTrace.Core.Tests.Localization;

public class CultureNormalizerTests
{
    [Theory]
    [InlineData("uz", "uz-Latn")]
    [InlineData("uz-UZ", "uz-Latn")]
    [InlineData("uz_UZ", "uz-Latn")]
    [InlineData("UZ-LATN", "uz-Latn")]
    [InlineData("uz-Latn", "uz-Latn")]
    [InlineData("uz-Latn-UZ", "uz-Latn")]
    [InlineData("uz-Cyrl", "uz-Cyrl")]
    [InlineData("uz-Cyrl-UZ", "uz-Cyrl")]
    [InlineData("ru", "ru")]
    [InlineData("ru-RU", "ru")]
    [InlineData("RU_ru", "ru")]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    [InlineData("en-GB", "en")]
    public void Normalize_MapsToBuiltInCulture(string value, string expected)
    {
        CultureNormalizer.Normalize(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("oz")]
    [InlineData("1")]
    [InlineData("russian")]
    [InlineData("english")]
    [InlineData("uz-AF")]
    [InlineData("fr-FR")]
    [InlineData("<script>")]
    public void Normalize_RejectsEverythingElse(string? value)
    {
        CultureNormalizer.Normalize(value).ShouldBeNull();
    }

    [Fact]
    public void Normalize_RejectsVeryLongValues()
    {
        CultureNormalizer.Normalize("en-" + new string('x', 100)).ShouldBeNull();
    }
}
