# Getting started

## Add it to a web application

```bash
dotnet add package FaultTrace.AspNetCore --prerelease
dotnet add package FaultTrace.Oracle --prerelease
```

`FaultTrace.Oracle` is only needed if the project uses Oracle. In a worker without ASP.NET Core, [use `FaultTrace.Core` instead](workers.md).

Register the services and put `UseFaultTrace()` first in the pipeline. Anything before it is out of its reach.

```csharp
using FaultTrace.AspNetCore.DependencyInjection;
using FaultTrace.Oracle.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddFaultTrace().AddOracle();

var app = builder.Build();

app.UseFaultTrace();
app.UseRouting();
app.MapControllers();

app.Run();
```

Call `UseRouting()` yourself. Otherwise .NET 6 and later put routing at the start of the pipeline, before FaultTrace.

On .NET Core 3.1 and 5, make the same calls in `Startup`:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddControllers();
    services.AddFaultTrace().AddOracle();
}

public void Configure(IApplicationBuilder app)
{
    app.UseFaultTrace();
    app.UseRouting();
    app.UseEndpoints(endpoints => endpoints.MapControllers());
}
```

## Read the settings from configuration

```csharp
builder.Services.AddFaultTrace(builder.Configuration.GetSection("FaultTrace")).AddOracle();
```

```json
{
  "FaultTrace": {
    "DefaultCulture": "uz-Latn",
    "DetailExposure": "DevelopmentOnly",
    "RetryAfterSeconds": 5
  }
}
```

Most settings reload without a restart. See [ASP.NET Core](aspnetcore.md) for the full list.

## What a response looks like

```json
{
  "type": "https://soyprofesor.github.io/FaultTrace/errors/not-found",
  "title": "Not found",
  "status": 404,
  "detail": "The requested resource was not found.",
  "instance": "/orders/5",
  "code": "not_found",
  "errorId": "KRGT-VFVP",
  "traceId": "9a1c4b7e2d3f4a5b8c6d7e8f9a0b1c2d",
  "timestamp": "2026-10-29T13:30:12Z"
}
```

| Field | Meaning |
|---|---|
| `type` | A link to the page about this code |
| `title` | A short localized title |
| `status` | The HTTP status |
| `detail` | The text to show to the user |
| `instance` | The path of the request |
| `code` | The stable key. **Frontends should rely on this, not on the text.** See [Error codes](errors/index.md) |
| `errorId` | The short ID the user can give to support |
| `traceId` | The trace ID of the request |
| `timestamp` | The time of the error, in UTC |
| `errors` | Field to messages, only for validation errors |
| `debug` | Details for developers, only when `DetailExposure` allows it |

The content type is `application/problem+json`. Empty fields are left out.

## Namespaces

| You need | Namespace |
|---|---|
| `AddFaultTrace`, `UseFaultTrace` | `FaultTrace.AspNetCore.DependencyInjection` |
| `AddOracle` | `FaultTrace.Oracle.DependencyInjection` |
| `AddFaultTraceCore` | `FaultTrace.Core.DependencyInjection` |
| `DetailExposure` and other settings | `FaultTrace.AspNetCore.Configuration` |
| `FaultResult`, `WriteFaultAsync` | `FaultTrace.AspNetCore.Results` |
| `IProblemEnricher` | `FaultTrace.AspNetCore.Response` |
| `IFaultTranslator`, `IFaultFactory`, `IFaultException`, `IFaultHook` | `FaultTrace.Core.Abstractions` |
| `Fault`, `FaultCodes`, `FaultCategory`, `FaultProblem` | `FaultTrace.Core.Model` |
| `FaultMessage` | `FaultTrace.Core.Localization` |

## Next steps

- Show [friendly column names](oracle.md#friendly-names) instead of a generic message.
- Choose [the default language](localization.md).
- Map [your own exceptions](aspnetcore.md#your-own-exceptions).
- See the running [samples](https://github.com/soyprofesor/FaultTrace/tree/main/samples).
