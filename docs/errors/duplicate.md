# duplicate

**Already exists** · HTTP 409 · Retry makes sense: No

## What it means

A record with the same unique value already exists. If the application has mapped the constraint, the `detail` says which one.

## What to do

Tell the user, and let them change the value or open the existing record.

## Where it comes from

`ORA-00001`.

[All error codes](index.md)
