# unavailable

**Service unavailable** · HTTP 503 · Retry makes sense: Yes

## What it means

A service the application needs, usually the database, is temporarily unavailable.

## What to do

Wait and try again. The `Retry-After` header says how long, in seconds.

## Where it comes from

`ORA-00060`, `ORA-00054`, `ORA-04068`, `ORA-03113`, `ORA-03114`, `ORA-03135`, the `ORA-12xxx` connection range, and the driver's own connection errors. See [Oracle](../oracle.md#connection-errors).

[All error codes](index.md)
