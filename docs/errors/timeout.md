# timeout

**Request timed out** · HTTP 504 · Retry makes sense: Yes

## What it means

The operation took too long and was stopped.

## What to do

Try again. If it keeps happening, tell the user to try later.

## Where it comes from

`ORA-01013`, a `TimeoutException`, and a cancelled operation while the client is still connected.

[All error codes](index.md)
