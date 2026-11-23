# Exams and Exam Attempts

This feature provides the full TOEIC Listening and Reading exam experience for learners.
Learners browse published mock tests, inspect exam structure, start timed test attempts, submit answers, lock listening sections, submit completed attempts for scoring, and inspect performance summaries and history.

## Sub-features

- `EXAM-CAT`: Discover published 200-question Listening and Reading mock exams.
- `EXAM-VIEW`: Inspect full exam metadata and structure prior to starting.
- `ATMP-START`: Start a new exam attempt and establish a test session timer.
- `ATMP-ANSW`: Save intermediate multiple-choice answers for specific questions.
- `ATMP-FLAG`: Toggle bookmark or review flags on questions.
- `ATMP-SUB`: Submit completed test attempt to compute scaled scores (0-990).
- `ATMP-HIST`: Retrieve past test attempt scores and completion timestamps.

## How to get to it (user POV)

- `GET /api/v1/exams/lr` lists all published mock exams.
- `GET /api/v1/exams/{examId}/lr-overview` returns structural overview and section question counts.
- `POST /api/v1/exams/{examId}/attempts` starts a new attempt for the authenticated user.
- `GET /api/v1/exam-attempts/{attemptId}` retrieves active attempt state, questions, and current answers.
- `PUT /api/v1/exam-attempts/{attemptId}/answers` saves selected answers for one or more questions.
- `PUT /api/v1/exam-attempts/{attemptId}/questions/{questionId}/flag` toggles a question review flag.
- `POST /api/v1/exam-attempts/{attemptId}/submit` finalizes the test attempt and calculates raw and scaled scores.
- `GET /api/v1/exam-attempts/{attemptId}/result` returns scaled scores for Listening and Reading.
- `GET /api/v1/exam-attempts/history` lists previous exam attempts and overall scores.

## Driving it with HTTP (curl)

Preconditions:
The API instance must be running on `http://127.0.0.1:15100` with the standard sample exam seeded, and a valid learner access token `$TOKEN` must be obtained.

- Discover published exams:
  ```bash
  curl -s -X GET http://127.0.0.1:15100/api/v1/exams/lr \
    -H "Authorization: Bearer $TOKEN"
  ```
  Observable result: HTTP 200 returning a JSON array containing at least one exam with `code`, `title`, and `totalQuestions`.

- Inspect exam structural overview:
  ```bash
  curl -s -X GET "http://127.0.0.1:15100/api/v1/exams/${EXAM_ID}/lr-overview" \
    -H "Authorization: Bearer $TOKEN"
  ```
  Observable result: HTTP 200 with JSON details of total parts, listening questions, and reading questions.

- Start an exam attempt:
  ```bash
  curl -s -X POST "http://127.0.0.1:15100/api/v1/exams/${EXAM_ID}/attempts" \
    -H "Authorization: Bearer $TOKEN"
  ```
  Observable result: HTTP 200 returning attempt details with `attemptId`, `status: "InProgress"`, and `startedAtUtc`.

- Save answers during attempt:
  ```bash
  curl -s -X PUT "http://127.0.0.1:15100/api/v1/exam-attempts/${ATTEMPT_ID}/answers" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"answers\":[{\"questionId\":\"${QUESTION_ID}\",\"selectedOption\":\"A\"}]}"
  ```
  Observable result: HTTP 200 confirming saved answers.

- Submit attempt for score evaluation:
  ```bash
  curl -s -X POST "http://127.0.0.1:15100/api/v1/exam-attempts/${ATTEMPT_ID}/submit" \
    -H "Authorization: Bearer $TOKEN"
  ```
  Observable result: HTTP 200 returning `status: "Completed"`, `listeningScaledScore`, and `readingScaledScore`.

## Gotchas

- Starting an attempt requires an active authenticated session; anonymous attempts are rejected with 401.
- Only one attempt can be actively in progress per user per exam simultaneously.
- When an attempt is submitted or abandoned, further modifications to answers return HTTP 409 Conflict.
- Listening audio playback requires media asset attachment in the database and is locked once the reading section begins.
