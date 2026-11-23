# Architecture Foundations & Standards (Foundations)

This directory contains documents that define the entire technical structure, invariant rules, and coding standards of the Backend project.

---

## 1. Purpose
- Establish solid technical **Guardrails** so AI and team members do not violate architectural principles.
- Provide **Golden Samples** for AI to follow (In-context Learning) when generating new source code.
- Clearly define **Anti-patterns** (prohibited practices) in the codebase.

---

## 2. Documents in This Layer
- **System Architecture (`architecture.md`):** 4-layer Clean Architecture split (`Domain`, `Application`, `Infrastructure`, `Api`), MediatR processing flow, and project dependency rules.
- **Coding Conventions (`coding-conventions.md`):** File structure, C# naming conventions, exception handling, logging, validation, and Command/Query standard samples.
- **Feature Development Process (`feature-development.md`):** Step-by-step guide to developing a feature from design to writing xUnit tests.

> [!TIP]
> Whenever AI proposes a solution that violates the architecture (for example: injecting DbContext directly into a Controller or performing I/O in Domain), this layer's documentation is the basis for reminding and realigning AI behavior.
