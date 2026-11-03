# Oracle

```csharp
builder.Services.AddFaultTrace().AddOracle();
```

`FaultTrace.Oracle` has no reference to ODP.NET. It finds `Oracle.ManagedDataAccess.Client.OracleException` by name, also inside inner exceptions, and reads it through reflection. It has been run against drivers 3.21 and 23.x.

Errors are classified by their number only, never by their text, so the session's `NLS_LANGUAGE` doesn't matter.

## What the user sees

### Business errors

Text from `RAISE_APPLICATION_ERROR` (`ORA-20000` to `ORA-20999`) is meant for the user. It's passed through as written, without its ORA codes, the `ORA-06512` lines and the Oracle 23 help link. It isn't translated.

```sql
RAISE_APPLICATION_ERROR(-20001, 'The limit is exceeded');
```

```json
{ "code": "business", "status": 400, "detail": "The limit is exceeded" }
```

> **Keep `SQLERRM` out of this text.** FaultTrace removes the codes but can't know what the rest means. With `'Failed: ' || SQLERRM`, the user gets the Oracle text, including any table and column names in it.

```sql
-- Don't: reveals database names
RAISE_APPLICATION_ERROR(-20001, 'Failed: ' || SQLERRM);

-- Do: says what the user needs to know, and keeps the original error for the log
RAISE_APPLICATION_ERROR(-20001, 'The order could not be saved', TRUE);
```

### Technical errors

For anything else, the Oracle text is never shown. The user gets the localized text for the code. See [Error codes](errors/index.md) for which `ORA` code gives which `code`.

An `ORA` code FaultTrace doesn't know yet becomes `unexpected`, and its log entry has `Unmapped=True`, so you can find it and map it.

## Friendly names

Without mapping, a technical error has a generic message with no database names. To name the field or give your own message:

```csharp
builder.Services.AddFaultTrace().AddOracle(oracle => oracle
    .MapColumn("TREASURY_CODE", new Dictionary<string, string>
    {
        ["en"] = "Treasury code",
        ["uz-Latn"] = "Xazina kodi",
    })
    .MapConstraint("UQ_ORDERS_CODE", new Dictionary<string, string>
    {
        ["en"] = "An order with this code already exists",
        ["uz-Latn"] = "Bunday kodli buyurtma allaqachon mavjud",
    }));
```

| Mapping | Used by |
|---|---|
| `MapColumn` | `required_field`, `value_too_long` |
| `MapConstraint` | `duplicate`, `reference_not_found`, `has_dependents` |

For a culture you didn't list, the default culture is tried, then English. Names are matched case-insensitively.

## Changing how an error is classified

Change what one number means:

```csharp
oracle.OverrideCode(1403, FaultCategory.NotFound, FaultCodes.NotFound, 404);
```

Or decide with a rule, which runs before any other classification. A rule that throws is logged and counts as no match.

```csharp
oracle.AddRule(
    error => error.Number == 1 && error.Details.TryGetValue("constraint", out var name) && name.StartsWith("UQ_"),
    FaultCategory.Conflict,
    FaultCodes.Duplicate,
    409,
    messages: new Dictionary<string, string> { ["en"] = "This record already exists" });
```

## Finding where the error came from

Two values appear in the log, and in the `debug` block when it's allowed:

| Value | Meaning |
|---|---|
| `Origin` | Where the error happened |
| `Entry` | What .NET called |
| `Chain` | Every error with its frames, keeping the re-raise boundaries |

```
ORA-01400: cannot insert NULL into ("APP"."ORDERS"."TREASURY_CODE")
ORA-06512: at "APP.ORDER_REQ", line 1749      <- Origin
ORA-06512: at "APP.ORDER_PACK", line 775      <- Entry
ORA-06512: at line 1
```

How well the origin can be found depends on how the PL/SQL handles the error:

| PL/SQL | `Origin` |
|---|---|
| No handler | The real line |
| `RAISE_APPLICATION_ERROR(..., TRUE)` | The real line, because the original error is kept |
| `RAISE_APPLICATION_ERROR(...)` | Only the `RAISE_APPLICATION_ERROR` line. Oracle has dropped the original error |
| Plain `RAISE` | The last `RAISE` line. The real line stays in `Chain` |
| An error in a trigger | The trigger's line. `Entry` is still what .NET called |

### Plain RAISE

Oracle lists a re-raised error as one flat list, starting at the last `RAISE`, and nothing in the message marks the real line:

```
ORA-01400: cannot insert NULL into (...)
ORA-06512: at "APP.ORDER_PACK", line 53       <- the last RAISE, shown as Origin
ORA-06512: at "APP.ORDER_PACK", line 45       <- the RAISE before it
ORA-06512: at "APP.ORDER_PACK", line 27       <- the real line, only in Chain
ORA-06512: at "APP.ORDER_PACK", line 42
ORA-06512: at "APP.ORDER_PACK", line 50
ORA-06512: at line 1
```

To get the exact line, add a backtrace to the error, in a section that starts with the word `BACKTRACE`. FaultTrace reads it into `Origin` and removes it from the user's text.

```sql
EXCEPTION
  WHEN OTHERS THEN
    RAISE_APPLICATION_ERROR(
      -20003,
      'The order could not be saved' || CHR(10) || 'BACKTRACE: ' || DBMS_UTILITY.FORMAT_ERROR_BACKTRACE);
```

## Hiding helper packages

If errors go through a helper package of your own, leave it out of `Origin` and `Chain`. `RawMessage` stays complete.

```csharp
oracle.IgnoredPackages.Add("ERROR_PACK");
```

A name can be `ERROR_PACK` or `CORE.ERROR_PACK`.

## Connection errors

A failed connection is `unavailable` (503) and transient, so a [worker](workers.md) can retry it. These come from the database or the network, and some from the driver itself, whose numbers differ between versions:

| Situation | Driver 23.x | Driver 3.21 |
|---|---|---|
| Connection timeout | `ORA-50000` | `-1000` |
| Pool is full | `ORA-50012` | `-1012` |
| Server didn't answer | `ORA-50201` | `-6001` |
| Name can't be resolved | `ORA-12154` | `ORA-12154` |

A driver number without an ORA code appears in the log like `ODP-06001`. A code that isn't listed becomes `unexpected` with `Unmapped=True`.
