# Localization

This covers the texts FaultTrace itself generates: `title`, and `detail` for technical errors. Text from `RAISE_APPLICATION_ERROR` and from your own exceptions is shown as written. Logs are always in English.

The built-in cultures are `en`, `uz-Latn`, `uz-Cyrl` and `ru`. A missing text falls back to the default culture, then to English, so a text is never empty.

FaultTrace never changes the thread culture.

## The default culture

```csharp
options.DefaultCulture = "uz-Latn";
```

## How the culture is found

If the project uses ASP.NET's `UseRequestLocalization`, FaultTrace reads the culture it chose. Otherwise, it tries these sources in order, and a value that isn't a supported culture moves on to the next one:

| Source | Default names | Setting |
|---|---|---|
| Query string | `culture`, `lang` | `Culture.QueryNames` |
| Header | `X-Culture`, `X-Language` | `Culture.HeaderNames` |
| Route value | `culture` | `Culture.RouteValueNames` |
| Cookie | `culture` | `Culture.CookieNames` |
| Claim | `locale` | `Culture.ClaimNames` |
| `Accept-Language` | Respects `q` weights | `Culture.UseAcceptLanguage` |
| Your own code | None | `Culture.Resolver` |

An empty list turns a source off.

```csharp
options.Culture.QueryNames = new List<string>();
options.Culture.HeaderNames = new List<string> { "X-Lang" };
```

To choose the mode yourself, set `Culture.Mode` to `RequestLocalization` or `Sources`. The default is `Auto`.

## Which values are accepted

Standard BCP 47 values, case-insensitively, with `_` accepted as `-`:

| Value | Culture |
|---|---|
| `uz`, `uz-UZ`, `uz-Latn`, `uz-Latn-UZ` | `uz-Latn` |
| `uz-Cyrl`, `uz-Cyrl-UZ` | `uz-Cyrl` |
| `ru`, `ru-RU` | `ru` |
| `en`, `en-US`, `en-GB` | `en` |

Anything else is ignored. If your clients send a non-standard value, add an alias:

```csharp
options.AddCultureAlias("oz", "uz-Latn");
options.AddCultureAlias("2", "ru");
```

## Your own texts

Change a built-in text, or add a language:

```csharp
options.SetMessage("uz-Latn", FaultCodes.NotFound, "Topilmadi", "Bunday buyurtma yo'q.");
options.SetMessage("kk", FaultCodes.NotFound, "Табылмады", "Деректер табылмады.");
```

A text you didn't set for a new language falls back to the default culture.

For a project's own `code`, add its text the same way:

```csharp
options.SetMessage("uz-Latn", "order_closed", "Buyurtma yopilgan", "Bu buyurtmani o'zgartirib bo'lmaydi.");
```

## Texts from your own store

To read texts from resource files or a database, use `UseMessages`. It gets the code and the culture, and returns the texts or `null` to use the built-in ones:

```csharp
var resources = new ResourceManager(typeof(ErrorTexts));

options.UseMessages((code, culture) =>
{
    var info = CultureInfo.GetCultureInfo(culture);
    var title = resources.GetString(code + ".Title", info);
    var detail = resources.GetString(code + ".Detail", info);

    return title != null && detail != null ? new FaultMessage(title, detail) : null;
});
```

The culture is a parameter on purpose. `IStringLocalizer` reads the thread culture, which FaultTrace doesn't change, so it could return the wrong language.

- `culture` is always a supported culture, never the client's raw value, so `GetCultureInfo` is safe here.
- It runs on the error path, so keep it fast and cache what comes from a database.
- A failure is logged, and the built-in text is used.
- For a culture that isn't built in, add it: `options.AddCulture("kk")`.
