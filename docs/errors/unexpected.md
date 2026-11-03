# unexpected

**Unexpected error** · HTTP 500 · Retry makes sense: No

## What it means

Something went wrong on the server. This is usually a bug, and the `detail` is deliberately generic.

## What to do

Show the `detail` along with the `errorId`, so the user can give it to support. Developers can find the cause in the logs by that ID.

## Where it comes from

Any exception FaultTrace has no better code for, `ORA-01403`, `ORA-01422`, `ORA-06502`, `ORA-06550`, `ORA-00942`, `ORA-01031`, `ORA-04061`, `ORA-06508`, any unknown `ORA` code, and any other empty 5xx response.

[All error codes](index.md)
