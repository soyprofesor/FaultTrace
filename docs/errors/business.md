# business

**Request could not be completed** · HTTP 400 · Retry makes sense: No

## What it means

A business rule in the application rejected the operation. The `detail` is written by the application's developers and is meant for the user.

## What to do

Show the `detail` to the user. Sending the same request again gives the same result until the data changes.

## Where it comes from

`ORA-20000` to `ORA-20999` (`RAISE_APPLICATION_ERROR`), and any other empty 4xx response.

[All error codes](index.md)
