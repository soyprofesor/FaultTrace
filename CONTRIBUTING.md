# Contributing

Thanks for helping. For anything bigger than a small fix, please open an issue first, so we can agree on the approach.

## Building and testing

You need the .NET 10 SDK. To run the tests on every target framework, you also need the .NET Core 3.1, .NET 6 and .NET 8 runtimes.

```bash
dotnet build -c Release
dotnet test -c Release
```

The build treats warnings as errors.

### Integration tests

These run against a real Oracle database when `FAULTTRACE_TEST_ORACLE_CONNECTION` holds a connection string, and are skipped otherwise. They create objects named `FAULTTRACE_TEST_*` and drop them afterwards, so **use a test database**. Keep the connection string out of the repository.

## Rules

- **Never put employer or organization data in the repository.** That means real schema, table, package, constraint or column names, internal URLs, IDs and screenshots. Anonymize error samples before adding them, using names like `APP.ORDER_PACK`.
- **Never let raw details reach a production response.** Stack traces, `ORA` codes, database names and exception messages belong only in the `debug` block and the logs. The one exception is text a developer passed to `RAISE_APPLICATION_ERROR`, without its codes.
- **Never swallow an error silently.** If the library fails while handling an error, it logs that and still sends a response.
- **Classify Oracle errors by number, never by text.** Message text depends on `NLS_LANGUAGE`.
- **Don't reference ODP.NET from `src`.** The driver is read through reflection, to avoid version conflicts in consumer projects.
- **`FaultTrace.Core` must not depend on Oracle or ASP.NET Core.**
- **Keep the public API the same on all target frameworks.** Don't use `init` on public types that consumers construct, because .NET Core 3.1 consumers on C# 8 can't set them.
- **Give every regex a timeout.**
- **Discuss new dependencies first.** Use xUnit and Shouldly in tests, not FluentAssertions.

## Tests

- Every parser behavior needs a fixture in `tests/FaultTrace.Oracle.Tests/Fixtures`: a raw message and the expected result.
- A bug fix comes with a test that fails without it.

## Changes

- Write code, comments, docs, commit messages and test names in English.
- Add public API changes and anything users would notice to `CHANGELOG.md`. Versions follow Semantic Versioning.
- Keep a pull request to one topic.

## Releasing

Pushing a tag like `v0.1.0-alpha` or `v1.2.0` builds, tests and publishes the packages with that version. Before tagging, move the `Unreleased` section of `CHANGELOG.md` under the new version.
