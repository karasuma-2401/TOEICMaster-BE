# AI Guidance - TOEICMaster Backend

This file is the top-level entrypoint for AI coding assistants. Read this file first, then route to the required documentation before editing code. In the two-repository workspace, also read `../AGENTS.md`.

---

## 1. AI Context Routing Matrix (Required Documents by Task)

Do not scan entire folders. Load only the targeted document required for your active task:

| Task | Required Context |
| :--- | :--- |
| **Feature Work** | `docs/features/<feature>/spec.md` + `docs/contracts/api-contract.md` |
| **Code Workflow & Testing Patterns** | `docs/foundations/feature-development.md` |
| **API Contracts / BFF** | `docs/contracts/api-contract.md` |
| **System Architecture & Layering** | `docs/foundations/architecture.md` |
| **Figma UI & Design Tokens** | `docs/ui-ux/screens-inventory.md` + `docs/ui-ux/design-tokens.md` |
| **Architecture Decision Records (ADR)** | `docs/decisions/` (e.g. `001-admin-lr-exam-revisions.md`) |
| **UAT & Operations Runbooks** | `docs/runbooks/` (e.g. `writing-uat-runbook.md`) |
| **Project-Wide Specs (Master PRD)** | `docs/specs/prd-mvp.md` |

*(To start a new feature, clone `docs/features/_template/` to `docs/features/<feature-name>/`).*

---

## 2. Hard Architectural & Safety Guardrails (Zero-Tolerance Rules)

AI assistants MUST strictly comply with these zero-tolerance guardrails:

1. **Clean Architecture & CQRS Invariants:**
   - Controllers call MediatR via `ISender.Send`. **DO NOT** put business logic or database queries directly in Controllers.
   - **DO NOT** create Generic Repositories (`IRepository<T>`) or custom ORM abstractions.
   - **DO NOT** inject `DbContext` into Controllers or Domain entities.
   - Always pass `CancellationToken` through handlers, validators, and I/O.
2. **Database & Migrations Boundary:**
   - **DO NOT** modify existing, already-applied EF Core migrations. Create a new migration file for schema changes.
   - **DO NOT** run `dotnet ef database update` against remote Neon or shared environments on your own initiative.
3. **Security & Secrets:**
   - **DO NOT** commit `.env`, credentials, connection strings, or sensitive keys.
   - **DO NOT** leak internal exceptions or raw stack traces in HTTP responses (use `ValidationProblemDetails` for 400).
4. **Codebase Hygiene:**
   - **DO NOT** add new NuGet packages without explicit instruction from the user.
   - **DO NOT** disable warnings-as-errors, analyzers, or nullable checks merely to pass CI.

---

## 3. Standard Verification Commands

Always run these commands from the backend repository root before completing a code change:

```sh
dotnet restore TOEICMaster.slnx --locked-mode
dotnet format TOEICMaster.slnx --verify-no-changes --no-restore
dotnet build TOEICMaster.slnx -c Release --no-restore
dotnet test TOEICMaster.slnx -c Release --no-build
```

*(For documentation-only changes, running Markdown diff checks is sufficient; build and application tests are not required).*

---

## 4. Enforced Rule Table

Every architectural, safety, and codebase hygiene guardrail is backed by an automated check.
Consult this table before making changes and verify the exact check when updating rules:

| Architectural & Safety Guardrail | Exact Enforcement Check |
| :--- | :--- |
| **Domain and Application isolation:** Core layers must not reference Infrastructure, ASP.NET Core, EF Core, or external client libraries. | `ArchitectureTests.Core_does_not_depend_on_infrastructure_or_http_frameworks` |
| **Clean Architecture layer boundaries:** Allowed project references are strictly bounded (`Api -> Application, Infrastructure`; `Infrastructure -> Application`; `Application -> Domain`; `Domain -> none`). | `ArchitectureTests.Layers_have_only_allowed_project_references` |
| **CQRS controller boundary:** Controllers must dispatch commands and queries via `ISender.Send` and never inject `DbContext`, `IRepository`, or `IMapper`. | `ArchitectureTests.Controllers_route_through_ISender_without_direct_persistence_or_mapper_dependencies` |
| **Safe ClaimsPrincipal extraction:** Controllers must use `User.GetRequiredUserId()` or extensions instead of raw `FindFirstValue` lookups. | `ArchitectureTests.Controllers_use_ClaimsPrincipalExtensions_instead_of_raw_claim_extraction` |
| **Unified ProblemDetails and typed DTOs:** Controllers must not instantiate `new ProblemDetails` directly or return anonymous `Ok(new { ... })` objects. | `ArchitectureTests.Controllers_do_not_instantiate_raw_ProblemDetails_or_anonymous_ok_objects` |
| **MediatR command validation:** Every MediatR command must define a FluentValidation validator unless explicitly allowlisted as parameterless or legacy. | `ArchitectureTests.MediatR_commands_have_validators_unless_explicitly_allowlisted` |
| **Solution-wide TimeProvider:** Domain, Application, Infrastructure, and Api must use `TimeProvider` rather than calling `DateTime.UtcNow` or `Now` directly. | `ArchitectureTests.All_layers_use_TimeProvider_instead_of_DateTime_UtcNow_or_Now` |
| **Split query enforcement:** Multi-collection Include queries in persistence must call `.AsSplitQuery()` to prevent Cartesian explosion. | `ArchitectureTests.Infrastructure_multi_collection_includes_use_AsSplitQuery` |
| **Safe XML / XXE defense:** XML and XLSX parsers must load through `SafeXmlLoader.Load` with DTD processing prohibited and null resolver. | `ArchitectureTests.Infrastructure_and_Api_must_use_SafeXmlLoader_instead_of_XDocument_Load` |
| **String enum JSON serialization:** Global JSON serializers must serialize enums as PascalCase strings without conflicting property-level converters. | `ArchitectureTests.Global_json_options_use_PascalCase_string_enums_and_no_incompatible_dto_converters` |
| **Strict compilation & warnings as errors:** Build must fail on any warning; nullable references and deterministic builds are enabled. | `Directory.Build.props` (`TreatWarningsAsErrors=true`, `Nullable=enable`, `Deterministic=true`) |
| **Dependency lock file integrity:** Restores must use locked mode to prevent unreviewed package version drift. | `Directory.Build.props` (`RestorePackagesWithLockFile=true`) + `dotnet restore --locked-mode` |
| **C# code formatting hygiene:** Code formatting must match solution rules without unformatted diffs. | `dotnet format TOEICMaster.slnx --verify-no-changes --no-restore` |
| **OpenAPI contract snapshot lock:** OpenAPI output must remain identical to `docs/contracts/openapi.json` and declare typed response schemas. | `OpenApiSnapshotTests.OpenApi_v1_snapshot_matches_live_endpoint_and_includes_admin_users_and_string_enums` |
| **Secret scanning:** Commits and working trees must not contain credentials, tokens, or private keys. | `.github/workflows/backend-ci.yml` (`ghcr.io/gitleaks/gitleaks`) |
| **Database migration boundary:** Applied migrations are immutable and tested against an isolated ephemeral PostgreSQL instance. | `.github/workflows/backend-ci.yml` (`dotnet ef database update` on isolated container) |
