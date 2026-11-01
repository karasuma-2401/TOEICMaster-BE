# Implementing a backend feature

This is a practical checklist for developers and AI tools. See [architecture.md](architecture.md) for the full architecture and the [README](../README.md) for setup. Start from the issue's acceptance criteria; do not add entities, endpoints, or packages just because this checklist mentions them.

## Choose the feature path

| Need | Location | Existing example |
| --- | --- | --- |
| Business rules and state | `src/TOEICMaster.Domain/` | Domain has no entities yet; define invariants before adding one |
| Read data or perform an action | `src/TOEICMaster.Application/<Feature>/Queries/<Action>/` or `Commands/<Action>/` | `System/Queries/GetSystemStatus/GetSystemStatusQuery.cs`, `Email/Commands/SendEmail/` |
| Validate input | Validator in the same use case | `SendEmailCommandValidator.cs`, `Common/Behaviors/ValidationBehavior.cs` |
| Database, cache, storage, or email | Interface in Application, implementation in Infrastructure | `Email/IMailSender.cs`, `Infrastructure/Email/SmtpMailSender.cs` |
| HTTP request/response | `src/TOEICMaster.Api/Controllers/`, mapping in `Api/Mapping/` | `SystemController.cs`, `SystemProfile.cs` |
| Tests | `tests/TOEICMaster.Tests/` | `ApplicationTests.cs`, `ApiTests.cs`, `ValidationApiTests.cs` |

Implementation order: define the contract and error cases → implement the use case and validator → add an adapter only when I/O is needed → add the endpoint and mapping → test → check the frontend if it uses the contract. Controllers call `ISender.Send(request, cancellationToken)`; handlers forward the token through I/O. Register adapters in `Infrastructure/DependencyInjection.cs`. Review `Application/DependencyInjection.cs` before adding handler, validator, or mapping registration because assembly scanning is already configured.

## Two existing examples

**Read query across layers:** `GET /api/v1/system/ping` → `SystemController.Ping` → `GetSystemStatusQuery` and handler → `SystemStatusDto` → `SystemProfile` → `PingResponse`. `ApiTests.Ping_has_the_frontend_contract` checks the real response through the test host. The frontend uses this endpoint through a same-origin route handler. This query needs no database; do not assume that for new data queries.

**Validated command with a side effect:** `SendEmailCommand` → `SendEmailCommandValidator` → `ValidationBehavior` → handler → `IMailSender` → `SmtpMailSender`. `ApplicationTests` covers valid input, invalid input, asynchronous validation, cancellation, and the absence of mail delivery on validation failure. The command is currently internal and used in tests: **there is no public mail-sending endpoint**. `ValidationApiTests` uses a test-only controller to check HTTP 400; do not turn that controller into a production endpoint.

## Test the behavior that changed

- Business rules: test valid states, boundaries, and rejected states; prefer focused Domain/Application tests.
- Commands with I/O: use a fake adapter such as `RecordingMailSender`. Verify the side effect occurs exactly once, does not occur for invalid input, and receives the correct `CancellationToken`.
- Endpoints: use `ApiFactory`/`WebApplicationFactory` to verify status, JSON contract, validation, and safe errors. The factory uses fake configuration; current tests do not require real Neon, Redis, MinIO, or SMTP services.
- New persistence: add meaningful tests for queries, mapping, and migrations. If a test needs a database, use an isolated, disposable test environment; never target shared Neon. Add test-container tooling only when the first use case needs it.
- Frontend impact: check [api-contract.md](api-contract.md), update the separate frontend repository, and test both sides.

## Run locally and verify

The existing test suite needs no secrets:

```sh
dotnet restore TOEICMaster.slnx --locked-mode
dotnet format TOEICMaster.slnx --verify-no-changes --no-restore
dotnet build TOEICMaster.slnx -c Release --no-restore
dotnet test TOEICMaster.slnx -c Release --no-build
```

To run the real API, follow the [README](../README.md) to configure `.env.example` and .NET user secrets, start Redis/MinIO/Mailpit, then run `dotnet run --project src/TOEICMaster.Api`. `GET /health/live` checks the process; `/health/ready` also checks Neon and supporting services, so it can return 503 until they are configured. Do not use shared Neon for tests or apply migrations to it automatically.

## Before handing off

Match each acceptance criterion to a test or explicit verification step. Report changed files/contracts, commands run, results, and remaining limits. If a business decision is missing, record the assumption in the issue or PR instead of silently inventing a rule.
