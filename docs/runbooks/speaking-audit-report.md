# Speaking Module Audit Report

> **Update 2026-11-21:** This report preserves historical evidence from 2026-11-03.
> ADR-002 Step 1, the microphone session, device check/beep, and the Test Builder ACs have been fixed; current results and remaining open UAT/calibration items are in the [Sprint 3 review](./sprint3-review.md).

> **Review date:** 2026-11-03  
> **Reviewed revisions:** BE `feature/speaking-evaluation` @ `d9a9e76`; FE `feature/speaking-part-practice` (working tree)  
> **Scope:** Speaking only. No code changes. Writing and L&R were read only for comparison.  
> **Method:** Read code and tests, cross-referenced against the PRD, Figma, and external sources; then reproduced via automated tests and prototyped solutions on a separate copy (Section 0). Not yet run in real browsers, so per-browser behavior still needs to be measured per the [UAT runbook](./speaking-uat-runbook.md).

Paths under `src/TOEICMaster.*` belong to the BE repo.
Paths prefixed with `FE:` belong to the `TOEICMaster-FE` repo, relative to `src/features/speaking/` unless noted otherwise.

---

## 0. Re-verification Results (2026-11-03)

BE and FE code are unchanged from the initial review.
Reproduction tests and prototypes ran on a copy outside the repo, so the repo has no code changes.

### Verification Coverage

| Finding | Verification Method | Result |
| :--- | :--- | :--- |
| DUP-01, DUP-02 (including across Parts), DUP-03 (same asset, and different asset with same hash) | BE test via real handler | Confirmed: all can be saved or published |
| MIC-01, MIC-02 | FE test: run from check screen to attempt screen | Confirmed: call order is `getUserMedia -> POST /attempts -> getUserMedia`, first stream is stopped |
| MIC-03 | FE test | Confirmed, with adjustments below |
| MIC-04, MIC-06 | FE test | Confirmed, with adjustments below |
| UI-01, UI-02, UI-03, UI-04, UI-05, UI-07, DUP-04 | FE test | Confirmed |
| MIC-05, MIC-07, MIC-08, MIC-10, MIC-11, FLOW-01, UI-06, UI-08 to UI-13, DOC, OPS-01 | Code reading | Confirmed in code, not yet run live |
| MIC-09; whether the permission prompt appears a second time | Requires real browser | **Unverified** |

The original test suite remains green on the copy: BE 286 tests, FE Speaking 53 tests.

### Adjustments from Initial Review

| ID | Initial | Adjusted | Reason |
| :--- | :--- | :--- | :--- |
| MIC-04 | High severity bug | **Product decision, Medium severity** | This behavior is intentional: there is a test `lets learners practise on timers when the microphone is denied`. It only diverges from the PRD on full tests. |
| MIC-03 | "Reopening a submitted attempt" | **Primary case is microphone staying active after submission** | This happens on every completed attempt. Reopening only occurs via page reload or direct URL, because FE has no back/history link (FLOW-02). |
| MIC-02 | High | **High, conditional** | The wrong order is definite, but it only costs timer time when the prompt reappears. With permanently granted permissions, no prompt usually appears. |
| UI-01 | High | **Medium, becomes High once there are >100 `PUBLISHED` exercises** | `notespeaking.md` notes 11 sample exercises, so the limit has not been hit yet. The number 270 is only a Figma placeholder. Actual exercise count on Neon has not been queried. |
| ADR-002 | Impact on tests not stated | **Breaks 22 tests if sample fixtures are not updated** | Sample fixtures reuse one passage and one image for both slots. |
| FLOW-02 | Not listed | **New finding, Medium severity** | See Section 3. |

### Prototypes Built

Run on a separate copy, not yet committed to the repo:

| Solution | Actual Scope | Result |
| :--- | :--- | :--- |
| Duplicate blocking in BE (no migration, no new API) | 1 new 61-line file, ~10 lines added across 2 files | Correctly blocked all 5 duplicate scenarios. Initially 22 tests failed. After updating sample fixtures and the `FullTestAsync` helper, 2 tests remained failing and needed individual fixes. |
| Single microphone session via layout and provider | 1 provider (106 lines), 1 layout, 3 files edited | `getUserMedia` called exactly once, before `POST /attempts`. Submitted attempt: 0 calls. Stream stopped on submit and on leaving the flow. FE Speaking passed, typecheck clean. |
| Preserve slot positions (UI-02) | ~6 lines across 3 files | Passed. Server already accepted draft tests with only slot 2 filled. |

What the microphone prototype still lacks: a button to re-enable the microphone on the attempt screen when the stream is missing (the current banner only handles `denied` and `unsupported`).
Keeping the stream alive in the layout across route transitions was only simulated in tests, not yet run on real Next.js.

### Recommended Execution Order

Ranked by impact versus effort.
Details are in [tasks.md](../features/speaking/tasks.md).

| Batch | Items | Changes |
| :--- | :--- | :--- |
| 1. Quick fixes | UI-02, UI-03, UI-04, UI-07, MIC-06, MIC-11, DUP-04, DUP-01 | FE, 1 BE validator rule |
| 2. Microphone | MIC-01, MIC-02, MIC-03, then measure in browsers | FE only |
| 3. Duplicate blocking | DUP-02, DUP-03, update test fixtures, audit existing data | BE, no migration |
| 4. PRD alignment | MIC-05, MIC-07, MIC-08, MIC-10, D1, D2 | FE only |
| 5. Builder | UI-01, UI-05, UI-06, UI-08, UI-10 to UI-13, FLOW-02, clone test | FE, a few small endpoints |
| Parallel | OPS-01 | Shared files, needs team alignment |
| Later | `publish-check`, near-duplicate warning, count of tests using an exercise, unpublish test | Implement when actually needed |

**Change from initial proposal:** `publish-check` is no longer a prerequisite.
`POST /publish` already returns full errors by key, so adding server rules and fixing UI-03 is enough to block duplicates and display all errors.
A new endpoint is only needed if errors must be shown before clicking publish or if non-blocking warnings are added.

---

## 1. Conclusions

| Assigned Question | Conclusion | Solution |
| :--- | :--- | :--- |
| Questions in a test must not be duplicated | The system only blocks duplicate **exercise IDs**. Duplicate content can still be published. | [ADR-002](../decisions/002-speaking-test-content-uniqueness.md) |
| Request microphone permission only once | Each attempt flow calls `getUserMedia` **twice**. The second call runs after the server timer has already started. The microphone stays active after submission, and reloading the review screen also requests permission. | [ADR-003](../decisions/003-speaking-microphone-session.md) |
| Test builder UI gaps | There are 13 issues. Currently the most severe is selected exercises shifting slots on save. The 100-exercise limit has not been hit with sample data. | [Test Builder Design](../features/speaking/admin-test-builder.md) |

Severity levels in the tables below reflect the adjustments in Section 0:

- **High:** broken business logic, data loss, or blocks a primary flow.
- **Medium:** causes confusion or extra manual effort, with a workaround available.
- **Low:** inconsistent, does not affect outcomes.

---

## 2. Duplicate Content in Tests (DUP)

| ID | Severity | Current State | Evidence | Impact |
| :--- | :--- | :--- | :--- | :--- |
| DUP-01 | High | All 3 questions inside a Part 3 or Part 4 exercise can be identical. The validator only checks question count (3) and length. | `Application/Speaking/Admin/SpeakingAdminContracts.cs:131`; FE: `admin-draft.ts:125` | Questions 5-7 or 8-10 can be the exact same question. |
| DUP-02 | High | Every check layer compares `ExerciseId`, not content. Two exercises with different codes and the same passage can be published together. | `SpeakingAdminContracts.cs:167`; `SpeakingTestCommands.cs:106`; `Infrastructure/Persistence/Configurations/SpeakingConfiguration.cs:139` | Question 1 and Question 2 can read the same passage. |
| DUP-03 | Medium | Uploading the same image again creates a new asset. `sha256` is already stored but never compared. | `SpeakingExerciseCommands.cs:95` | Question 3 and Question 4 can use the same image. |
| DUP-04 | Medium | The slot picker lists every exercise in the Part, including ones already used in another slot. The checklist only shows a generic message without naming which slots collide. | FE: `components/admin/test-slot-picker.tsx:74`; `publish-checklist.ts:46` | Admins only discover the duplicate after selecting and must hunt for it manually. |
| DUP-05 | Low | There is no visibility into how many tests currently use an exercise. | Admin DTO lacks this field | Two tests can become nearly identical unnoticed. Reuse across tests is valid, but visibility is needed. |

**What already works and should be kept:** the validator blocks duplicate `ExerciseId` and duplicate slot positions; there is a unique index `(test_id, exercise_id)`; publish re-validates everything; published tests use an immutable snapshot.

---

## 3. Microphone Permission and Recording (MIC)

| ID | Severity | Current State | Evidence | Impact |
| :--- | :--- | :--- | :--- | :--- |
| MIC-01 | High | The pre-check screen and the attempt screen each instantiate a separate `useVoiceRecorder`. Leaving the first screen stops the stream; the next screen calls `getUserMedia` again. `src/app/speaking/` has no `layout.tsx` to persist the stream. | FE: `components/session/speaking-session.tsx:80`; `components/session/speaking-attempt.tsx:31,48`; `hooks/use-voice-recorder.ts:127` | May trigger a second permission prompt. Code comment only says "usually", meaning it is not guaranteed. |
| MIC-02 | High, conditional | The attempt is created on the server **before** the second call. The domain enters Question 1 immediately on creation. | FE: `speaking-session.tsx` (`start`); `Domain/Entities/SpeakingAttempt.cs:106` | If the prompt reappears, the timer runs while the candidate clicks Allow. |
| MIC-03 | High | The stream is not stopped upon submission, so the microphone remains active throughout the review screen. That screen also requests the microphone on mount regardless of state. | FE: `speaking-attempt.tsx:46-50` | After every attempt, the mic stays on even when not recording. Reloading the review screen can show a permission prompt. |
| MIC-04 | Medium | The start button remains clickable when permission is denied or the browser is unsupported. This is intentional, guarded by a test. | FE: `components/session/mic-check-panel.tsx:56-57`, `mic-check-panel.test.tsx` | Reasonable for practice, but violates PRD 4.3.2.A and AC-SPEAK-01 for full tests. See D1. |
| MIC-05 | Medium | Missing test recording, volume meter check, playback, speaker check, and microphone selector. The level meter only runs while recording. | FE: `use-voice-recorder.ts`; `mic-check-panel.tsx` | Missing compared to PRD 4.3.2.A and Figma 20. A muted mic is still treated as ready. |
| MIC-06 | Medium | All errors collapse into `denied`. Non-HTTPS pages collapse into `unsupported`. | FE: `use-voice-recorder.ts:58` | Missing or busy microphone shows "grant permission" instructions, which is the wrong remediation. |
| MIC-07 | Medium | No handling for microphone unplugging mid-test: does not listen to `track.ended`, `MediaRecorder.onerror`, or `devicechange`. | FE: `use-voice-recorder.ts` | Candidates lose recordings without any notification. |
| MIC-08 | Medium | No beep sound. Searching for `beep`, `oscillator`, or beep-related terms in the module returns no results. | FE: `src/features/speaking` | Violates PRD 4.3.2.B and old spec. |
| MIC-09 | Low | `AudioContext` is created without a user gesture on the attempt screen and `resume()` is never called. | FE: `use-voice-recorder.ts:49` | Risk: the volume meter may stay frozen. Recording is unaffected because `MediaRecorder` uses the stream directly. Needs live measurement. |
| MIC-10 | Medium | Active recordings live only in memory. Reloading the page during the response phase loses that recording with no warning. | FE: `use-voice-recorder.ts`, `hooks/use-recording-uploads.ts` | Loses the answer, usually making that question ungradable. |
| MIC-11 | Low | The intro screen states "Recordings are only stored in the current tab", whereas recordings are uploaded to the server. Guest recordings are retained for 30 days. | FE: `components/speaking-test-overview.tsx:113`; `SpeakingRecordingRetentionWorker.cs:14` | Inaccurate personal data disclosure. |
| FLOW-01 | Medium | A skip button is present in every phase, including the preparation phase of full tests. `Next` ignores mode. | FE: `components/session/session-stage.tsx`; `SpeakingAttempt.cs:133` | Old spec forbade skipping the preparation phase. Needs decision, see Section 7. |
| FLOW-02 | Medium | FE never calls `GET /attempts`, and only `speaking-session.tsx` navigates to `/speaking/attempts/{id}`. Backend already provides a history API. | FE: `api.ts`; search for `speaking/attempts` in `src` | Once leaving the results screen, there is no in-app path back to view the attempt or AI score, even though grading jobs can take a while. Violates AC-HIST-01. |

---

## 4. Test Builder UI (UI)

Proposed solutions for each item are in [admin-test-builder.md](../features/speaking/admin-test-builder.md).

| ID | Severity | Current State | Evidence | Impact |
| :--- | :--- | :--- | :--- | :--- |
| UI-01 | Medium | Builder only loads the latest 100 `PUBLISHED` exercises across **all** Parts, with no search. | FE: `components/admin/speaking-test-workspace.tsx:95` | Once >100 exercises exist, older ones cannot be selected (becomes High). Figma shows a 270-question bank with 85 exercises in Part 1 alone. Sample data has not reached this yet. |
| UI-02 | High | Slots collapse on save: `compact()` drops empty slots, `sequenceNumber` is assigned by compacted index, and the parser reloads via `push`. | FE: `speaking-test-workspace.tsx:46`; `admin-api.ts:85-88`; `parsers.ts:208-212` | Selecting "Slot 2" and leaving "Slot 1" empty causes the exercise to jump to "Slot 1" after saving. |
| UI-03 | Medium | Only the first error per field is kept, while `Exercises` can return multiple errors. | FE: `http.ts:24-25` | Admins must fix and republish repeatedly to see all errors. |
| UI-04 | Medium | All `409` errors display a generic message, discarding the exact server message. That message also tells the user to close and reopen. | FE: `speaking-test-workspace.tsx:53` | Cannot distinguish duplicate code from version conflict. Following the prompt loses unsaved edits. |
| UI-05 | Medium | "Cancel" and the close button exit immediately without asking when there are unsaved changes. | FE: `speaking-test-workspace.tsx:188,193` | Loses selections. |
| UI-06 | Medium | "Save draft" closes the builder. | FE: `components/admin/admin-speaking-tests-page.tsx` (`onSaved`) | Continuing work requires reopening and reloading. |
| UI-07 | Low | Errors when saving a draft still show the heading "Cannot publish yet". | FE: `components/admin/publish-checklist-panel.tsx:58` | Misleading error category. |
| UI-08 | Medium | Green `2/2` badge only counts filled slots, while the checklist can fail because an exercise left `PUBLISHED` status. No reason or link to the broken slot is shown. | FE: `test-slot-picker.tsx:38`; `publish-checklist.ts` | Contradictory signals. L&R Figma 43 has a clickable banner that scrolls to the error. |
| UI-09 | Medium | Published tests cannot be unpublished or cloned. Draft tests cannot be deleted. | `AdminSpeakingController.cs` lacks these endpoints | Broken tests must be recreated from scratch and stay visible to learners. L&R already has a fork mechanism ([ADR-001](../decisions/001-admin-lr-exam-revisions.md)). |
| UI-10 | Low | Mixed units: header says `2/2/3/3/1 QUESTIONS`, while slot pickers count exercises `2/2/1/1/1`. | FE: `speaking-test-workspace.tsx`, `test-slot-picker.tsx` | Easy to mistake Part 3 as needing 3 exercises. |
| UI-11 | Medium | Part 4 table uses `label \| content` syntax. Server limits are not validated upfront. The form shows only one error at a time. | FE: `admin-draft.ts:36-48,110-181`; `SpeakingAdminContracts.cs` | Easy to mistype, errors only surface after submit. |
| UI-12 | Medium | Exercise form allows changing status without warning which draft tests reference that exercise. | FE: `components/admin/speaking-exercise-editor.tsx` | A draft test can silently lose publish eligibility. |
| UI-13 | Low | Preview only shows one exercise at a time, not the full 11-question test sequence. Parts without exercises have no shortcut to create one. | FE: `components/admin/test-preview-panel.tsx` | Hard to review the entire test before publishing. |

---

## 5. Documentation Drift (DOC)

| ID | Current State | Resolution |
| :--- | :--- | :--- |
| DOC-01 | Old `spec.md` listed only Gemini for grading, while actual code uses Azure Speech and Gemini. | Fixed in `spec.md` and `ai-evaluation.md`. |
| DOC-02 | Old `spec.md` had a broken link to `../../notespeaking.md`, and the Figma mapping cited screen 19 (L&R) instead of 23. | Fixed in `spec.md`. |
| DOC-03 | `contracts/api-contract.md` still lacks a Speaking section. FE handoff (Oct 23) is gitignored and predates Part practice. | Temporarily use `admin-api.md` and `attempt-api.md`. See proposal in Section 8. |
| DOC-04 | PRD 5.5.1 requested per-question timer configuration, question audio upload, and sample audio. Code hardcodes timings (`SpeakingTiming`) with no audio; Part 3-4 use browser TTS button. | Keep fixed timings because they match ETS format. Audio remains an open decision, see Section 7. |
| DOC-05 | Figma uses `/exams/speaking/...` and `/admin/exams/speaking`, while code uses `/speaking/...` and `/admin/tests/speaking`. | Added mapping table in `spec.md`. Do not change routes. |
| DOC-06 | Docs say "~20 minutes", while total reading, prep, and response time in code is 618 seconds (~10.3 minutes). UI displays this figure too. | Both numbers are valid under different definitions: ETS's 20 minutes covers the whole exam session. Needs explanation in UI. |
| OPS-01 | `ffmpeg` and `AzureSpeech__*`, `Gemini__*` env vars exist only in `docker-compose.override.yml`. That file is excluded from git via `.git/info/exclude`. `Dockerfile`, `docker-compose.yml`, `docker-compose.shared.yml`, and `.env.example` all lack them. | AI grading works on Jin's machine, but other machines or deployments from the repo cannot grade. Needs moving into shared config files, see Section 8. |

---

## 6. References Used

**Internal**

| Source | Takeaways |
| :--- | :--- |
| Writing (`WritingTestCommands.cs:28-31`, `admin-writing-pages.tsx:940-957`) | Same ID-based blocking and non-excluding slot picker. Therefore not a pattern to copy for this issue. No Writing changes proposed in this scope. |
| L&R ([ADR-001](../decisions/001-admin-lr-exam-revisions.md), Figma 43) | Fork when editing published tests; error banner linking to slot; slot-indexed table. |

**External**

| Source | Relevant Content | Reliability |
| :--- | :--- | :--- |
| [ETS: TOEIC Speaking & Writing](https://www.ets.org/toeic/about/speaking-writing.html) | 11 questions, ~20 minutes, 0-200 scale, per-task timings. Matches `SpeakingTiming`. | Official |
| [Azure: REST for short audio](https://docs.azure.cn/en-us/ai-services/speech-service/rest-speech-to-text-short) | Pronunciation assessment 30-second limit, reason for segment splitting. | Vendor docs (azure.cn mirror) |
| [MDN: getUserMedia](https://developer.mozilla.org/docs/Web/API/MediaDevices/getUserMedia) | Secure context; meanings of `NotAllowedError`, `NotFoundError`, `NotReadableError`. | Reference standard |
| [MDN: Permissions.query](https://developer.mozilla.org/docs/Web/API/Permissions/query) | Promise rejects when unsupported, so `try/catch` is required. | Reference standard; browser support table needs re-checking |
| [Chrome: One-time permissions](https://developer.chrome.com/blog/one-time-permissions) | Choosing "Allow this time" re-prompts next time; permission can expire back to `prompt`. | Official |
| [Chrome: Permissions request chip](https://developer.chrome.com/blog/permissions-chip) | Sites must choose the right timing and context to request permissions. | Official |
| [W3C webrtc logs (2019)](https://lists.w3.org/Archives/Public/public-webrtc-logs/2019Oct/0176.html) | Safari prompts per document and can expire permissions. | **Dated, needs re-measuring** |
| [Apple Forums (2025)](https://developer.apple.com/forums/thread/788518) | One user reported iOS re-prompting on SPA route changes. | **Single report, unconfirmed** |
| [Cambridge: Speaking Test Setup](https://auto-metrica.cambridgeenglish.org/metrica/systemTest.aspx) | Speak into mic for 5 seconds then confirm volume on playback. | Exam board page |
| [Talview: Precheck](https://certification.talview.com/support/solutions/articles/11000130937-complete-guide-of-precheck-process-for-online-exams), [Prometric FAQ](https://www.prometric.com/files/cbla/Speaking-Platform-FAQs-for-Candidates.pdf) | Pre-session system check is standard practice. | Exam delivery vendors |
| [Moodle forum](https://moodle.org/mod/forum/discuss.php?d=251833), [Moodle docs 2.6](https://docs.moodle.org/26/en/Building_Quiz) | Moodle disallows adding the same question twice, yet two **copies** of the same question still slip through. This is exactly DUP-02. | Old docs, principle still holds |
| [ASC: Enemy items](https://assess.com/enemy-items/), [FastTest](https://www.fasttestweb.com/manual/enemy-items/) | "Enemy items": two items that must not appear together in one test even when worded differently. | Testing software vendors |

**Could not verify:** no official documentation was found for microphone check rules in TOEFL Home Edition, Duolingo English Test, or Pearson OnVUE, so this report does not rely on those systems.

---

## 7. Decisions Pending Feature Owner

Docs are currently written per the recommended options.
If different options are chosen, the ADRs and `spec.md` must be updated.

| # | Question | Recommendation | Rationale |
| :--- | :--- | :--- | :--- |
| D1 | What to do when microphone permission is missing? | Full test: block. Part practice: allow explicitly choosing "Practice without recording". | Meets AC-SPEAK-01 while preserving timed practice. |
| D2 | Allow skipping phases? | Full test: remove skip button in reading and preparation phases. Part practice: keep it. | Closer to exam conditions while keeping practice flexible. |
| D3 | Near-duplicate content warning threshold? | Jaccard >= 0.8, warn only, do not block. | Starting point only; needs calibration against real data. |
| D4 | What to do when a published test is broken? | Build "clone as draft" first. Defer "unpublish" since it requires a migration. | Cloning needs no schema changes. |
| D5 | Do Parts 3-4 need question audio? | Keep browser TTS for MVP, document as a PRD divergence. | Audio requires an upload flow and additional storage/data. |

---

## 8. Proposed Edits to Shared Files (Not Yet Applied)

These files are shared with the team so they were left untouched.
Each item needs only one or two lines:

- `docs/README.md`: add the new Speaking files, ADR-002, ADR-003, and the two runbooks to the directory tree.
- `docs/features/README.md`: update Speaking description to "AI grading via Azure Speech + Gemini".
- `docs/runbooks/README.md`: add links to the two Speaking runbooks.
- `docs/contracts/api-contract.md`: add a Speaking section pointing to `features/speaking/admin-api.md` and `attempt-api.md`.

Shared config files (OPS-01), also untouched:

- `Dockerfile`: install `ffmpeg` in the runtime stage. The install step is already in `docker-compose.override.yml:22`.
- `docker-compose.yml` and `.env.example`: add `AzureSpeech__*` and `Gemini__*` variables as in `docker-compose.override.yml:30-35`.
