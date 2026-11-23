# Listening & Reading Learner API

> **Status:** The backend already provides the learner attempt API in `ExamAttemptsController`; the endpoints below require a Bearer token and only access the current user's attempts.
> Learner FE integrates via the Next.js BFF `/api/lr/*`.

## Implemented Endpoints

| Method | Endpoint | Functionality |
| --- | --- | --- |
| GET | `/api/v1/exams/lr` | List latest Published revisions per exam, without returning correct answers |
| GET | `/api/v1/exams/{examId}/lr-overview` | Exam info and activeAttemptId |
| POST | `/api/v1/exams/{examId}/attempts` | Create an attempt or resume an in-progress attempt |
| GET | `/api/v1/exam-attempts/{attemptId}` | Load exam snapshot, saved answers, phase state, and remaining time |
| POST | `/api/v1/exam-attempts/{attemptId}/start-reading` | Transition to Reading and lock Listening |
| PUT | `/api/v1/exam-attempts/{attemptId}/answers` | Autosave up to 200 questions |
| PUT | `/api/v1/exam-attempts/{attemptId}/questions/{questionId}/flag` | Toggle flag on a question |
| POST | `/api/v1/exam-attempts/{attemptId}/submit` | Submit and score the attempt |
| GET | `/api/v1/exam-attempts/{attemptId}/result` | View results after submitting |
| GET | `/api/v1/exam-attempts/{attemptId}/review?filter=All` | View answers/explanations after submitting (`audioAssetId`, `audioDurationMs` for Parts 1-4) |
| GET | `/api/v1/exam-attempts/{attemptId}/listening-audio-urls` | List of presigned GET URLs for all Listening Part 1-4 blocks in order (`items`) |
| GET | `/api/v1/exam-attempts/{attemptId}/audio/{assetId}` | Presigned GET URL for a single attempt audio block (`409` when Listening is locked) |
| GET | `/api/v1/exam-attempts/{attemptId}/images/{assetId}` | Presigned GET URL for an attempt image |
| GET | `/api/v1/exam-attempts/history` | Attempt history |
| POST | `/api/v1/exam-attempts/{attemptId}/abandon` | Abandon an in-progress attempt |

## Security and Business Rules

- User ID is always read from the authenticated claim; the client does not send UserId.
- In-progress attempt responses never include `correctKey`, explanation, or translation.
- An attempt is bound to the revision and score profile at start time; results are scored using the bound score profile.
- The server is the single source of time, with a default limit of 120 minutes.
  The client only displays a timer based on RemainingSeconds.
- After starting Reading, Listening cannot be modified or flagged.
- Unanswered questions count as skipped; scoring only occurs on submit or expiration.
- Review/result can only be read once the attempt is Completed.
- Answers must be valid for the question's options; Part 2 only accepts A-C.
- Autosave allows a 5-second grace period after the deadline to handle network latency.

## Listening & Reading Practice

Learners can use the `/api/v1/lr-practice` endpoints to practice individual Published items without opening an exam attempt.

`GET /api/v1/lr-practice/parts/{part}/items` and `GET /api/v1/lr-practice/items/{id}` are anonymous and only return content without answers.
`POST /api/v1/lr-practice/items/{id}/submit` and `GET /api/v1/lr-practice/parts/{part}/statistics` require a Bearer token.
Submit saves an answer snapshot, and statistics only include data for the user in the JWT.
Invalid payload, question not belonging to the item, duplicate key, or non-existent key returns `400 application/problem+json`; non-existent or unpublished item returns `404`.

## Media

The sheet DTO (`ExamAttemptSheetDto`) no longer includes `MasterAudioObjectKey`; each `ExamBlockSheetDto` returns `audioAssetId` and `audioDurationMs` (ms) for Parts 1-4.
The review DTO (`ExamAttemptReviewBlockDto`) returns `audioAssetId` and `audioDurationMs` for Parts 1-4 (`null` for Parts 5-7).
During the Listening phase (`InProgress` and `IsListeningLocked == false`), the learner calls `GET /api/v1/exam-attempts/{attemptId}/listening-audio-urls` to receive the list of presigned GET URLs (3-hour expiry) for all Listening Part 1-4 blocks in a single request.
After transitioning to Reading (`IsListeningLocked == true`) or when the attempt is not in `InProgress` status, both the playlist endpoint and the in-exam `GET .../audio/{assetId}` endpoint return `409 Conflict`.
Once the attempt is `Completed`, the learner can still call `GET .../audio/{assetId}` on the Review screen.
The legacy endpoint `GET /api/v1/exam-attempts/{attemptId}/audio` has been removed.

## Frontend

Learner FE uses the BFF `/api/lr/[...segments]`, checks same-origin on mutations, reads the access token from the HttpOnly session cookie, forwards requests to `/api/v1/*`, and sets `Cache-Control: no-store`.
Do not call Admin preview APIs from the learner client.
