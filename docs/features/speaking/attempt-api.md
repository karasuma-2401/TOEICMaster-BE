# Speaking Attempt API

All endpoints use the `/api/v1/speaking` prefix and are open to unauthenticated users as well.
Every response includes `Cache-Control: no-store`.
The browser calls them through the same-origin BFF `/api/speaking/*`.

## Attempt Ownership

Requests with a valid JWT bind the attempt to `sub`.
If unauthenticated, a UUID must be sent in the `X-Speaking-Anonymous-Session` header; the backend stores only the SHA-256 hash of this value.
Each attempt belongs to exactly one owner type.
Any other caller receives `404`.

## Tests and Practice Exercises

These responses do not include rubrics or sample answers.

- `GET /tests?query=&pageNumber=1&pageSize=20`: published tests only.
- `GET /tests/{testId}`: includes `parts: [{ part, questionCount, durationSeconds }]`.
- `GET /exercises?part=1&pageNumber=1&pageSize=20`: `PUBLISHED` exercises for individual practice.
- `GET /exercises/{exerciseId}`: `{ id, code, part, topic, difficulty, questionCount, durationSeconds, updatedAtUtc }`.
- `GET /media/{assetId}`: images belonging to published exercises or tests only.

Standard `durationSeconds` values by Part: P1 180, P2 150, P3 69, P4 114, P5 105.

## Starting an Attempt

`POST /attempts` accepts exactly one of two shapes:

```json
{ "testId": "uuid", "mode": "FULL" }
```

```json
{ "exerciseId": "uuid" }
```

`mode` is `FULL` or `PART_1` through `PART_5`.
For individual exercise practice, the mode is taken from the exercise's Part.
Questions are renumbered starting from 1 and their content is snapshotted at creation time.

> **When to call:** the timer starts as soon as this request succeeds. Per [ADR-003](../../decisions/003-speaking-microphone-session.md), FE must have the microphone ready **before** calling.

## Taking the Test and Timers

- `GET /attempts`: history for the current owner; includes `recordedCount`, `evaluationStatus`, `estimatedScore`.
- `GET /attempts/{attemptId}`: load or resume. Automatically advances past any expired phases.
- `POST /attempts/{attemptId}/next`: ends the current phase. On the response phase of the last question, this request submits the attempt.
- `POST /attempts/{attemptId}/submit`: submits the attempt, idempotent.

Phase order for each question:

```text
READING (if any) → PREPARING (if any) → RESPONDING → lock question → next question
After the last question: status = SUBMITTED; currentQuestionPosition, currentPhase, phaseEndsAtUtc = null
```

FE counts down from `phaseEndsAtUtc`, accounting for clock skew using `serverNowUtc`.
The backend is the source of truth for time.
When a phase expires, FE calls `GET` to retrieve the new state.

For Parts 3 and 4, upcoming entries in `content.questions` are `null`.
Part 4 also returns `null` for the current question while in `READING`.

Response fields: `id`, `testId`, `exerciseId`, `testCode`, `testTitle`, `mode`, `status`, `currentQuestionPosition`, `currentPhase`, `phaseEndsAtUtc`, `serverNowUtc`, `createdAtUtc`, `submittedAtUtc`, `questions`, `reviewQuestions`, `evaluation`.

For individual practice attempts, `testId` is `null`, `testCode` is the exercise code, and `testTitle` follows the format `Practice Part n · <topic>`.

**Target (D2):** hide the skip button during the reading and preparation phases of `FULL` tests.
This only requires an FE change; if server-side enforcement is desired, a mode check must be added in `Next`.

## Recordings

`PUT /attempts/{attemptId}/recordings/{position}`: `multipart/form-data`, containing `file` and `durationSeconds`.

- Format: WebM, Ogg, or MP4, validated by file contents.
- Maximum size per question: `min(4 MiB, responseSeconds × 32 KiB + 256 KiB)`.
- Accepted only during that question's response phase, or within 15 seconds after the question is locked.
- Re-uploading for the same question within this window replaces the previous recording.
- Rate limit: 30 requests per minute per anonymous session, token, or IP address.

`GET /attempts/{attemptId}/recordings/{position}`: only the attempt owner can listen to it.

Guest recordings are deleted after 30 days.
Recordings are stored on the server, not only in the browser tab; the UI must communicate this accurately (MIC-11).

## After Submission

`reviewQuestions` contains the rubric and sample answer per question.
See [AI Evaluation](./ai-evaluation.md) for grading.

The FE route `/speaking/attempts/{id}` is shared between taking the test and reviewing it.
When `status = SUBMITTED`, FE must not request microphone permission.

## Errors

| Code | When |
| --- | --- |
| `400` | Invalid body, empty recording, or invalid format |
| `401` | Missing both a valid JWT and a valid anonymous session UUID |
| `404` | Not found or caller is not the attempt owner |
| `409` | Test is not ready; recording upload window has expired |
| `413` | Recording exceeds maximum size |
| `429` | Uploading recordings too frequently |
