# Documentation & Context Map

Documentation in `docs/` is organized using a **Hierarchical Context Architecture** to optimize comprehension for developers and AI Coding Agents (Cursor, Claude Code, Gemini, Copilot).

---

## 1. Directory Structure Map

```text
docs/
├── README.md                      # [Layer 0] Overview documentation map (this file)
├── jira.md                        # Project Jira workspace information (Project CNPMCS, Board, Sprints, Issues)
│
├── foundations/                   # [Layer 1] Architecture foundations, standards & guardrails
│   ├── README.md
│   ├── architecture.md            # Clean Architecture, CQRS, MediatR, and Layering
│   └── feature-development.md     # Standard feature development & testing workflow guide
│
├── contracts/                     # [Layer 2] Data contracts (Single Source of Truth)
│   ├── README.md
│   └── api-contract.md            # API endpoints list, Request/Response payloads, HTTP statuses
│
├── features/                      # [Layer 3] Lean feature specs & implementation plans (High SNR)
│   ├── README.md
│   ├── _template/                 # Standard templates spec.template.md & tasks.template.md
│   ├── auth-profile/spec.md       # Auth, Google OAuth & Profile module
│   ├── listening-reading/spec.md  # TOEIC L&R 200-question exam, 103 blocks, 990 score scale module
│   ├── speaking/spec.md           # Speaking test 11-question, 2-phase timer, AI grading module
│   ├── writing/                   # Writing test 8-question, Anonymous session & AI grading module
│   │   ├── spec.md, tasks.md, admin-api.md, attempt-api.md, ai-evaluation.md
│   └── admin-portal/spec.md       # Dashboard, Score Profiles & Media Assets module
│
├── ui-ux/                         # [UI Reference] Design Tokens system & Figma screens
│   ├── README.md
│   ├── design-tokens.md           # Color palette, typography, and category theming for 3 test modules
│   └── screens-inventory.md       # Lookup table of 44 Figma screens by route & Frame ID
│
├── specs/                         # [Master Documents] System-wide master specification documents
│   ├── README.md
│   ├── prd-mvp.md                 # Comprehensive Product Requirements Document (PRD/SRS)
│   └── figma-screens-master.md    # Detailed interaction spec for 44 Figma screens
│
├── decisions/                     # [Layer 4] Architecture Decision Records (ADR)
│   └── 001-admin-lr-exam-revisions.md
│
└── runbooks/                      # [Layer 5] Operations, test scenarios & reports
    ├── README.md
    └── writing-uat-runbook.md     # UAT test scenarios and Writing module handoff checklist
```

---

## 2. AI Context Routing Guide

When asking AI to perform a task, **never load the entire `docs/` directory**.
Provide selective context according to the appropriate layer:

| Task Type | Docs for AI to Read First |
| :--- | :--- |
| **Start a new feature** | `features/<feature-name>/spec.md` + `contracts/api-contract.md` + `foundations/architecture.md` |
| **Write API or update contract** | `contracts/api-contract.md` + `foundations/feature-development.md` |
| **Fix architecture / Refactor** | `foundations/architecture.md` + relevant `decisions/` |
| **Testing / UAT** | `features/<feature-name>/tasks.md` + relevant `runbooks/` |
| **Cross-check Figma UI** | `ui-ux/screens-inventory.md` + `features/<feature-name>/spec.md` |

---

## 3. Feature Development Process (Spec-Driven Workflow)

1. **Initialize:** Copy `docs/features/_template/` to `docs/features/<feature-name>/`.
2. **Draft Spec (`spec.md`):** Extract requirements from `specs/prd-mvp.md`, and clearly define **In-scope**, **Out-of-scope**, and **Acceptance Criteria**.
3. **Create Checklist (`tasks.md`):** List phases (Domain -> Application -> Infrastructure -> Api -> Tests).
4. **Implement with AI:** Have AI work through one task at a time, check `[x]` upon completion, and run verification tests before moving to the next task.
