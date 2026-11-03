# Workers

Without ASP.NET Core, use `FaultTrace.Core` and the providers you need:

```csharp
services.AddFaultTraceCore().AddOracle();
```

Inject `IFaultTranslator` and translate what you catch. The fault is logged with all its details, so your own log line only needs the ID. `Fault.IsTransient` tells you whether another attempt makes sense:

```csharp
for (var attempt = 1; attempt <= MaxAttempts; attempt++)
{
    try
    {
        await handler.HandleAsync(message);
        return;
    }
    catch (Exception exception)
    {
        var fault = translator.Translate(exception);
        if (!fault.IsTransient)
        {
            await deadLetters.SendAsync(message, fault.ErrorId, fault.Code);
            return;
        }
    }

    await Task.Delay(TimeSpan.FromSeconds(attempt));
}

await deadLetters.SendAsync(message);
```

## What counts as transient

| Code | Transient | Examples |
|---|---|---|
| `unavailable` | Yes | A lost connection, a deadlock, a full pool |
| `timeout` | Yes | A `TimeoutException`, `ORA-01013` |
| `too_many_requests` | Yes | A 429 from your own exception |
| Everything else | No | Business rules, invalid data, bugs |

To change this for one Oracle error, see [`OverrideCode`](oracle.md#changing-how-an-error-is-classified).

Each attempt that fails is logged with its own error ID, because each one is a new exception.

## Language

There's no request to take a language from, so the default culture is used. To choose one:

```csharp
var fault = translator.Translate(exception, "ru");
```

See the [worker sample](https://github.com/soyprofesor/FaultTrace/tree/main/samples/Sample.Worker) for a running example.
