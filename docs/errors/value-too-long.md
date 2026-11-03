# value_too_long

**Invalid data** · HTTP 400 · Retry makes sense: No

## What it means

A value is longer than the field allows. If the application has mapped the column, the `detail` names the field.

## What to do

Ask the user to shorten the value.

## Where it comes from

`ORA-12899`, `ORA-01401`.

[All error codes](index.md)
