# Security policy

## Supported versions

FaultTrace hasn't had a stable release yet. Security fixes go into the latest version.

## Reporting a vulnerability

**Please don't open a public issue.** Report it privately through [GitHub's security advisories](https://github.com/soyprofesor/FaultTrace/security/advisories/new).

Include what you found, how to reproduce it and which version you used. Remove any real database names, connection strings and other private data from your report.

You'll get an answer as soon as possible. This is a project maintained in spare time, so there's no guaranteed response time.

## What counts as a vulnerability

The library's main promise is that internal details don't reach the user, so these are the most important:

- A production response that contains a stack trace, an `ORA` code, a database name or a raw exception message, when the `debug` block isn't allowed.
- The `debug` block appearing when `DetailExposure` doesn't allow it.
- Log injection through a request value.
- A request that makes the library hang or crash, for example through a regex.

## What doesn't

- Database names in text a developer passed to `RAISE_APPLICATION_ERROR`, such as `'Failed: ' || SQLERRM`. That text is shown as written, and [the docs](https://soyprofesor.github.io/FaultTrace/oracle) explain how to avoid this.
- Details in the `debug` block of an application that set `DetailExposure` to `Always`.
- Details in the logs. Logs are meant to hold them, so protect them accordingly.
