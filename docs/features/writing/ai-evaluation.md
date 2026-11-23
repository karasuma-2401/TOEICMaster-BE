# AI Evaluation for TOEIC Writing

CP6 evaluates Writing using a durable queue. Submissions and answers are saved first; provider errors, quota limits, or timeouts do not cause an attempt to be lost.

## Usage Flow

1. The user submits the attempt. The server creates an evaluation in the `CONSENT_REQUIRED` state.
2. The user reads the notice that data is sent to AI and clicks consent. Only then does the job transition to `QUEUED`.
3. The background worker picks up the job, calls Gemini on the server, and saves the validated JSON feedback.
4. The FE automatically refreshes the status every 5 seconds while the job is `QUEUED`, `PROCESSING`, or `RETRYING`.
5. When completed, the UI labels the test score as **AI-estimated TOEIC Writing score (0-200, step 10)**, not an official ETS score. Standalone exercises display raw scores per Part; model answers are not returned to learners.

A `FAILED` job can be resubmitted for grading by the User. The saved consent is reused; the retry uses the currently configured model and rubric. The browser never automatically sends a submission to the provider without initial consent.

To protect the Free Tier, a guest/anonymous session may only start one new AI evaluation per 24-hour window. Retries of the **same attempt** are not counted again; signed-in Users do not have quota enforced in this checkpoint.

An Admin with JWT role `Admin` can safely review an evaluation at `GET /api/v1/admin/writing/evaluations/{attemptId}`. The endpoint returns the attempt snapshot along with the saved status/model/rubric/feedback; it does not return the raw provider response or key. An override flow with audit logging is reserved for hardening and must not be used to silently alter the original AI score.

## Gemini Configuration

Do not commit API keys. Copy the variables below into your local `.env` or deployment environment secrets:

```dotenv
GEMINI_ENABLED=true
GEMINI_API_KEY=your_server_side_key
GEMINI_MODEL=gemini-3.5-flash-lite
GEMINI_REQUEST_TIMEOUT_SECONDS=45
GEMINI_RUBRIC_VERSION=toeic-writing-v2
GEMINI_USE_WRITING_REFERENCE_ANSWERS=false
```

`GEMINI_ENABLED=false` is the safe default. With this configuration, the UI and consent flow can still be tested, but jobs will transition to `FAILED` with a message that the service is not configured. The API key must never be returned to User APIs, logs, or the Next.js browser bundle.

Create the database schema (do not run this command against the shared database without team approval):

```powershell
dotnet ef database update --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api
```

The checkpoint migrations are `AddWritingEvaluations` and `AddWritingAttemptReviewSnapshots`; they add
`writing_evaluations`, `writing_evaluation_feedback`, immutable attempt review snapshots, and raw rubric scores.

## Rubric, Model Answers, and Score Conversion

The worker instructs Gemini to return a raw score for each question by Part: Part 1 is **0-3**, Part 2 is **0-4**,
Part 3 is **0-5**. The server validates question positions, score bounds, and the entire JSON before saving; invalid responses will not
be displayed as valid results.

Only `FULL` attempts with all 5/2/1 questions and non-practice mode save `EstimatedScore` 0-200, rounded to increments of 10:

```text
round((total raw score / maximum possible raw score of attempt) * 20) * 10
```

This is a transparent practice conversion, not a proprietary ETS conversion table. Optional model answers and rubrics are snapshotted
when creating the attempt; the learner API does not return these two pieces of data. By default, the AI only receives the rubric; only enable
`GEMINI_USE_WRITING_REFERENCE_ANSWERS` after the team has performed calibration. When enabled, the model answer is a reference, not the sole acceptable answer.
Change `GEMINI_RUBRIC_VERSION` whenever changing how reference answers are used so results can be distinguished across configurations.
Part 1 images are sent to Gemini after consent; total image data is capped at 12 MiB per evaluation, and exceeding the limit reports a safe error.

`PART_1`, `PART_2`, `PART_3`, and practice attempts save `EstimatedScore = null`. Per-question scores are still saved in
`writing_evaluation_feedback`; the API additionally derives `rawScore`, `maxRawScore`, and `scorePercentage`. The API also hides
0-200 scores of old partial attempts so they are not mixed with full tests; no backfill has been run on existing data.

Prompt v2 adds criteria per Part, accepts keyword variations, checks all email requirements, does not treat word count
as an automatic score threshold, and resists prompt-injection instructions embedded in submissions. The schema places per-question feedback before scores and summary;
this is an experimental output ordering change, not a guarantee of reasoning or accuracy. Blank answers are handled
as 0 points on the server without calling Gemini or downloading images. Feedback text exceeding the limit is truncated at sentence boundaries when available,
preserving Unicode surrogate pairs; out-of-bounds scores or missing/duplicate positions are still rejected and retried, without auto-correcting scores.

When using an older `.env`, set `GEMINI_RUBRIC_VERSION=toeic-writing-v2` to distinguish results from before the change.

The editor disables `spellcheck` and debounces autosave for **800ms** after the last keystroke. Browsers have no reliable API to know
for sure whether pasted content came from the editor itself or an external source, so paste-blocking mechanisms that could cause
data loss are not implemented; the server still persists/controls the attempt, timer, and ownership.

## Retries and Stored Data

- Timeout, network errors, HTTP 429, and HTTP 5xx: retry up to 3 times with backoff of 1, 3, and 9 minutes; if the provider sends a larger `Retry-After`, that value is respected.
- Key/configuration errors, rejected requests, or invalid JSON after retries: status `FAILED`, while the submission remains preserved.
- The worker uses a 2-minute lease and optimistic concurrency (`Version`) so a single job cannot be completed twice across multiple API instances.
- Only scores, normalized feedback, model/rubric versions, and safe error codes are saved. Raw provider responses and API keys are never stored.

## Calibration Before Official Demo

Gemini is a learning feedback tool, not an ETS scorer. Before enabling it for real Users, create a set of 30-50 submissions graded by instructors/trusted sources, covering all of Part 1/2/3 and various score levels. For each rubric/model version, record:

| Field | Example |
|---|---|
| `attemptId` or fixture id | `calibration-p2-07` |
| Part/mode | `PART_2` |
| Expected score | `140` |
| AI score | `150` |
| Difference | `+10` |
| Reviewer notes | `over-prioritized vocabulary over task completion` |

Only bump `GEMINI_RUBRIC_VERSION` after reviewing discrepancies; completed evaluation history retains the old model and rubric version for auditing.

The team has no instructor-graded reference submissions as of 2026-11-21, so there are no MAE/QWK metrics or accuracy conclusions yet.
Offline tools and the 3-runs-per-submission measurement procedure are in the [Sprint 3 report](../../runbooks/sprint3-review.md).

## UAT and Enabling Demo

See the [Writing UAT runbook](../../runbooks/writing-uat-runbook.md) before running migrations or sending a test submission to Gemini. The runbook
lists User/Admin flows, Free Tier safety conditions, and accepted limits.
