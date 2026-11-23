# Feature Specifications

This directory contains business specifications, related API contracts, and implementation progress for each Backend feature module following **Spec-Driven Development (SDD)**.

---

## 1. Existing Feature Catalog

| Feature Directory | Business Description | Status |
| :--- | :--- | :--- |
| **[auth-profile/](./auth-profile/spec.md)** | Registration, Login, Google OAuth, Personal Profile, Account Administration | Completed |
| **[listening-reading/](./listening-reading/spec.md)** | ETS-standard 200-question L&R exam, Excel Import, Master Audio, 990 Score Scale | Admin Completed |
| **[speaking/](./speaking/spec.md)** | 11-question Speaking test, 2-phase timer, Mic check, Gemini AI grading | In Progress |
| **[writing/](./writing/spec.md)** | 8-question Writing test, Exercise bank, Anonymous session, AI grading queue | Completed |
| **[admin-portal/](./admin-portal/spec.md)** | Dashboard, Score Profiles management, Media Assets library management | Completed |

---

## 2. Standard Feature Directory Structure

Each feature directory contains:
```text
features/<feature-name>/
├── spec.md              # Business spec, API contract, constraints, and Acceptance Criteria
└── tasks.md             # Execution checklist per Clean Architecture phase (optional during active dev)
```

---

## 3. How to Initialize a New Feature

When preparing to implement a new feature:
1. Copy the [_template/](./_template/) directory to a new directory:
   ```sh
   cp -r docs/features/_template docs/features/<feature-name>
   mv docs/features/<feature-name>/spec.template.md docs/features/<feature-name>/spec.md
   mv docs/features/<feature-name>/tasks.template.md docs/features/<feature-name>/tasks.md
   ```
2. Fill in `spec.md` and `tasks.md`.
3. Provide `spec.md` to the AI Agent to begin implementation with maximum signal-to-noise ratio.
