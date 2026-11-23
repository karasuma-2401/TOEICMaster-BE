# Notes: AI Grading for Speaking

Date: 2026-10-30 · BE branch: `feature/speaking-evaluation`, created from `dev` @ `171f307`. FE branch: `feature/speaking-part-practice`, created from `dev` @ `4416ec9`. Both are uncommitted and unpushed.

This document covers two parts: AI grading for Speaking (section 2) and part-by-part practice using individual exercises (section 7).

## 1. New Migration

Before starting, the branch was updated with the latest migration on `dev`. The previous latest migration was `20261028180220_AddLrExerciseBank`.

| Migration | Description |
| --- | --- |
| `20261029192259_AddSpeakingPracticeAndAiEvaluation` | Creates 2 tables `speaking_evaluations` and `speaking_evaluation_items`, and alters `speaking_attempts` for part-by-part practice. Does not touch tables of other modules. |

This migration replaces `20261029180213_AddSpeakingAiEvaluation` from the earlier version. That earlier migration had never been applied to Neon, so I removed and merged it so the team only needs to run a single migration.

**Changes on `speaking_attempts`:**

- `test_id` changed to nullable.
- Added column `exercise_id` (uuid, nullable), FK -> `speaking_exercises` with `RESTRICT`, indexed.
- Added CHECK `ck_speaking_attempts_source`: each attempt has exactly one source, either `test_id` or `exercise_id`.
- On rollback (`Down`), individual practice attempts (`test_id IS NULL`) are deleted first, then `test_id` is restored to required.

**`speaking_evaluations`**: each attempt has at most 1 grading job. Columns:

- Identity and status: `id`, `attempt_id` (UNIQUE, FK -> `speaking_attempts`, cascade delete), `status`.
- Provider and rubric: `speech_provider`, `speech_locale`, `language_provider`, `language_model`, `rubric_version`.
- Job timestamps: `consent_accepted_at_utc`, `queued_at_utc`, `started_at_utc`, `lease_expires_at_utc`, `next_attempt_at_utc`, `completed_at_utc`.
- Results: `retry_count`, `estimated_score`, `summary`, `strengths_json` and `improvements_json` (jsonb).
- Errors: `failure_code`, `failure_message`.
- System: `created_at_utc`, `updated_at_utc`, `version` (concurrency token).

`status` only accepts `QUEUED`, `PROCESSING`, `RETRYING`, `COMPLETED`, `FAILED`.

**`speaking_evaluation_items`**: primary key is `(evaluation_id, position)`, one row per question. Columns:

- Question info: `part`, `recognition_status` (`RECOGNIZED`, `NO_SPEECH`, `NO_RECORDING`).
- Azure results: `transcript`, `audio_seconds`, `accuracy_score`, `fluency_score`, `completeness_score`, `prosody_score`, `pronunciation_score`, and `word_issues_json` (jsonb, list of mispronounced words).
- LLM results: `grammar_score`, `vocabulary_score`, `relevance_score`, `feedback`, `suggested_response`.
- Question score: `raw_score`, `max_raw_score`.

There are CHECK constraints for all enums and score ranges, plus indexes on `(status, next_attempt_at_utc)` and `lease_expires_at_utc` for the worker to pick up jobs.

The migration ran Up -> Down -> Up successfully on a temporary PostgreSQL 16 instance, including when individual practice attempts already existed.

**Already applied to Neon** (2026-10-30) with your approval. Please inform the team: anyone pulling this branch **does not** need to run `database update` again. The backend Docker image has also been rebuilt from the current code.

## 2. Grading Flow

The grading pipeline follows the design guide: Azure Speech STT extracts the transcript, enables pronunciation assessment to collect pronunciation metrics, and then the LLM grades vocabulary and grammar.

1. **Learner gives consent.** The learner calls `POST /api/v1/speaking/attempts/{id}/evaluation-consent` with body `{ "accepted": true }`. This step is mandatory because voice recordings are sent to an external service, matching the Writing policy. The request creates a `QUEUED` job.
2. **Background worker processes each question that has a recording:**
   - Downloads the recording from MinIO.
   - Uses `ffmpeg` to convert to 16 kHz mono WAV PCM and splits into chunks <= 25 seconds.
   - Sends each chunk to Azure Speech REST (short audio) with the `Pronunciation-Assessment` header: `Dimension=Comprehensive`, `Granularity=Word`, and prosody enabled when using en-US.
   - Receives transcript, AccuracyScore, FluencyScore, ProsodyScore, PronScore, and the list of mispronounced words. Chunk scores are aggregated weighted by duration.
   - Part 1 questions also include completeness: the proportion of words in the passage read in the correct order.
   - Each question's results are persisted immediately, so retries do not call Azure again.
3. **Calls the LLM (Gemini) once for the entire test.** Inputs include the transcript, prompt, part-specific rubric, and pronunciation scores. The LLM returns grammar, vocabulary, and relevance (0-100), feedback in Vietnamese, suggested response, along with summary, strengths, and areas for improvement.
4. **Server computes the final score.** The LLM does not decide the final score.

**Why audio chunking and `ffmpeg` are required:**

- Azure's REST API only evaluates pronunciation for audio **<= 30 seconds**, whereas Part 1 allows 45 seconds and Part 5 allows 60 seconds.
- REST only accepts WAV/OGG, whereas Chrome records WebM and Safari records MP4.

**Scoring formula** (`SpeakingEvaluationScoring`), where P = Azure PronScore (already combining accuracy, fluency, and prosody):

| Part | Quality 0-100 | Max Score |
| --- | --- | --- |
| 1 (Read Aloud) | 0.7·P + 0.3·completeness | 3 |
| 2 (Describe a Picture) | 0.4·P + 0.2·grammar + 0.2·vocabulary + 0.2·relevance | 3 |
| 3, 4 | 0.3·P + 0.2·grammar + 0.2·vocabulary + 0.3·relevance | 3 |
| 5 (Express an Opinion) | 0.3·P + 0.2·grammar + 0.2·vocabulary + 0.3·relevance | 5 |

- **Question score** = round(quality / 100 × max score).
- **Off-topic response:** if relevance < 40, the question receives at most 1 point, regardless of fluency.
- **No recording or no speech recognized:** 0 points, and not sent to the LLM.
- **Estimated score** = sum of question scores / sum of max scores × 200, rounded to the nearest multiple of 10.

This is a practice score, not an official ETS score. The weights above are my proposal; the team can adjust them.

**Retry on error** (same as Writing):

- Transient errors (quota, network, 5xx, invalid LLM output) are retried up to 3 times, waiting 1, 3, and then 9 minutes.
- Configuration errors (invalid key, missing `ffmpeg`) transition directly to `FAILED`. The learner calls the consent endpoint again to re-queue.
- Anonymous guests are allowed 1 grading run every 24 hours.

## 3. API for Frontend

- **`POST /api/v1/speaking/attempts/{id}/evaluation-consent`**, body `{ "accepted": true }` -> returns `SpeakingAttemptDto`.
  - `400`: `accepted` is not `true`.
  - `404`: caller is not the owner of the attempt.
  - `409` when any of the following conditions occur:
    - attempt has not been submitted yet;
    - still within the 15-second grace period to upload the final recording;
    - attempt has no recordings;
    - backend AI grading is not configured;
    - anonymous guest has already used their grading quota in the past 24 hours.
  - Calling again when a job already exists returns the current job. If the job is `FAILED`, it re-queues the job.
- **`GET /api/v1/speaking/attempts/{id}`**: includes a new `evaluation` field, `null` until the learner accepts consent. FE polls this endpoint until `status` is `COMPLETED` or `FAILED`.
  - `evaluation` includes `status`, `estimatedScore`, `summary`, `strengths`, `improvements`, `items`, `speechProvider`, `languageProvider`, `languageModel`, `rubricVersion`, `consentAcceptedAtUtc`, `queuedAtUtc`, `completedAtUtc`, `retryCount`, `failureMessage`.
  - Each item in `items` (present only when `COMPLETED`) includes `position`, `part`, `recognitionStatus`, `transcript`, `accuracyScore`, `fluencyScore`, `completenessScore`, `prosodyScore`, `pronunciationScore`, `grammarScore`, `vocabularyScore`, `relevanceScore`, `rawScore`, `maxRawScore`, `feedback`, `suggestedResponse`, and `wordIssues` as `[{ word, accuracyScore }]`.
- **`GET /api/v1/speaking/attempts`**: each item now includes `evaluationStatus` and `estimatedScore`.

Existing responses only have fields added; no existing fields were modified. FE does not yet have a UI screen for this feature; I did not touch the FE repo.

## 4. Action Items

1. **Create an Azure Speech resource** (Azure Portal -> Speech service). Recommended region is `southeastasia`; the free F0 tier is sufficient for testing. Copy the **Key** and **Region**.
2. **Enable Gemini.** Speaking grading shares the `Gemini` key from Writing, so `Gemini__Enabled=true` and `Gemini__ApiKey` are required. If either Azure or Gemini is missing, the consent endpoint returns `409` ("not configured").
3. **Configure environment variables.** I **did not modify** the shared files `docker-compose.yml`, `.env.example`, and `appsettings.json`, so they must be added manually.
   - In `docker-compose.yml`, under `backend.environment`:
     ```yaml
     - AzureSpeech__Enabled=${AZURE_SPEECH_ENABLED:-false}
     - AzureSpeech__Key=${AZURE_SPEECH_KEY:-}
     - AzureSpeech__Region=${AZURE_SPEECH_REGION:-southeastasia}
     - Gemini__Enabled=${GEMINI_ENABLED:-false}
     - Gemini__ApiKey=${GEMINI_API_KEY:-}
     ```
     Note: compose also currently does not pass `GEMINI_*` variables, so Writing AI grading in Docker cannot be enabled yet either.
   - In `.env.example`, add `AZURE_SPEECH_ENABLED=false`, `AZURE_SPEECH_KEY=`, `AZURE_SPEECH_REGION=southeastasia`.
   - When running via `dotnet run`, you can use `dotnet user-secrets` or environment variables with the same names.
   - Other options have sensible defaults: `AzureSpeech__Locale` (en-US), `AzureSpeech__Endpoint`, `AzureSpeech__FfmpegPath` (ffmpeg), `AzureSpeech__ChunkSeconds` (25), `AzureSpeech__RequestTimeoutSeconds` (30).
4. **Install `ffmpeg` into the runtime image.** Because `Dockerfile` is shared, I have not edited it yet. Add to the `runtime` stage before `USER $APP_UID`:
   ```dockerfile
   USER root
   RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
   ```
   I tested this on the exact `mcr.microsoft.com/dotnet/aspnet:10.0` base image (Ubuntu 24.04, ffmpeg 6.1.1): it installs cleanly and converts WebM, Ogg, and MP4 audio properly. The image size increases by roughly a few dozen MB. If running `dotnet run` on a dev machine, install via `brew install ffmpeg`. Missing `ffmpeg` causes jobs to transition to `FAILED` with error `AUDIO_CONVERTER_UNAVAILABLE`.
5. ~~Apply migration to Neon~~: already done, see section 1.
6. **Test with a real key.** Azure documentation is inconsistent on whether `ReferenceText` is mandatory for unscripted grading. The code sends unscripted mode first. If Azure returns `400`, the code automatically transcribes speech first and then grades again using that transcript as the reference text. A test with a real recording is needed to confirm the returned scores are reasonable.
7. **Cost.** Each Full test makes roughly 11-16 Azure requests (due to chunking) and 1 Gemini call. Pronunciation assessment is billed at speech-to-text rates.

## 5. Scope of Changes

- **New files, all within Speaking:**
  - Domain: `SpeakingEvaluation.cs`.
  - Application: `Application/Speaking/Evaluations/` directory.
  - Infrastructure: `Infrastructure/Speaking/Evaluations/` directory, `SpeakingEvaluationConfiguration.cs`, `EfSpeakingEvaluationRepository.cs`, and the migration.
  - Tests: new test files.
- **Modified existing Speaking files:** `SpeakingAttemptsController`, `SpeakingAttemptContracts`, `SpeakingAttemptHandlers`, `SpeakingShared`, `SpeakingConfiguration`, and Speaking tests.
- **Minimal touchpoints in shared files:**
  - `DependencyInjection.cs`: 2 lines (`using` and `services.AddSpeakingEvaluation(configuration)`).
  - `MediatorPipelineTests.cs`: 3 lines registering Speaking fakes.
  - `ToeicDbContextModelSnapshot.cs`: auto-generated by EF, only adding 2 tables.
- Read but **did not modify** Writing's `GeminiOptions`. Did not touch the FE repo.

## 6. Testing

- `dotnet test`: 280/280 pass (previously 236 tests). Includes 44 new tests:
  - scoring formula and completeness for read-aloud;
  - consent: preconditions, anonymous guest limit, owner check;
  - processing an 11-question Full job with missing-recording and silent questions;
  - off-topic penalty enforcement;
  - retry without re-calling Azure;
  - permanent failure followed by re-queuing;
  - LLM output missing questions;
  - Azure request formatting (URL, headers, base64 JSON), duration-weighted chunk aggregation, parsing both response formats, fallback path on `400`, error classification;
  - Gemini request (key in header, not in URL);
  - full HTTP flow: consent -> background worker -> polling.
- **On real PostgreSQL 16** (temporary test, deleted after running):
  - Two workers racing for the same job: only one acquires it.
  - Duplicate job creation blocked by the UNIQUE index and returns `409`.
  - Expired lease allows another worker to take over.
  - Per-question results remain saved before LLM failure.
  - On retry, job completes without making another Azure call.
  - Deleting an attempt cascades to delete the job and its items.
- **Real `ffmpeg`** (temporary image from runtime base): 45s WebM -> 25 + 20s, 60s MP4 -> 25 + 25 + 10s, 10s Ogg -> 1 chunk. All chunks are 16 kHz mono WAV; corrupted files return empty output.
- Verified solution-wide formatting, Release build with 0 warnings, and `has-pending-model-changes` shows no remaining changes.
- **Not yet tested:** live Azure Speech and Gemini calls, as keys are not available yet.

## 7. Part-by-Part Practice

Previously, the "Practice by Part" card only reopened Full tests in single-part mode, and no cards were clickable when no published tests existed. Now every published exercise in the bank can be practiced standalone, without needing a full test.

**New API (BE):**

- `GET /api/v1/speaking/exercises?part=1..5&pageNumber&pageSize`: lists `PUBLISHED` exercises without rubric or sample responses. Each item contains `{ id, code, part, topic, difficulty, questionCount, durationSeconds, updatedAtUtc }`.
- `GET /api/v1/speaking/exercises/{id}`: returns 1 exercise as above, `404` if not published.
- `POST /api/v1/speaking/attempts` additionally accepts `{ "exerciseId": "..." }`. The attempt gets `mode = PART_n` matching the exercise's Part.
  - Exactly one of `testId` or `exerciseId` must be sent; otherwise returns `400`.
  - Exercise content is snapshotted at start time, so later admin edits do not affect an in-progress attempt.
- `SpeakingAttemptDto` and attempt list items include `exerciseId`, while `testId` may be `null`. For standalone practice attempts, `testCode` is the exercise code and `testTitle` follows `Practice Part n · <topic>`.
- Part 2 images of published exercises are served via `GET /api/v1/speaking/media/{id}`. Once archived, the image is no longer served unless it also belongs to a published test.

**FE:**

- New page `/speaking/parts/[1-5]`: lists exercises for the Part with code, topic, difficulty, question count, and duration. Invalid Part returns `404`.
- New page `/speaking/exercises/[id]/practice`: runs mic check then starts the practice attempt, reusing the existing exam room and review screens.
- Part cards on `/speaking` always route to `/speaking/parts/n`.
- Review screen: "Practice again" restarts the same exercise; "Choose another exercise" navigates back to the Part's list.
- BFF `/api/speaking/*` allows 2 additional `exercises` routes.

**Sample data on Neon** (created via admin API, all codes prefixed with `SAMPLE-`):

- 11 `PUBLISHED` exercises: `SAMPLE-P1-001..003`, `SAMPLE-P2-001..002`, `SAMPLE-P3-001..002`, `SAMPLE-P4-001..002`, `SAMPLE-P5-001..002`.
- Test `SAMPLE-FULL-01` "Sample TOEIC Speaking Test 01", complete with 11 questions, published.
- **Part 2 images live in MinIO on Jin's local machine**, while the DB records are shared. Other team members opening Part 2 exercises will not be able to load the images. To share them, each developer can re-upload the images via the admin page, or the team can switch to a shared MinIO bucket.
- To clean up: archive the `SAMPLE-` exercises in the admin UI. Published tests currently do not have an unpublish/archive API.

**Additional testing:**

- BE: 286/286 tests pass, including 6 new tests for Part-by-Part practice.
- On temporary PostgreSQL 16: Part-by-Part practice flow works properly, CHECK `ck_speaking_attempts_source` blocks attempts without a source, and rollback succeeds even with existing standalone practice attempts.
- FE: format, lint, typecheck clean; 221/221 tests pass.
- End-to-end run against the new backend and Neon DB through the FE BFF:
  - test list and exercises for each Part;
  - standalone Part 2 practice (JPEG image returns `200`) and Part 4 (questions hidden as expected);
  - complete 11-question Full test;
  - attempt history.
- Pages `/speaking`, `/speaking/parts/3`, `/speaking/exercises/{id}/practice` render properly. `/admin` redirects to the login page when not signed in as admin.

## 8. AI Grading Flow in the UI

Before this build, FE had no UI for Speaking AI grading: the review screen still stated "will be added in a later phase", and the BFF did not yet allow the `evaluation-consent` route. Now the flow works as follows:

1. The learner finishes and submits the test. The **review screen** (`/speaking/attempts/{id}`) includes an **"AI Grading"** panel below the summary stats.
2. The panel automatically calls `GET /api/speaking/evaluation-availability` (new BE endpoint returning `{ available }`).
   - **Not configured** (Azure Speech or Gemini missing): displays "AI grading for Speaking is not enabled on the server" with no action button.
   - **Configured:** displays a 3-step explanation covering Azure Speech transcribing and scoring pronunciation, Gemini grading grammar, vocabulary, and relevance, and the system computing an overall score out of 200. Accompanying this is a consent checkbox and **"Agree and Grade"** button. The button is disabled until the checkbox is checked, while recordings are still uploading, or if the attempt has no recordings.
3. Clicking the button calls `POST .../evaluation-consent`. The panel updates to **"Queued for grading"** or **"AI is listening and grading your speech"**, and polls `GET` attempt every 5 seconds. The learner can leave the page; reopening the attempt shows the current status.
4. Once the job is **completed**, the report is displayed:
   - Estimated score /200, overall summary, strengths, and areas for improvement.
   - Per question: score `x/3` or `x/5`, "What AI heard" (transcript), and 0-100 subscores for Overall Pronunciation, Accuracy, Fluency, Prosody, Completeness (Part 1), Grammar, Vocabulary, and Relevance. Also includes words needing pronunciation practice, feedback, and a suggested response.
5. When the job **fails**, the reason is shown along with a **"Retry Grading"** button. When rejected (`409`), the exact message from BE is displayed. For example, clicking within 15 seconds of submission displays "The final recording is still uploading".

**To see the live flow in the UI**, complete the steps in section 4: Azure Speech key and region, `Gemini__Enabled=true` and `Gemini__ApiKey` passed into the container, and `ffmpeg` installed in the image. Missing any step leaves the UI showing "not enabled" or causes the job to fail (`FAILED`) with the corresponding reason.

**Testing:** FE has 230/230 tests passing. Additions include the evaluation result parser, grading panel states (disabled, consented and queued, rejected, completed report, retry on error), and 2 new BFF routes. BE also added assertions for `evaluation-availability` in HTTP tests.
