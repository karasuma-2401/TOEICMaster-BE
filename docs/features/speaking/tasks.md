# Execution Checklist: TOEIC Speaking Module

> **Specification:** [spec.md](./spec.md)  
> **Status:** CNPMCS-51 ACs implemented on 2026-11-21. Device UAT, real data, and calibration remain open; see [Sprint 3 report](../../runbooks/sprint3-review.md).

## Execution Order

Follow the batches below, not alphabetical order.
Rationale is in [report section 0](../../runbooks/speaking-audit-report.md#0-re-verification-results-2026-11-03).

| Batch | Tasks | Belongs To |
| :--- | :--- | :--- |
| 1 | UI-02, UI-03, UI-04, UI-07; MIC-06, MIC-11; DUP-04; distinct 3-question rule (DUP-01) | C, B, A |
| 2 | Microphone session: B "Priority 1", add re-enable mic button, measure browser matrix | B |
| 3 | Server-side duplicate blocking, fix sample seed set, audit data. **Do not** implement `publish-check` | A |
| 4 | Remaining items in B "Priority 2" | B |
| 5 | Remaining items in C, attempt history links (FLOW-02) | C |
| Parallel | Move `ffmpeg` and environment variables into shared files (OPS-01) | Phase 6 |
| Deferred | `publish-check`, near-duplicate warnings, `usedInTestCount`, D | A, C, D |

---

## Execution Principles for AI
1. Only edit Speaking code: on BE, `Speaking*` files; on FE, `src/features/speaking` and `src/app/speaking`. Touchpoints in shared files must be minimal.
2. Do not modify applied migrations. Do not run `database update` against Neon without approval. Do not add packages unless requested.
3. Implement in order `Domain` → `Application` → `Infrastructure` → `Api` → `Tests`, then FE. After each phase, run checks and mark `[x]`.
4. BE checks: the four commands in `AGENTS.md`. FE checks: `npm run check`.
5. Before starting Work Package A, B, or D, confirm that decisions D1–D5 in the [audit report](../../runbooks/speaking-audit-report.md#7-decisions-awaiting-feature-owner) have been approved.

---

## Completed

### Phase 1: Domain & Entities
- [x] `SpeakingExercise`, `SpeakingTest`, `SpeakingTestExercise`, `SpeakingMediaAsset`.
- [x] `SpeakingAttempt` with phase state machine; `SpeakingEvaluation`.
- [x] `SpeakingTiming` fixed to the ETS format.

### Phase 2: Application (CQRS & Business Logic)
- [x] Commands and queries for exercises, tests, attempts, recordings, and AI grading.
- [x] Publish requirements reporting per-field errors; snapshot on publish.
- [x] Scoring formula in `SpeakingEvaluationScoring`.

### Phase 3: Infrastructure (Persistence & External Services)
- [x] EF Core mappings and Speaking migrations.
- [x] `MinioSpeakingMediaStore`; worker to delete guest recordings.
- [x] Azure Speech, Gemini, audio conversion via `ffmpeg`, and grading worker.

### Phase 4: API Presentation
- [x] `AdminSpeakingController`, `SpeakingAttemptsController`, recording upload rate limits.
- [x] Individual exercise practice and `evaluation-availability`.

### Phase 5: Verification & Testing
- [x] Domain, Application, API, and AI provider tests (see `tests/TOEICMaster.Tests`).
- [ ] Smoke-test Azure Speech and Gemini with real keys. `notespeaking.md` notes this was not tested yet; needs re-verification.
- [ ] Complete the [UAT runbook](../../runbooks/speaking-uat-runbook.md).

### Phase 6: Handoff & Review
- [x] Add Speaking section to `contracts/api-contract.md` (shared file, requires team agreement).
- [ ] Move `ffmpeg` and Azure/Gemini variables from `docker-compose.override.yml` (untracked in git) into `Dockerfile`, `docker-compose.yml`, and `.env.example` (report OPS-01; shared files, requires team agreement).

---

## Work Package A: No Duplicate Content ([ADR-002](../../decisions/002-speaking-test-content-uniqueness.md))

**Domain:** no changes.

**Application**
- [x] Create `Speaking/Admin/SpeakingTestContentRules.cs`: normalize text, extract comparison units, return issue list with code, severity, and question positions.
- [x] `SpeakingExerciseInputValidator`: the 3 questions in Parts 3–4 must differ after normalization.
- [x] `PublishRequirements`: add `Content` key for blocking-level errors. Retrieve image `sha256` via `FindMediaAssetAsync`.
- [ ] (Deferred) Add `CheckSpeakingTestPublishQuery`, passing `CancellationToken` throughout.

**Infrastructure:** no migration needed in this phase.

**Api**
- [ ] (Deferred) `POST /api/v1/admin/speaking/tests/publish-check` in `AdminSpeakingController`, dispatched via `ISender.Send`.

**Tests**
- [x] Fix sample fixtures: `SpeakingSamples` includes a second passage and byte-distinct image; update `FullTestAsync`, `Admin_and_learner_contract_matches_the_frontend`, `Published_images_are_served_only_through_a_published_snapshot`. Note that `DetectImage` checks the PNG trailer, so modify bytes in the middle of the file rather than appending to the end.
- [x] Normalization: casing, punctuation, whitespace, Unicode.
- [x] Duplicates within an exercise; across two Part 1 exercises; across Part 3 and Part 4; same asset; different assets with the same `sha256`.
- [ ] Similarity around thresholds 0.79 and 0.8 differs only by warning; warnings do not block publishing.
- [ ] `publish-check` writes no data and accepts incomplete tests.
- [x] Already-published tests and existing attempts remain unchanged.

**Before enabling:** run the read-only query in [section 5 of UAT](../../runbooks/speaking-uat-runbook.md#5-existing-data-review).

---

## Work Package B: Microphone Session ([ADR-003](../../decisions/003-speaking-microphone-session.md))

FE changes only.

**Priority 1: fix permission prompt bugs**
- [x] Create `src/app/speaking/layout.tsx` and `src/features/speaking/mic/mic-session-provider.tsx`.
- [x] Move `getUserMedia`, `AudioContext`, and volume metering into the provider. Ensure `acquire` is idempotent, including under StrictMode.
- [x] `useVoiceRecorder` uses only the provider's stream.
- [x] `SpeakingAttemptRun`: remove the mount-time permission request effect. Do not touch the microphone when `SUBMITTED`. Call `release()` upon submission.
- [x] Release the stream when leaving the attempt flow.
- [x] `SpeakingSession`: call `POST /attempts` only when the microphone is ready.
- [x] Recover after reload based on `permissions.query`, wrapped in `try/catch`.
- [x] Attempt screen includes a re-enable microphone button when status is `idle`, along with a notice that the timer is still running.
- [ ] On a real browser: verify that the layout preserves the stream across route transitions, and that the final question's recording retains full data.

**Priority 2: match PRD**
- [x] Distinguish errors: `denied`, `no-device`, `busy`, `insecure`, `unsupported`, with specific guidance for each.
- [x] 3-step device check: grant permission, 5-second test recording, playback. Add microphone selector.
- [x] Mode-based policy (D1).
- [x] Beep sound using `OscillatorNode`; recording starts after the beep.
- [x] Detect `track.ended` and `recorder.onerror`; `beforeunload` warning during the response phase.
- [x] Update the copy in `speaking-test-overview.tsx:113` regarding recording storage.
- [x] Hide phase skip button in full tests (D2).

**Tests**
- [x] Mock `getUserMedia`: called exactly once across device check, attempt creation, and route transitions.
- [x] `SUBMITTED` attempts: 0 calls.
- [x] No `POST /attempts` sent without permission in `FULL` mode.
- [x] Each error type produces the expected state.
- [x] Leaving the flow stops all tracks.
- [ ] Measure on real browsers per the [browser matrix](../../runbooks/speaking-uat-runbook.md#3-microphone-permission-matrix).

---

## Work Package C: Test Builder ([design](./admin-test-builder.md))

**FE, no BE changes needed (do first)**
- [x] UI-02: preserve positions when saving and reloading.
- [x] UI-01, DUP-04: picker with server-side search; lock already-used exercises and show which question uses them.
- [x] UI-03, UI-07: display full errors with accurate titles.
- [x] UI-05, UI-06: confirm on close; saving a draft does not close the builder.
- [ ] UI-10, UI-11, UI-13.
- [ ] FLOW-02: call `GET /attempts` and add an attempt list linking to `/speaking/attempts/{id}`. The `/history` page does not belong to Speaking, so place it on the Speaking page unless the team agrees otherwise.

**Needs BE**
- [ ] UI-08: error banner and 11-question map driven by `publish-check` (requires Work Package A).
- [x] UI-04: add `code` to `409` ProblemDetails.
- [x] UI-09: `DELETE /tests/{id}?version=`.
- [x] UI-09: `POST /tests/{id}/clone`.
- [ ] UI-12, DUP-05: add `usedInTestCount`.

**Tests:** the scenarios in [admin-test-builder.md](./admin-test-builder.md#5-acceptance-criteria).

---

## Work Package D: Deferred, Needs Decision

- [ ] Unpublish a published test: requires a new migration for `ck_speaking_tests_status` (D4).
- [ ] Question audio for Parts 3–4 (D5).
- [ ] `content_fingerprint` column for bank-wide duplicate warnings.

---

## Handoff

- [x] Update `admin-api.md` and `attempt-api.md`: move proposed sections to implemented sections.
- [x] Update ADR-002 and ADR-003 statuses when approved or implemented.
- [ ] Mark resolved items in the audit report.
- [ ] Report shared files that need updating (report section 8).
