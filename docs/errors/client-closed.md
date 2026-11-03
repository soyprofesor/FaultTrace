# client_closed

**Request cancelled** · HTTP 499 · Retry makes sense: No

## What it means

The client cancelled the request or closed the connection before the response was ready. Nobody is usually there to read this response.

## What to do

Nothing. It's normal when a user leaves a page.

## Where it comes from

A cancelled request, including `ORA-01013` when the request was aborted.

[All error codes](index.md)
