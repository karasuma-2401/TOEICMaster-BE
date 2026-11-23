# Writing UAT and Handover Checklist

This document locks the **Writing** scope: 5 Part 1 questions, 2 Part 2 questions, and 1 Part 3 question.
Speaking is outside this checklist.

## 1. Safe Setup

1. Create `.env` from `.env.example`; do not copy keys into source code, screenshots, or tickets.
2. Ensure PostgreSQL/Redis/MinIO are running and apply migrations after team approval:

   ```powershell
   dotnet ef database update --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api
   ```

3. Set `GEMINI_ENABLED=true` along with `GEMINI_API_KEY`, model, and timeout in `.env`. Do not, and never need to, put keys into the frontend `.env.local`.
4. Start the API and frontend per each repository's README. Use only non-PII sample tests/attempts during Gemini Free Tier UAT.
5. Seed an account with role `ADMIN` per the **Local Admin account seeding** section in the README, then sign out and sign back in so the JWT and session pick up the new role. Do not substitute with headers, query strings, or client-side bypasses.

## 2. User Checklist

| Flow | Expected Result |
|---|---|
| Library | Only `Published` tests appear; search, loading, empty, and error states have a retry button. |
| Start | Full mode has 8 questions in `5/2/1` order; Part mode has only that Part; pre-start screen displays both durations. |
| Timer | Part 1 transitions from 30s preparation to 60s writing; reloading or switching tabs does not add extra time. |
| Autosave | Type, wait for saved status; hide tab or reload right after typing and reopen: latest server answer is preserved. |
| Question lock | Next or timer expiration locks the question; cannot edit or go back. Last question auto-submits once. |
| Consent | Submitting does not auto-send to AI; only sends after checking the consent box. |
| AI | Queued/processing/retry polls every 5 seconds; can leave and return to the page. Key/quota errors do not lose the attempt and show a retry button. |
| Results | Shows 0-200 AI estimated score label, not ETS; feedback only appears after `COMPLETED`; internal sample answers are never shown. |
| Practice | Select an individual published question by Part, write untimed, submit and consent to grading; results show the raw Part score. |
| Part 1 image | Image is sent to AI after consent; verify feedback references the image. Total image files capped at 12 MiB per evaluation. |
| Ownership | Guests see only attempts from the current HttpOnly cookie; accounts see only their own attempts. |

## 3. Admin/API Checklist

| Flow | Expected Result |
|---|---|
| Question bank | CRUD for Parts 1/2/3, validation field errors, images enforce MIME/magic-byte/size validation. |
| Import | Excel preview reports exact row/column errors before committing. |
| Test | Drafts cannot be started by Users; publish only passes with `5/2/1` slots filled and snapshots do not alter old attempts. |
| Test lifecycle | Confirmation prompt before deleting a draft; archiving a published test hides it from Users while retaining attempts/images/history; restoring makes it visible again. Submitting a stale version returns `409`. |
| Review AI | `GET /api/v1/admin/writing/evaluations/{attemptId}` requires Admin policy; never returns raw provider response or API key. |
| Security | Attempt/history APIs return `Cache-Control: no-store`; responses include `X-Content-Type-Options`, `X-Frame-Options`, and `Referrer-Policy`. |

## 4. Go/No-Go Decision

Only enable Gemini for demos when all mandatory User checklist items pass, migrations are applied to the right database, and a non-PII calibration test returns valid JSON/scores.
The Free Tier is quota-limited and is not an ETS scorer; scores must always keep the **AI-estimated** label.

Accepted limitations:

- Browsers cannot reliably distinguish internal vs. external clipboard content, so blocking paste is avoided as it could cause Users to lose work.
- Page-lifecycle autosave is best-effort; the API/server timer remains the source of truth.
- Admin overrides with audit logging and self-service Admin role promotion are platform backlog items, not bypassed inside Writing.
