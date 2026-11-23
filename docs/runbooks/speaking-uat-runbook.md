# Speaking UAT and Handover Checklist

This document locks the **Speaking** scope: 11 questions, 5 Parts, full tests and Part practice.
Writing and L&R are outside this checklist.

Rows marked **(Target)** only pass after implementing ADR-002, ADR-003, or [Test Builder](../features/speaking/admin-test-builder.md).
When run against the current version, those rows are expected to **fail** and serve as evidence for the [audit report](./speaking-audit-report.md).

## 1. Safe Setup

1. Create `.env` from `.env.example`. Do not put keys into source code, screenshots, or tickets.
2. Ensure PostgreSQL, Redis, and MinIO are running. Only run migrations on the shared database once the team approves.
3. For AI grading: enable Azure Speech and Gemini per [AI Evaluation](../features/speaking/ai-evaluation.md), and ensure the image has `ffmpeg`.
4. Use HTTPS or `localhost`. Plain HTTP pages usually cannot access the microphone.
5. Have an Admin account ready. Do not bypass auth via headers or query strings.
6. When testing Azure or Gemini, use only recordings that contain no PII.
7. Have at least 2 exercises for each of Part 1 and Part 2, and 1 exercise for each of Part 3, 4, and 5. Sample data uses the `SAMPLE-` prefix. Images for Part 2 sample exercises might not exist in MinIO on another machine, in which case re-upload the images.

## 2. Learner Checklist

| Flow | Expected Result |
|---|---|
| Library | Shows only `PUBLISHED` tests. Part cards navigate to the exercise list. |
| Full test | Exactly 11 questions in `2/2/3/3/1` order. |
| Part practice | Contains only questions from that Part, renumbered starting from 1. |
| Timer | Matches timings in `spec.md`. Reloading does not add extra time. |
| Part 4 | 45s reading phase with questions hidden. Questions then reveal one by one. |
| Long absence | Close tab and reopen after time expires: attempt has already advanced or submitted. |
| Recording | Recording uploads after each question. Can be played back on review. |
| Upload failure | Turn off network during upload: shows error state and a retry button. |
| Consent | Submitting does not auto-send to AI; only sends after explicit consent. |
| AI | Polls every 5 seconds while processing. Leaving and returning keeps accurate state. Failures do not lose the attempt and show a retry grading button. |
| Results | Displays 0-200 estimated score label and per-question breakdown. |
| Ownership | Guests see only their session's attempts. Accounts see only their own attempts. Others receive `404`. |
| **(Target)** Beep | Plays a beep before recording starts. The beep is not captured in the recording. |
| **(Target)** Device check | All 3 steps present. A muted microphone cannot pass the test-recording step. |
| **(Target)** Skip phase | `FULL` tests have no skip button during reading and preparation phases. |

## 3. Microphone Permission Matrix

Run the entire matrix over HTTPS, **using an incognito/private window or after clearing site permissions**.
Fill in the actual prompt count in each cell.
This is the only way to confirm per-browser behavior, since public sources on Safari are outdated.

| Scenario | Chrome: Allow | Chrome: This time | Edge | Firefox | Safari macOS | Safari iOS |
|---|---|---|---|---|---|---|
| A. Grant permission, start, finish all 11 questions | | | | | | |
| B. Same as A, reload on Question 3 | | | | | | |
| C. Open a submitted attempt | | | | | | |
| D. Deny permission, try starting `FULL` | | | | | | |
| E. Deny permission, Part practice | | | | | | |
| F. No microphone connected | | | | | | |
| G. Microphone held by another app | | | | | | |
| H. Unplug microphone on Question 5 | | | | | | |

**Expected results after implementing ADR-003 (Target):**

| Scenario | Expected Result |
|---|---|
| A | Exactly **1** prompt, before the attempt is created. `getUserMedia` is called once. |
| B | No automatic prompt appears. If the browser forgot permission: banner warns that the timer is still running, with a re-enable button. |
| C | **0** prompts. No microphone indicator on the browser tab. |
| D | Start button is disabled. No `POST /attempts`. Instructions to open site settings are shown. |
| E | "Practice without recording" button is available. |
| F | Message says no microphone was found, without mentioning granting permission. |
| G | Message says the microphone is busy. |
| H | Immediate warning. Timer keeps running. Missing question recording is flagged on the review screen. |

How to count calls: paste this snippet into the Console before starting, then inspect `window.__gum` when finished.

```js
window.__gum = 0;
const original = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
navigator.mediaDevices.getUserMedia = (constraints) => {
  window.__gum += 1;
  console.log("getUserMedia", window.__gum, location.pathname);
  return original(constraints);
};
```

This snippet does not persist across page reloads, so in Scenario B it must be pasted again after reload.

Also record in the matrix: browser version and OS.
Do not assume results on one version hold for every version.

## 4. Admin/API Checklist

| Flow | Expected Result |
|---|---|
| Exercise bank | Full CRUD across all 5 Parts. Field-level errors display properly. Images validate format and size. |
| Draft test | Saves even with missing slots. Learners cannot see draft tests. |
| Publish | Only passes with `2/2/1/1/1` slots filled and every exercise `PUBLISHED`. |
| Snapshot | Editing an exercise after publish does not alter existing tests or attempts. |
| Concurrency | Two tabs editing the same item: the second receives `409`. |
| Duplicate ID | Same exercise in two slots is rejected. |
| Security | Non-Admin callers are rejected. Learner API does not expose rubrics or sample answers during an active attempt. |
| **(Target)** In-exercise duplicate | Part 3 with 2 identical questions differing only in case: `400`. |
| **(Target)** Cross-exercise duplicate | 2 Part 1 exercises with different codes and the same passage: `422`, naming Question 1 and Question 2. |
| **(Target)** Cross-Part duplicate | Part 3 question matching a Part 4 question: `422`. |
| **(Target)** Duplicate image | Uploading the same image twice for 2 Part 2 exercises: `422`. |
| **(Target)** Near-duplicate | Warning only, still publishable after confirmation. |
| **(Target)** Picker | Can search exercises beyond the latest 100. Already-used exercises are disabled and state which slot uses them. |
| **(Target)** Slots | Fill Slot 2, leave Slot 1 empty, save and reopen: slots stay in exact positions. |
| **(Target)** No data loss | Closing with unsaved edits asks for confirmation. Saving a draft does not close the builder. |
| **(Target)** Clone | Published test can be cloned into a draft. Original test is unchanged. |

## 5. Existing Data Audit

Run before enabling ADR-002 to identify exercises that need cleanup.
These queries are **read-only**, yet still require team approval if run on a shared database.
Normalization here approximates the rules in the ADR.

```sql
-- Part 3 and 4 exercises with duplicate questions
SELECT e.code, e.part, q.normalized, count(*) AS occurrences
FROM speaking_exercises e
CROSS JOIN LATERAL (
  SELECT trim(regexp_replace(lower(value), '[^[:alnum:]]+', ' ', 'g')) AS normalized
  FROM jsonb_array_elements_text(e.content_json -> 'questions')
) q
WHERE e.part IN (3, 4)
GROUP BY e.code, e.part, q.normalized
HAVING count(*) > 1;

-- Different exercises with the same passage or prompt
SELECT e.part, array_agg(e.code) AS codes
FROM speaking_exercises e
WHERE e.part IN (1, 5) AND e.status <> 'ARCHIVED'
GROUP BY e.part, trim(regexp_replace(
  lower(coalesce(e.content_json ->> 'passage', e.content_json ->> 'prompt')),
  '[^[:alnum:]]+', ' ', 'g'))
HAVING count(*) > 1;

-- Identical images currently used by Part 2 exercises
SELECT a.sha256, array_agg(e.code) AS codes
FROM speaking_exercises e
JOIN speaking_media_assets a ON a.id = e.image_asset_id
WHERE e.status <> 'ARCHIVED'
GROUP BY a.sha256
HAVING count(*) > 1;
```

The JSON keys `questions`, `passage`, and `prompt` follow `SpeakingJson` camelCase naming.
These queries have **not yet been run** on PostgreSQL, so test them on a local database first.

## 6. Go/No-Go Decision

Consider done only when: all non-(Target) rows pass; all implemented (Target) rows pass; the microphone matrix is filled for at least Chrome, Edge, and one Safari browser; and one non-PII test attempt returns valid scores from Azure and Gemini.

Accepted limitations:

- Cannot guarantee zero permission re-prompts after page reload, as permission lifecycle belongs to the browser.
- The browser microphone indicator stays lit throughout the attempt, including preparation phases. No audio is recorded outside response phases.
- AI scores are not ETS scores. Weights have not been calibrated.
- Only exact and lexical near-duplicates are detected. Paraphrases with identical meaning or edited images are not detected.
