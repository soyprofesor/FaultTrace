# validation

**Invalid data** · HTTP 400 · Retry makes sense: No

## What it means

Some of the submitted data is invalid. The `errors` field, if present, lists the messages per field. Without it, the request body couldn't be read at all.

## What to do

Show the messages next to their fields, or the `detail` if there's no `errors`. Fix the data before sending it again.

## Where it comes from

ModelState validation, an unreadable request body, a request the server rejected, and an empty 400 response.

[All error codes](index.md)
