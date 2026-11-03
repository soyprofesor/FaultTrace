# too_many_requests

**Too many requests** · HTTP 429 · Retry makes sense: Yes

## What it means

The caller sent too many requests in a short time.

## What to do

Wait and try again. If there's a `Retry-After` header, wait that long.

## Where it comes from

A rate limiter or the application.

[All error codes](index.md)
