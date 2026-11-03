# FaultTrace

FaultTrace turns exceptions, first of all Oracle errors (`ORA-xxxxx`), into two things:

- a **safe, localized error response** in one format (RFC 9457 `application/problem+json`), and
- a **structured log entry** that shows where the error came from, down to the PL/SQL package and line.

The user sees a clear message and a short error ID. Developers search the logs by that ID.

> **Status:** FaultTrace is an early preview. The public API may still change.

## The problem

Without a handler, this is what a user can get from a database error:

```
ORA-01400: cannot insert NULL into ("APP"."ORDERS"."TREASURY_CODE")
ORA-06512: at "APP.ORDER_REQ", line 1749
ORA-06512: at "APP.ORDER_PACK", line 775
ORA-06512: at line 1
```

It reveals schema, table and package names, and it means nothing to the user.

## The result

The same error with FaultTrace:

```json
{
  "type": "https://soyprofesor.github.io/FaultTrace/errors/required-field",
  "title": "Incomplete data",
  "status": 400,
  "detail": "A required field is missing: Treasury code",
  "instance": "/api/orders",
  "code": "required_field",
  "errorId": "M3QD-7HZA",
  "traceId": "9a1c4b7e2d3f4a5b8c6d7e8f9a0b1c2d",
  "timestamp": "2026-10-29T13:30:12Z"
}
```

And in the log, under the same `errorId`:

```
Fault M3QD-7HZA: required_field (400) from APP.ORDER_REQ:1749.
NativeCode=ORA-01400 Entry=APP.ORDER_PACK:775 Details=[schema=APP, table=ORDERS, column=TREASURY_CODE] ...
```

## Quick start

```bash
dotnet add package FaultTrace.AspNetCore --prerelease
dotnet add package FaultTrace.Oracle --prerelease
```

```csharp
using FaultTrace.AspNetCore.DependencyInjection;
using FaultTrace.Oracle.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddFaultTrace().AddOracle();

var app = builder.Build();

app.UseFaultTrace(); // first in the pipeline
app.UseRouting();
app.MapControllers();

app.Run();
```

From here, these all use the same format:

- every exception nobody caught,
- ModelState validation errors,
- any 4xx or 5xx response without a body,
- cancelled requests and timeouts.

## Packages

| Package | For | Targets |
|---|---|---|
| `FaultTrace.AspNetCore` | Web applications | .NET Core 3.1, .NET 6, 8 and 10 (.NET 5 uses the 3.1 build) |
| `FaultTrace.Oracle` | Oracle errors, in any host | .NET Standard 2.0 |
| `FaultTrace.Core` | Workers and other hosts without ASP.NET Core | .NET Standard 2.0 |

`FaultTrace.Oracle` has no reference to ODP.NET. It reads `OracleException` through reflection, so it works with the driver version your project already uses.

## Documentation

- [Getting started](https://soyprofesor.github.io/FaultTrace/getting-started)
- [ASP.NET Core](https://soyprofesor.github.io/FaultTrace/aspnetcore): what gets handled, settings, your own exceptions
- [Oracle](https://soyprofesor.github.io/FaultTrace/oracle): what users see, how the origin is found, mapping names
- [Localization](https://soyprofesor.github.io/FaultTrace/localization): cultures, your own texts
- [Logging](https://soyprofesor.github.io/FaultTrace/logging): levels, properties, hooks
- [Workers](https://soyprofesor.github.io/FaultTrace/workers): retry or dead-letter
- [Error codes](https://soyprofesor.github.io/FaultTrace/errors/): every `code` a response can have

Working examples are in [samples](https://github.com/soyprofesor/FaultTrace/tree/main/samples): an API on [.NET 8](https://github.com/soyprofesor/FaultTrace/tree/main/samples/Sample.Api.Net8), one on [.NET Core 3.1](https://github.com/soyprofesor/FaultTrace/tree/main/samples/Sample.Api.NetCore31) and a [worker](https://github.com/soyprofesor/FaultTrace/tree/main/samples/Sample.Worker).

## Building

```bash
dotnet build -c Release
dotnet test -c Release
```

The integration tests run against a real Oracle database when `FAULTTRACE_TEST_ORACLE_CONNECTION` is set, and are skipped otherwise. They create objects named `FAULTTRACE_TEST_*` and drop them afterwards, so point them at a test database.

## License

[MIT](https://github.com/soyprofesor/FaultTrace/blob/main/LICENSE)
