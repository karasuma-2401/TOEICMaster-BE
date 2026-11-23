# Feature Specification: Admin Portal

> **Status:** Implemented / Production Ready  
> **Figma Mapping:** Screens 27, 28, 29, 30  
> **API Contract:** [contracts/api-contract.md](../../contracts/api-contract.md)

---

## 1. Goal & Scope

Provide the operations and resource management hub for the TOEICMaster system, restricted exclusively to accounts with the `Admin` role.

### Core Modules:
1. **Overview Dashboard:** Statistics on user counts, test attempts, test completion rates, and system health.
2. **Score Scale Management (Score Profiles):**
   - Create and store reusable ETS-standard score conversion tables.
   - Requires all 101 Listening milestones and 101 Reading milestones (from 0 to 100 correct answers, scaled scores from 5 to 495, in multiples of 5).
3. **Media Asset Management Library:**
   - Upload image files for questions and reading passages: only `.png`, `.jpg`, `.jpeg`, `.webp` formats accepted, max 5 MiB.
   - Validate file signatures (magic bytes); never rely solely on client-supplied file extensions or MIME headers.
   - Generate a random `assetId` (UUID) for tests to reference; clients cannot specify custom URLs or paths.
4. **Account & Role Administration:**
   - View paginated user lists and manage account statuses.

---

## 2. Technical & Security Guardrails

- All endpoints under `/api/v1/admin/*` strictly require an Authorization header containing a trusted JWT role claim `Admin` enforced via the `AccountAdministration` policy.
- Never accept roles from arbitrary request headers or query params.
