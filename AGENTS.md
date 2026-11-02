# AI Guidance — TOEICMaster Backend

This file is the top-level entrypoint for AI coding assistants. Read this file first, then route to the required documentation before editing code. In the two-repository workspace, also read `../AGENTS.md`.

---

## 1. AI Context Routing Matrix (Tài liệu bắt buộc theo tác vụ)

Do not scan entire folders. Load only the targeted document required for your active task:

| Nhiệm vụ / Task | Tài liệu bắt buộc nạp / Required Context |
| :--- | :--- |
| **Phát triển tính năng (Feature Work)** | `docs/features/<feature>/spec.md` + `docs/contracts/api-contract.md` |
| **Quy trình code & Testing mẫu** | `docs/foundations/feature-development.md` |
| **Giao ước API (API Contracts / BFF)** | `docs/contracts/api-contract.md` |
| **Kiến trúc hệ thống & Layering** | `docs/foundations/architecture.md` |
| **Giao diện Figma & Design Tokens** | `docs/ui-ux/screens-inventory.md` + `docs/ui-ux/design-tokens.md` |
| **Quyết định kiến trúc lịch sử (ADR)** | `docs/decisions/` (e.g. `001-admin-lr-exam-revisions.md`) |
| **Kịch bản kiểm thử UAT & Vận hành** | `docs/runbooks/` (e.g. `writing-uat-runbook.md`) |
| **Đặc tả toàn dự án (PRD tổng thể)** | `docs/specs/prd-mvp.md` |

*(To start a new feature, clone `docs/features/_template/` to `docs/features/<feature-name>/`).*

---

## 2. Hard Architectural & Safety Guardrails (Lệnh cấm tuyệt đối)

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

## 3. Standard Verification Commands (Kiểm chứng kết quả)

Always run these commands from the backend repository root before completing a code change:

```sh
dotnet restore TOEICMaster.slnx --locked-mode
dotnet format TOEICMaster.slnx --verify-no-changes --no-restore
dotnet build TOEICMaster.slnx -c Release --no-restore
dotnet test TOEICMaster.slnx -c Release --no-build
```

*(For documentation-only changes, running Markdown diff checks is sufficient; build and application tests are not required).*
