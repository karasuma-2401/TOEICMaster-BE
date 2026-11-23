# Checklist: TOEIC Listening & Reading

> A `[x]` indicates corresponding source code exists; it does not imply deployment or UAT completion.

## Backend

- [x] Exam/ExamRevision administration, publish/fork, 200 questions, and Admin media.
- [x] ScoreProfile with all 101 conversion entries per skill.
- [x] Persistence for ExamAttempt and ExamAttemptAnswer.
- [x] Learner API: Published list, overview, create/resume, load attempt.
- [x] Answer autosave, flagging, Listening -> Reading transition, and server-side Listening lock.
- [x] Server-side deadline, auto-grade on timeout, submit, result, review, history.
- [x] Never return correctKey/explanation/translation before submit.
- [x] Application tests for lifecycle, ownership, validation, deadline, and scoring.
- [x] Learner presigned GET media: Part 1-4 audio playlist, single audio by asset, and attempt images. Legacy Master Audio has been removed.
- [x] HTTP/DB integration tests (Postgres) for media, Listening playlist, Listening lock, and review.
- [ ] UAT with MinIO/production storage.
- [ ] Practice API from the L&R exercise bank and exam assembly from the bank.

## Frontend/BFF

- [x] Learner `/api/lr/*` BFF: same-origin mutation, session token forwarding, no-store.
- [x] Typed API client for catalog, overview, attempt, autosave, start-reading, submit, result, and review.
- [x] Published exam list backed by Backend API.
- [x] Instructions page loading real exam data and starting/resuming attempts.
- [x] Attempt page loading real snapshot and mapping questions/options/contexts.
- [x] Answer/flag autosave; submit and switch to Reading call the Backend.
- [x] Result and review pages backed by Backend data.
- [ ] Real audio player with autoplay and pause/seek lock using the Backend presigned GET playlist.
- [ ] Render question/context images from media asset IDs using the Backend presigned GET image endpoint.
- [ ] Display attempt history and attempt count/best score on the library.
- [ ] Integrate Part 1-7 practice with the learner exercise API (Backend does not yet have a learner API for the exercise bank).
- [ ] E2E tests and UAT in an environment with real Published exam data.

## Remaining Risks / Limitations

- Do not use the Admin preview API in the learner flow because its response includes correct answers.
- Learner media uses presigned GET URLs with a 3-hour TTL; the playlist and in-attempt audio endpoints return `409` once Listening is locked.
- The backend does not enforce player pause/seek restrictions; this is a UI constraint, not a server-side check.
- Tests/builds must pass in both repositories and the `API_BASE_URL` deployment env var must be verified before merging.
