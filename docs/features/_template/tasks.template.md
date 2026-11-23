# Execution Checklist: [Feature Name]

> **Specification:** [spec.md](./spec.md)  
> **Status:** [Not Started / In Progress / Completed]

---

## Execution Principles for AI
1. Complete each task in architectural layer order: `Domain` -> `Application` -> `Infrastructure` -> `Api` -> `Tests`.
2. After each task or phase, run the corresponding checks (`dotnet build`, `dotnet test`).
3. Mark `[x]` on each completed item.

---

## Phase 1: Domain & Entities
- [ ] Define or update Entities / Value Objects in `Domain/Entities/`.
- [ ] Establish business invariants, constructors, and domain events (if any).
- [ ] Run `dotnet build TOEICMaster.slnx` to verify build.

## Phase 2: Application (CQRS & Business Logic)
- [ ] Create Command/Query record in `Application/<Feature>/Commands/` or `Queries/`.
- [ ] Create DTOs and configure AutoMapper profile.
- [ ] Write FluentValidation validator inheriting `AbstractValidator<T>`.
- [ ] Write Command/Query Handler (`IRequestHandler<TRequest, TResponse>`).
- [ ] Ensure `CancellationToken` is passed through all asynchronous calls.

## Phase 3: Infrastructure (Persistence & External Services)
- [ ] Configure EF Core EntityTypeConfiguration (if new entities are added).
- [ ] Create migration using source scripts (if schema changes exist).
- [ ] Implement repository or external service adapters in `Infrastructure/`.

## Phase 4: API Presentation
- [ ] Create or update Controller in `Api/Controllers/v1/`.
- [ ] Dispatch via MediatR through `ISender.Send`.
- [ ] Declare Swagger attributes (`[ProducesResponseType]`, `[Authorize]`).
- [ ] Ensure errors are mapped and returned in `ValidationProblemDetails` format.

## Phase 5: Verification & Testing
- [ ] Write Unit tests for Handler and Validator in `tests/TOEICMaster.Application.UnitTests/`.
- [ ] Write Integration tests for the API endpoint in `tests/TOEICMaster.Api.IntegrationTests/`.
- [ ] Run the full test suite:
  ```sh
  dotnet test TOEICMaster.slnx -c Release
  ```
- [ ] Verify code formatting:
  ```sh
  dotnet format TOEICMaster.slnx --verify-no-changes --no-restore
  ```

## Phase 6: Handoff & Review
- [ ] Update [api-contract.md](../../contracts/api-contract.md) if any API contract changes occurred.
- [ ] Summarize changes and key integration notes for connecting with Frontend.
