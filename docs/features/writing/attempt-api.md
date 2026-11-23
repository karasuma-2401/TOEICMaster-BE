# Writing Attempt API

All endpoints use the `/api/v1/writing` prefix. `GET /tests` and `GET /tests/{testId}` only return published tests; they do not return
Admin model answers or rubrics. `GET /media/{assetId}` only serves images referenced by published content.

## Attempt Ownership

Requests with a valid JWT bind the attempt to the account's `sub`. For unauthenticated users, the client/BFF generates a random UUID
and sends it in the `X-Writing-Anonymous-Session` header; the backend only stores the SHA-256 hash of the UUID. Do not pass the owner identifier in
the body. An attempt belongs to **only one** of the two owner types, and a different owner receives `404` instead of learning whether the attempt exists.

## Tests and History

- `GET /exercises?part=1&pageNumber=1&pageSize=20`: lists only published standalone exercises.
- `POST /exercises/{exerciseId}/attempts`: starts an untimed practice attempt, saves a separate snapshot, and uses the save/submit/AI grading endpoints below.
- `GET /tests?query=&pageNumber=1&pageSize=20`
- `GET /tests/{testId}`
- `GET /attempts`: up to the 100 most recent attempts of the current owner.

`POST /attempts` has the body:

```json
{ "testId": "uuid", "mode": "FULL" }
```

`mode` is `FULL`, `PART_1`, `PART_2`, or `PART_3`. Full tests preserve the exact order of 5 Part 1 questions, 2 Part 2 questions, and 1 Part 3 question;
Part-specific modes only take questions of that Part.

## Taking the Test and Timer

- `GET /attempts/{attemptId}`: loads/resumes the attempt and automatically transitions states if the server deadline has passed.
- `PUT /attempts/{attemptId}/answers/{position}` body `{ "answer": "..." }`: autosaves only the current question while
  in the `ANSWERING` phase.
- `POST /attempts/{attemptId}/next`: locks the current question and moves to the next question; only usable in `ANSWERING`.
- `POST /attempts/{attemptId}/submit`: idempotent, locks the current question if still in progress, and returns
  `SUBMITTED_AWAITING_EVALUATION`.

The attempt response always includes `serverNowUtc`, `phaseEndsAtUtc`, `currentQuestionPosition`, `currentPhase`, and the prompt snapshot.
The frontend counts down from `phaseEndsAtUtc`; the backend is the authoritative time source. Passed deadlines sequentially transition
`PREPARING → ANSWERING → lock question → next question`, even when the user closes the tab for longer than multiple phases combined. Fixed durations:
Part 1 is 30s preparation + 60s answering; Part 2 is 60s + 540s; Part 3 is 120s + 1680s.

The public response of `writing_attempt_questions` only contains the display prompt; it does not return `modelAnswer` or `rubricNotes`
while the User is taking the test or viewing results. The server keeps an immutable private review snapshot for evaluation;
`reviewQuestions` is always empty in the learner API. Images in the snapshot are downloaded via
`GET /attempts/{attemptId}/media/{assetId}` and require the same owner as the attempt.

The `attempts` endpoints and corresponding BFF responses always include `Cache-Control: no-store`, so browsers/proxies do not cache
the User's submission, timer state, or results. Autosave is debounced by 800ms; when the tab is hidden or the User leaves the page, the frontend
sends an additional best-effort `keepalive` request. Do not treat this lifecycle request as an absolute guarantee: upon reopening the page,
the API remains the source of truth and returns the most recently server-saved answer.

After submitting, the server creates the AI job in `CONSENT_REQUIRED`. See [AI evaluation for Writing](ai-evaluation.md) for
consent, retries, and score conversion details.

The `AddWritingAttempts` migration creates `writing_attempts` and `writing_attempt_questions`. It has only been added to source; `database update` has not yet
been run on Neon.
