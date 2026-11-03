# method_not_allowed

**Method not allowed** · HTTP 405 · Retry makes sense: No

## What it means

The resource exists, but it doesn't support this HTTP method. This is usually a bug in the client.

## What to do

Correct the request. The `Allow` header, if present, lists the supported methods.

## Where it comes from

Routing.

[All error codes](index.md)
