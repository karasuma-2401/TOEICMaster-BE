# Feature Specification: TOEIC Listening & Reading Module

> **Status:** The backend has implemented the learner attempt lifecycle, learner media delivery (presigned GET for block-level audio and images), and score-profile grading, while the frontend already has the BFF, catalog, exam room, autosave, submit, result, and review flows.
> A learner practice API from the exercise bank is still missing.
> **Figma Mapping:** Learner screens 12, 15-19; Admin 33-35, 40, 43, 44 (UI designs only, not an indication that the APIs are implemented).
> **Architecture Decision:** [decisions/001-admin-lr-exam-revisions.md](../../decisions/001-admin-lr-exam-revisions.md)
> **Related Docs:** [Admin API](./admin-api.md) · [Attempt API](./attempt-api.md) · [Scoring Profiles](./scoring.md) · [Checklist](./tasks.md) · [Shared API Contract](../../contracts/api-contract.md#toeic-listening--reading-exam-administration)

---

## 1. Goals & ETS Standard Exam Structure

The product goal is a TOEIC Listening & Reading practice exam with **200 questions** in **120 minutes** (Listening: 45 minutes, Reading: 75 minutes), with Listening/Reading scores from 5-495 and a total score from 10-990.
The backend has implemented **exam content administration**, **learner attempt lifecycle**, **server-side deadlines**, and **score-profile grading**; what remains missing is the learner practice API and AI evaluation (L&R does not use the AI flow).
Score profiles are provided by Admin; the system does not include a default ETS conversion table.

### Current Scope

- Separate exercise bank by Part 1-7: view summary, list items, create an item, and import XLSX by Part.
  This bank does not yet support auto-assembling exams.
- Admin creates a Draft exam with a fixed structure of 103 blocks/200 slots, edits questions and group contexts, selects a score profile, imports XLSX, publishes, and forks published revisions.
- Admin creates a `ScoreProfile` with Listening/Reading mappings; publishing only succeeds when each skill has all 101 conversion entries.
- Exam media uses a unified architecture: audio is attached per Listening Part 1-4 block and images are attached per Reading question/context, uploaded via presigned PUT.
  Legacy Master Audio is completely removed.
- Learners fetch audio/images for an attempt via presigned GET URLs; the Listening playlist returns all Part 1-4 blocks in a single call.

### Remaining Backend Gaps

- No learner API yet for practicing by Part from the exercise bank or assembling exams from the bank.
- No AI evaluation for L&R; L&R uses multiple-choice grading via score profiles rather than the Speaking/Writing AI flow.

### Structure of 103 Blocks & 200 Slots:
- **Part 1 (Photographs):** 6 single blocks (6 questions: 1-6, 4 options A-D).
- **Part 2 (Question - Response):** 25 single blocks (25 questions: 7-31, **only 3 options A-C**).
- **Part 3 (Short Conversations):** 13 group blocks (39 questions: 32-70, 3 questions per group).
- **Part 4 (Short Talks):** 10 group blocks (30 questions: 71-100, 3 questions per group).
- **Part 5 (Incomplete Sentences):** 30 single blocks (30 questions: 101-130, 4 options A-D).
- **Part 6 (Text Completion):** 4 group blocks (16 questions: 131-146, 4 questions per passage).
- **Part 7 (Reading Comprehension):** 15 group blocks, totaling 54 questions: 147-200; each block has 2-5 questions following a fixed skeleton.

---

## 2. Strict Technical Invariants

1. **Revision Immutability:**
   - The exam (`Exam`) manages identity; exam content is tied to `ExamRevision`.
   - Once a Revision is `Published`, it is **immutable (read-only)** to keep content stable when serving learner attempts.
     Publishing a new revision transitions the previous Published revision to `Superseded`.
   - To edit a published exam, Admin must create a new draft (`Draft revision`) via the Fork mechanism.
2. **Listening Media:**
   - Every Draft revision requires an audio asset for each Part 1-4 block before publishing.
     Legacy Master Audio is gone; audio is attached per block through the media workspace.
   - Learners receive presigned GET URLs for all Listening Part 1-4 blocks while the attempt is `InProgress` and Listening is not locked.
     After switching to Reading, both the playlist endpoint and the in-attempt audio endpoint return `409`; the backend does not enforce pause/seek restrictions on the player flow.
3. **Score Profiles:**
   - An exam can only be Published when linked to a Score Profile that has all **101 conversion entries** for Listening (0-100) and 101 conversion entries for Reading (0-100).
4. **All-or-Nothing XLSX Import:**
   - The Excel file contains 4 sheets: `Manifest`, `Blocks`, `ContextBlocks`, and `Questions`.
     An invalid workbook creates no Draft.
     Import only creates content; Listening audio and images are uploaded afterward via `media-upload-intents`, so no orphaned media objects are left behind if saving to the DB fails.
5. **Media Workspace for New Exams:**
   - Drafts created manually or imported with only a workbook use private object storage, uploading via presigned PUT per Listening audio block and Reading image.
   - Publish validates Part 1-4 audio and all images marked `ImageRequired=true`.
     Presigned URLs exist only in the upload response and are not persisted in the database.
   - `GET revision` returns `audioAssetId` and `audioDurationMs` on blocks and `imageRequired` on questions/contexts; flags such as `usesMediaWorkspace` and `hasMasterAudio` no longer exist.

   [ADR-001](../../decisions/001-admin-lr-exam-revisions.md) documents the original Master Audio decision and is kept for historical context; the current unified media architecture supersedes the Master Audio portion of that ADR.

6. **Publish Conditions:**
   - Every question must have a valid code, stem, options, and answer key; Part 2 only allows A-C, while other Parts allow A-D.
     Question codes must be unique within a revision.
   - Group blocks must have at least one context; every image with `imageRequired=true` must have an attached asset.
     Images are only validated when marked `imageRequired=true` in the current data.
   - Audio is required for every Listening Part 1-4 block.
   - The score profile linked to the revision must have all 101 entries for Listening and all 101 entries for Reading.

---

## 3. Acceptance Criteria

```gherkin
Scenario: Admin imports an exam via workbook
  Given a valid workbook with 103 blocks, 200 questions, and an existing ScoreProfileId
  When Admin calls POST /api/v1/admin/exams/import
  Then the system creates a Draft ExamRevision with 200 slots
  And Listening Part 1-4 audio files are uploaded afterward via media-upload-intents

Scenario: Admin publishes a workspace revision missing required media
  Given a Draft workspace revision missing audio for a Part 1-4 block or an image marked imageRequired
  When Admin calls POST /api/v1/admin/exams/{examId}/revisions/{revisionId}/publish
  Then the API returns 400 ValidationProblemDetails with key exam and keeps the revision as Draft

Scenario: Admin publishes a valid revision
  Given all questions and contexts are valid, the score profile is complete, and required media is attached
  When Admin calls POST /api/v1/admin/exams/{examId}/revisions/{revisionId}/publish
  Then the revision transitions to Published and the previous Published revision becomes Superseded

Scenario: Admin imports exercises into the exercise bank by Part
  Given an XLSX file matching the Part template with non-duplicate item codes
  When Admin calls POST /api/v1/admin/lr-exercise-bank/parts/{part}/imports
  Then the API returns the number of items and questions created along with the updated summary
```

### Learner Criteria Already Implemented in the Backend

```gherkin
Scenario: Candidate takes an L&R exam
  Given the candidate starts a valid attempt on a Published revision
  When 120 minutes elapse or the candidate submits the exam
  Then the system grades the attempt using the score profile linked to the revision
  And returns Listening 5-495, Reading 5-495, and total score 10-990

Scenario: Candidate loads the Listening audio playlist
  Given the attempt is InProgress and Listening is not locked
  When the candidate calls GET /api/v1/exam-attempts/{attemptId}/listening-audio-urls
  Then the API returns presigned GET URLs for all Listening Part 1-4 blocks
  And blocks are ordered by ascending Part and then ascending FirstQuestionNumber
```

Endpoints and payloads are documented in [Attempt API](./attempt-api.md).

## 4. Existing Frontend & BFF Flows

### Routes and Access Boundaries

| FE Screen | Role | Backend Called |
| --- | --- | --- |
| `/admin/exams` | Data Bank Center: view/create/import exams, view score profiles to select when creating exams, and manage the Part 1-7 exercise bank. | Via BFF routes `/api/admin/exams`, `/api/admin/score-profiles`, `/api/admin/lr-exercise-bank/*`. |
| `/admin/exams/reading/{examId}/{revisionId}` | Workspace for editing a single revision: questions, contexts, media, and publishing. | Via BFF routes `/api/admin/exams/{examId}/revisions/{revisionId}/*`. |

Next.js `/api/admin/*` routes are not a new public API: they enforce same-origin checks for mutations, read the session cookie, require the `ADMIN` role, forward the access token to BE `/api/v1/admin/*`, and return `Cache-Control: no-store`.
As a result, the browser never sends a JWT directly to the backend.

### Flow for Creating a New Exam or Importing a Workbook

```mermaid
sequenceDiagram
    autonumber
    actor Admin
    participant FE as Next.js UI/BFF
    participant API as BE Admin API
    participant Store as S3-compatible storage

    Admin->>FE: Create Draft or select XLSX workbook
    FE->>API: POST /api/v1/admin/exams or /import
    API-->>FE: ExamId, RevisionId, Draft 200 slots
    Admin->>FE: Open revision workspace
    FE->>API: GET revision; PUT question/context/score profile
    loop Each workspace audio or image
        FE->>API: POST media-upload-intents
        API-->>FE: presigned PUT URL, headers, expiresAtUtc
        FE->>Store: Direct PUT file with signed headers
        FE->>API: POST intent/complete
        API-->>FE: asset id attached to block/question/context
    end
    Admin->>FE: Publish
    FE->>API: POST publish
    API-->>FE: Published or ValidationProblemDetails
```

`DataBankCenter` imports an exam from a workbook; after creation/import succeeds, Admin navigates to the workspace to upload Listening audio and required images via the `media-upload-intents` flow.
There is no longer a `masterAudio` parameter or `/master-audio` endpoint in the media creation flow.

### Editing and Media Workspace

- Every Draft revision shares a single media workspace: a list of Part 1-4 audio slots along with image upload buttons on Reading questions/contexts.
  `GET revision` returns `audioAssetId`, `audioDurationMs`, and `imageRequired`, without the `usesMediaWorkspace` flag.
- The FE executes the exact three-step flow `intent -> direct PUT -> complete`.
  The PUT request uses `credentials: "omit"`; signed URLs and storage credentials are never stored in the FE or database.
- The FE only disables the publish button when it detects missing required media.
  The BE is always the final authority because state may change or requests may bypass the UI.
- After a `401` error, the client attempts a session refresh and retries the request; other HTTP errors are displayed from the BFF/BE `ProblemDetails`/`ValidationProblemDetails`.

### Exercise Bank on the Frontend

`DataBankCenter` allows viewing the summary, filtering/paginating by Part, manually creating an item, and importing XLSX.
The `audioUrl`/`imageUrl` fields on bank items are plain URL strings in the item data; they do not go through the revision media workspace.
The UI does not currently use bank items to auto-assemble exams.

### Implemented and Missing Learner Frontend Features

The FE already has the `/api/lr/*` BFF, Published exam catalog, instructions page, attempt room, autosave, switch to Reading, submit, result, and review.
The backend already provides presigned GET URLs for the Listening audio playlist and individual audio/images, so the remaining FE gaps are integrating a player that locks pause/seek, rendering images from asset IDs, displaying attempt history on the library, and Part 1-7 practice.
See the updated checklist in [tasks.md](./tasks.md).
