# Changelog

All notable changes are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `FaultTrace.Core`: `Fault`, `IFaultTranslator`, `IFaultFactory`, `IFaultProvider`, `IFaultHook`, `IFaultException`, short error IDs, structured logging, and built-in texts in `en`, `uz-Latn`, `uz-Cyrl` and `ru`.
- `FaultTrace.Oracle`: classification by `ORA` number, PL/SQL stack parsing with origin, entry and chain, friendly column and constraint names, code overrides and custom rules. It has no reference to ODP.NET.
- `FaultTrace.AspNetCore`: `UseFaultTrace()` with RFC 9457 responses for exceptions, validation errors and empty 4xx and 5xx responses, the `debug` block, culture resolution, `IProblemEnricher`, and `FaultResult` and `WriteFaultAsync` for errors caught locally.
- Samples for .NET 8, .NET Core 3.1 and a worker, and integration tests against a real Oracle database.
