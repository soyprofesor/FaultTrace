using System;
using System.Linq;
using System.Reflection;
using FaultTrace.Core.Localization;
using FaultTrace.Core.Model;
using FaultTrace.Core.Options;
using Shouldly;
using Xunit;

namespace FaultTrace.Core.Tests.Localization;

public class DefaultFaultMessageProviderTests
{
    private static readonly string[] Cultures = { "en", "uz-Latn", "uz-Cyrl", "ru" };

    [Fact]
    public void Catalog_HasEveryCodeInEveryCulture()
    {
        var provider = Create();
        var codes = typeof(FaultCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetValue(null)!)
            .ToArray();

        codes.Length.ShouldBe(17);
        var catalog = FaultMessageCatalog.Create();

        foreach (var culture in Cultures)
        {
            catalog[culture].Keys.ShouldBe(codes, ignoreOrder: true);
            foreach (var code in codes)
            {
                var message = provider.Get(code, culture);
                message.Title.ShouldNotBeNullOrWhiteSpace();
                message.Detail.ShouldNotBeNullOrWhiteSpace();
            }
        }
    }

    [Theory]
    [InlineData("en", "A required field is missing: {0}")]
    [InlineData("uz-Latn", "Majburiy maydon to'ldirilmagan: {0}")]
    [InlineData("uz-Cyrl", "Мажбурий майдон тўлдирилмаган: {0}")]
    [InlineData("ru", "Не заполнено обязательное поле: {0}")]
    public void Get_ReturnsTextOfTheCulture(string culture, string expected)
    {
        Create().Get(FaultCodes.RequiredField, culture).FieldDetail.ShouldBe(expected);
    }

    [Fact]
    public void Catalog_TextsHaveNoDatabaseNames()
    {
        foreach (var culture in FaultMessageCatalog.Create().Values)
        {
            foreach (var message in culture.Values)
            {
                message.Detail.ShouldNotContain("ORA-");
                message.Title.ShouldNotContain("ORA-");
            }
        }
    }

    [Theory]
    [InlineData("uz-UZ", "uz-Latn")]
    [InlineData("ru-RU", "ru")]
    [InlineData("fr", "en")]
    [InlineData(null, "en")]
    public void ResolveCulture_FallsBackToDefault(string? requested, string expected)
    {
        Create().ResolveCulture(requested).ShouldBe(expected);
    }

    [Fact]
    public void ResolveCulture_UsesConfiguredDefault()
    {
        var provider = Create(options => options.DefaultCulture = "uz");

        provider.ResolveCulture(null).ShouldBe("uz-Latn");
        provider.ResolveCulture("fr").ShouldBe("uz-Latn");
        provider.ResolveCulture("ru").ShouldBe("ru");
    }

    [Fact]
    public void ResolveCulture_UnsupportedDefaultBecomesEnglish()
    {
        Create(options => options.DefaultCulture = "fr").ResolveCulture(null).ShouldBe("en");
    }

    [Fact]
    public void ResolveCulture_AppliesAliases()
    {
        var provider = Create(options => options.AddCultureAlias("oz", "uz-Latn").AddCultureAlias("2", "ru"));

        provider.ResolveCulture("oz").ShouldBe("uz-Latn");
        provider.ResolveCulture("OZ").ShouldBe("uz-Latn");
        provider.ResolveCulture("2").ShouldBe("ru");
    }

    [Fact]
    public void SetMessage_OverridesBuiltInText()
    {
        var provider = Create(options => options.SetMessage("uz", FaultCodes.Duplicate, "Title", "Detail"));

        provider.Get(FaultCodes.Duplicate, "uz-Latn").Detail.ShouldBe("Detail");
        provider.Get(FaultCodes.Duplicate, "ru").Detail.ShouldBe("Такая запись уже существует.");
    }

    [Fact]
    public void SetMessage_AddsNewCultureWithFallbackForMissingCodes()
    {
        var provider = Create(options => options.SetMessage("kk", FaultCodes.Duplicate, "Title", "Detail"));

        provider.ResolveCulture("kk").ShouldBe("kk");
        provider.Get(FaultCodes.Duplicate, "kk").Detail.ShouldBe("Detail");
        provider.Get(FaultCodes.Timeout, "kk").Detail.ShouldBe("The operation took too long. Please try again.");
    }

    [Fact]
    public void Get_MissingCodeFallsBackThroughDefaultCultureToEnglish()
    {
        var provider = Create(options =>
        {
            options.DefaultCulture = "uz-Latn";
            options.SetMessage("kk", FaultCodes.Duplicate, "Title", "Detail");
        });

        provider.Get(FaultCodes.Timeout, "kk").Detail.ShouldBe("Amal juda uzoq davom etdi. Qayta urinib ko'ring.");
    }

    [Fact]
    public void Get_UnknownCodeGivesUnexpectedText()
    {
        Create().Get("project_specific", "ru").Title.ShouldBe("Непредвиденная ошибка");
    }

    private static DefaultFaultMessageProvider Create(Action<FaultTraceOptions>? configure = null)
    {
        var options = new FaultTraceOptions();
        configure?.Invoke(options);
        return new DefaultFaultMessageProvider(Microsoft.Extensions.Options.Options.Create(options));
    }
}
