# ASP.NET Core

## What gets handled

| Situation | Result |
|---|---|
| An exception nobody caught | The template. The code depends on the exception |
| ModelState errors, `ValidationProblem()`, `BadRequest(ModelState)` | `validation` with `errors` |
| A 4xx or 5xx response without a body | The code of that status. See [Error codes](errors/index.md) |
| The client cancelled the request | `client_closed` (499) |
| A timeout | `timeout` (504) |

Some responses are left alone on purpose:

- Successful responses.
- A response that already has a body or a content type, because it belongs to someone else. This includes `NotFound("text")`, `Problem()`, `Results.Problem()` and `Results.ValidationProblem()`.
- A response that has already started, such as a stream. It can't be rewritten, so the error is logged and the connection is aborted.

If FaultTrace itself fails while handling an error, it logs that and writes a minimal English 500 response with an error ID.

## Settings

Set them in code or through [configuration](getting-started.md#read-the-settings-from-configuration). They reload without a restart, except those marked as read at startup.

```csharp
builder.Services.AddFaultTrace(options =>
{
    options.DetailExposure = DetailExposure.DevelopmentOnly;
    options.RetryAfterSeconds = 5;
});
```

| Setting | Default | Meaning |
|---|---|---|
| `DetailExposure` | `DevelopmentOnly` | When a response may contain the `debug` block: `Never`, `DevelopmentOnly` or `Always` |
| `DetailExposurePredicate` | none | Narrows `DetailExposure`, for example to one role. It can't widen it |
| `TypeBaseUri` | The FaultTrace docs | The start of the `type` link. Point it at your own pages if you have them |
| `RetryAfterSeconds` | 5 | The `Retry-After` value on a 503. Zero or less sends no header |
| `UserIdClaim` | `sub` | The claim that holds the user ID for the logs. The mapped `NameIdentifier` claim is also checked |
| `UserIdResolver` | none | Gets the user ID in your own way |
| `HandleEmptyStatus` | `true` | Whether a 4xx or 5xx without a body gets the template |
| `EmptyStatusFilter` | none | Narrows `HandleEmptyStatus` to some requests |
| `HandleValidation` | `true` | Whether validation errors get the template. Read at startup |
| `DefaultCulture` | `en` | See [Localization](localization.md). Read at startup |

### The debug block

The `debug` block contains raw messages, database names and the stack trace. Keep it away from users.

```csharp
options.DetailExposure = DetailExposure.Always;
options.DetailExposurePredicate = context => context.User.IsInRole("Developer");
```

A predicate that throws is logged and counts as `false`.

## Empty responses

An empty response gets the template whoever produced it: routing, an authentication handler or your own code.

```csharp
return NotFound();                  // the not_found template
return StatusCode(500);             // the unexpected template
return NotFound("No such order");   // left alone, it has a body
```

You can turn this off, or only for some paths:

```csharp
options.HandleEmptyStatus = false;
options.EmptyStatusFilter = context => !context.Request.Path.StartsWithSegments("/legacy");
```

Headers you set stay, such as `WWW-Authenticate` on a 401 or `Allow` on a 405. Unhandled exceptions always get the template.

## Validation

```json
{
  "code": "validation",
  "detail": "Some of the submitted data is invalid.",
  "errors": {
    "amount": ["The field Amount must be between 1 and 1000."],
    "items[0].code": ["The Code field is required."]
  }
}
```

Field names are written the way the client sent them. Your own validation messages are shown as written.

If the request body can't be read at all, for example broken JSON, the response has only the `detail`, with no `errors`. A value of the wrong type gets the generic text under its field. Parser messages never reach the user, with `System.Text.Json` or with Newtonsoft.

> **With Newtonsoft**, also set `AllowInputFormatterExceptionMessages = false`. FaultTrace recognizes Newtonsoft's messages by their format, and this setting stops them at the source.

To keep the MVC format and your own `InvalidModelStateResponseFactory`, set `HandleValidation = false`.

## Your own exceptions

There are two ways. Both set the status, code, text and field errors.

Implement `IFaultException` on the exception:

```csharp
public sealed class OrderClosedException : Exception, IFaultException
{
    public int Status => 409;
    public string Code => "order_closed";
    public string? Detail => "This order is closed and can't be changed.";
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors => null;
}
```

Or map it without changing its class, which also suits exceptions from other libraries:

```csharp
options.MapException<KeyNotFoundException>(_ => new FaultProblem(404, FaultCodes.NotFound));
```

- `Detail` is shown as written, so it must be safe to show. If it's empty, the library's text for the code is used.
- `Status` also decides the category, and so the log level and whether the fault is transient.
- If `Errors` is set, the fault counts as a validation fault.
- A mapping covers derived types, and the most specific one wins. It comes before `IFaultException` and all providers.
- A mapping that throws is logged and leaves the exception to the next rule.

## Handling an error yourself

**Let database and infrastructure exceptions go up to the global handler.** Catch locally only when a decision depends on the error.

When you do catch one, translate it. The error is logged once, and rethrowing it keeps the same error ID.

```csharp
try
{
    await orders.AddAsync(order);
}
catch (Exception exception)
{
    var fault = translator.Translate(exception);
    if (fault.Code == FaultCodes.Duplicate)
    {
        return Ok(await orders.GetAsync(order.Id));
    }

    return this.FaultResult(fault);
}
```

Outside a controller, such as in a Minimal API handler or a middleware, write the response directly:

```csharp
await context.WriteFaultAsync(fault);
```

### The Result pattern

FaultTrace has no `Result` type of its own. If your project has one, map a `Fault` to your own error type and keep the `ErrorId`:

```csharp
catch (Exception exception)
{
    var fault = translator.Translate(exception);
    return Result.Failure(new Error(fault.Code, fault.UserMessage, fault.ErrorId));
}
```

To create a fault with no exception behind it, use `IFaultFactory`. It's logged like any other:

```csharp
var fault = faults.Create(FaultCategory.Conflict, "order_closed", 409, detail: "This order is closed.");
```

## Rate limiting

FaultTrace doesn't limit anything itself, but it works with `UseRateLimiter`. Set the status, because the framework rejects with 503 by default, and put the limiter after `UseFaultTrace()`:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("orders", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
});
```

`Retry-After` isn't added to a 429, because FaultTrace doesn't know the limiter's window. Set it in `OnRejected`, and the template keeps it:

```csharp
options.OnRejected = (context, _) =>
{
    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
    {
        context.HttpContext.Response.Headers["Retry-After"] = ((int)retryAfter.TotalSeconds).ToString();
    }

    return ValueTask.CompletedTask;
};
```

If `OnRejected` writes its own body, the response is left alone.

## Adding fields to the response

The standard fields can't be removed or renamed, but you can add your own. Register as many enrichers as you need:

```csharp
public sealed class TenantEnricher : IProblemEnricher
{
    public void Enrich(HttpContext context, Fault fault, IDictionary<string, object?> extensions)
    {
        extensions["tenant"] = context.Request.Headers["X-Tenant"].ToString();
    }
}
```

```csharp
builder.Services.AddSingleton<IProblemEnricher, TenantEnricher>();
```

A key that matches a standard field is ignored. An enricher that fails is logged, and the response goes out without its fields. Don't copy `Fault.Provider` here, because it can reveal database names.
