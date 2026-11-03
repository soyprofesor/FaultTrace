# Logging

FaultTrace logs through `Microsoft.Extensions.Logging`, so it works with Serilog, NLog and any other provider. The logger category is `FaultTrace`. The text is always in English, whatever language the user got.

## Finding an error

A user gives you an error ID like `M3QD-7HZA`. Search the logs for the `ErrorId` property.

IDs are random and short, so in rare cases two different errors can share one. If you find more than one entry, `TraceId` and the time tell them apart.

## Levels

| Level | Categories |
|---|---|
| `Error` | `Internal`: bugs and unknown errors |
| `Warning` | `Business`, `Validation`, `Conflict`, `Transient`, `Timeout` |
| `Information` | `Unauthorized`, `Forbidden`, `NotFound`, `ClientClosed` |

## Properties

Every entry has these structured properties, plus the .NET exception if there is one:

| Property | Meaning |
|---|---|
| `ErrorId` | The ID the user saw |
| `TraceId` | The trace ID of the request |
| `Code`, `Status`, `Category` | What the fault is |
| `Culture` | The language of the response |
| `NativeCode` | The provider's own code, such as `ORA-01400` |
| `Origin` | Where the error happened |
| `Entry` | What the application called |
| `Chain` | Every error with its frames |
| `Details` | Schema, table, column or constraint |
| `RawMessage` | The complete original message |
| `Unmapped` | The provider doesn't know this code yet |
| `Endpoint`, `Method`, `User` | The request |

`Unmapped=True` is worth watching. Those are real errors that only have the generic `unexpected` message, and you can [classify them](oracle.md#changing-how-an-error-is-classified).

## An error is logged once

If you translate an exception in a local `catch` and rethrow it, the global handler reuses the same `ErrorId` and doesn't log it again.

## The user ID

It comes from the `sub` claim or the mapped `NameIdentifier` claim, and is `anonymous` if there's none. To use something else:

```csharp
options.UserIdClaim = "uid";
options.UserIdResolver = context => context.Request.Headers["X-User"].ToString();
```

## Hooks

`IFaultHook` runs after a fault is handled. Use it for metrics or notifications:

```csharp
public sealed class FaultMetrics : IFaultHook
{
    public void OnFault(Fault fault, Exception? exception)
    {
        Counter.Add(1, new KeyValuePair<string, object?>("code", fault.Code));
    }
}
```

```csharp
builder.Services.AddSingleton<IFaultHook, FaultMetrics>();
```

Register a hook as a singleton. It runs on the error path, so keep it fast and queue slow work elsewhere. A hook that fails is logged and doesn't affect the response.
