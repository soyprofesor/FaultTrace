# Error codes

Every FaultTrace response has a `code`. It's a stable key, so a frontend can rely on it and keep its own texts if it wants to. Applications can also have codes of their own.

| `code` | Status | Retry makes sense | Title |
|---|---|---|---|
| [`business`](business.md) | 400 | No | Request could not be completed |
| [`validation`](validation.md) | 400 | No | Invalid data |
| [`required_field`](required-field.md) | 400 | No | Incomplete data |
| [`value_too_long`](value-too-long.md) | 400 | No | Invalid data |
| [`invalid_format`](invalid-format.md) | 400 | No | Invalid data |
| [`reference_not_found`](reference-not-found.md) | 400 | No | Invalid data |
| [`unauthorized`](unauthorized.md) | 401 | No | Authentication required |
| [`forbidden`](forbidden.md) | 403 | No | Access denied |
| [`not_found`](not-found.md) | 404 | No | Not found |
| [`method_not_allowed`](method-not-allowed.md) | 405 | No | Method not allowed |
| [`unsupported_media_type`](unsupported-media-type.md) | 415 | No | Unsupported format |
| [`duplicate`](duplicate.md) | 409 | No | Already exists |
| [`has_dependents`](has-dependents.md) | 409 | No | Record is in use |
| [`conflict`](conflict.md) | 409 | No | Conflict |
| [`too_many_requests`](too-many-requests.md) | 429 | Yes | Too many requests |
| [`unexpected`](unexpected.md) | 500 | No | Unexpected error |
| [`unavailable`](unavailable.md) | 503 | Yes | Service unavailable |
| [`timeout`](timeout.md) | 504 | Yes | Request timed out |
| [`client_closed`](client-closed.md) | 499 | No | Request cancelled |

A 4xx or 5xx response without a body gets the code of its status above. Any other empty 4xx is `business`, and any other empty 5xx is `unexpected`, with the status kept as it was.
