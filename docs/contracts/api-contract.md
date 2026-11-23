# Backend API contract

## TOEIC Speaking

Admin and learner Speaking endpoints use `/api/v1/admin/speaking/*` and `/api/v1/speaking/*`.
Detailed contracts: [Admin API](../features/speaking/admin-api.md), [Attempt API](../features/speaking/attempt-api.md).
Cloning uses `POST /admin/speaking/tests/{id}/clone` with `{version, code, title}`, returning `201` and an independent draft DIRECT test from the source's snapshot content.
Publish validation error `422` preserves the full error message array, including the `Content` key for duplicate content.

Writing evaluation/history adds `rawScore`, `maxRawScore`, and `scorePercentage`; `estimatedScore` is `null` for practice items or single-Part attempts.
Only a full Writing test with all 5/2/1 questions is scaled to 0-200.

## TOEIC Listening & Reading Exam Administration

The `/api/v1/admin/exams`, `/api/v1/admin/exam-media`, and `/api/v1/admin/score-profiles` endpoint groups require the `AccountAdministration` policy (`Admin` role).
This scope covers exam content administration only.
There are no learner attempt, submission, scoring, or exam-taking endpoints here.

`POST /api/v1/admin/score-profiles` creates a reusable score conversion profile.
The request includes `name` and `conversions`, where each conversion has `skill` (`Listening` or `Reading`), `rawCorrectCount` from 0 to 100, and `scaledScore` from 5 to 495 as a multiple of 5.
`GET /api/v1/admin/score-profiles` returns the profiles for Admins to select when creating or importing an exam.
A profile is considered complete when it has 101 entries for each skill; the API does not automatically seed ETS score tables.

`POST /api/v1/admin/exams` accepts JSON:

```json
{ "code": "LR-2026-001", "title": "Practice test 1", "scoreProfileId": "<uuid>" }
```

Returns `201` with a Draft revision already populated with exactly 200 slots.
`GET /api/v1/admin/exams?status=Draft&pageNumber=1&pageSize=20` paginates by the latest revision status.
`GET /api/v1/admin/exams/{examId}/revisions/{revisionId}` returns the full preview content for Admins, including answer keys.

To edit a Draft:

| Endpoint | Description |
| --- | --- |
| `PUT /api/v1/admin/exams/{examId}/revisions/{revisionId}/questions/{questionNumber}` | JSON question content and answer key; Part 2 only accepts A-C, while other Parts accept A-D. Includes `imageRequired`; when `true`, Publish only succeeds after an image is attached. |
| `PUT /api/v1/admin/exams/{examId}/revisions/{revisionId}/score-profile` | JSON `{ "scoreProfileId": "<uuid>" }`; reassigns the score profile for the Draft. |
| `PUT /api/v1/admin/exams/{examId}/revisions/{revisionId}/blocks/{blockKey}/contexts` | JSON `{ "contexts": [...] }`, with `ordinal` sequential starting from 1. Contexts include `imageRequired`; required images must be attached before Publish. |
| `POST /api/v1/admin/exams/{examId}/revisions/{revisionId}/publish` | Publishes the revision if it is complete. |
| `POST /api/v1/admin/exams/{examId}/revisions` | JSON `{ "sourceRevisionId": "<published-revision-uuid>" }`; forks a published revision into a new Draft. |

`POST /api/v1/admin/exam-media/images` accepts `multipart/form-data` with field `file`, PNG/JPEG/WebP only, up to 5 MiB.
The API validates the file signature and returns an asset UUID; the client uses that UUID in `ImageAssetId`, not the filename or URL.

### Media workspace for exams

Every Draft revision uses a unified media architecture attached via presigned URLs.
`GET revision` returns `audioAssetId` and `audioDurationMs` for blocks, along with `imageRequired` for questions/contexts (`usesMediaWorkspace` and `hasMasterAudio` are no longer returned).

1. `POST /api/v1/admin/exams/{examId}/revisions/{revisionId}/media-upload-intents` accepts JSON `{ ownerKind, ownerKey, contentType, byteSize, durationMs? }` and returns `201` with `{ intentId, uploadUrl, expiresAtUtc, requiredHeaders }`.
   `ownerKind` is `AudioGroup` (Part 1-4), `QuestionImage` (Part 1-7), or `ContextImage` (Part 5-7).
   The server only issues URLs for Draft revisions with server-generated keys, MP3/WAV audio (`audio/mpeg`, `audio/mp3`, `audio/wav`, `audio/x-wav`) up to 100 MiB (with optional `durationMs` in `1..3_600_000`), and PNG/JPEG/WebP images up to 5 MiB (`durationMs` must be `null`).
2. The browser performs a `PUT` of raw bytes directly to `uploadUrl` with `requiredHeaders`; storage credentials are not exposed through the FE.
3. `POST /api/v1/admin/exams/{examId}/revisions/{revisionId}/media-upload-intents/{intentId}/complete` verifies object existence, MIME type, and size, saves `durationMs` from `expectedDurationMs` to `ExamMediaAsset`, and attaches it to the owner; returns `201` with `{ id, contentType, kind, byteSize, durationMs }`.
   Expired intents, already completed intents, or invalid Drafts return `409`.

Publish requires audio for every Listening Part 1-4 block and every image with `imageRequired=true`.
Incomplete intents are cleaned up after expiration; the bucket is private and presigned URLs are not stored in the database.

### Importing a complete L&R exam

`POST /api/v1/admin/exams/import` accepts `multipart/form-data` with the required field:

- `workbook`: XLSX, up to 10 MiB.

Importing creates a standard Draft revision; audio for each Part 1-4 block is uploaded afterward via `media-upload-intents`.

The workbook has four required sheets: `Manifest`, `Blocks`, `ContextBlocks`, and `Questions`.
`Manifest` contains `SchemaVersion=1`, `TestCode`, `Title`, `Scope=LISTENING_READING`, and `ScoreProfileId`.
`ContextBlocks` and `Questions` support the `ImageRequired` column (true/false; older workbooks omitting this column remain valid).
The importer matches exactly 103 blocks and 200 questions by `BlockKey` + `QuestionNumber`; it does not rely on row order.
Workbook errors return `400 ValidationProblemDetails` with key `workbook` and do not create a Draft.

Replace all placeholder content, `ScoreProfileId`, and `ImageAssetId` values before importing; the template is intentionally not publishable as-is.

`GET /api/v1/admin/exams/import-template` downloads a 200-question, 103-block XLSX template covering Parts 1-7.
The template is distributed by the API after admin sign-in.
Replace `ScoreProfileId`, `ImageAssetId` values, and demo content before calling the import endpoint.

Endpoints in this document use the `/api/v1` prefix.
The frontend calls the backend through Next.js route handlers and server-side `API_BASE_URL` configuration; do not expose backend URLs or credentials in the browser bundle.

The user profile contract uses the backend `/api/v1/profile` prefix; the BFF maps this group to `/api/profile` for the browser.
The `/api/v1/users/me` routes remain available for backward compatibility with older clients.

## User Profile

The browser calls Next.js BFF routes `/api/profile`, `/api/profile/password`, and `/api/profile/avatar`.
The BFF forwards requests to the backend under the `/api/v1/profile` prefix.
Backend routes under `/api/v1/users/me` are kept as compatibility aliases.
All backend endpoints below require `Authorization: Bearer <accessToken>`.
The backend reads the user ID from the authenticated `sub` claim; the client does not send a user ID.

| Endpoint | Input | Success | Primary Errors |
| :--- | :--- | :--- | :--- |
| `GET /api/v1/profile` | No body | `200` profile JSON | `401` when there is no valid session |
| `PATCH /api/v1/profile` | JSON `{ "displayName": "New Name" }` | `200` updated profile JSON | `400` when name is empty or exceeds 100 characters |
| `PUT /api/v1/profile` | JSON `{ "displayName": "Name", "phone": "0912345678", "targetScore": 750 }` | `200` updated profile JSON | `400` when data is invalid |
| `PUT /api/v1/profile/avatar` | `multipart/form-data`, field `file` | `200` updated profile JSON (or `204`) | `400` when file is invalid, `413` when exceeding 2 MiB |
| `GET /api/v1/profile/avatar` | No body | `200` binary PNG/JPEG/WebP | `404` when no avatar exists |
| `PUT /api/v1/profile/password` | JSON `{ "currentPassword": "...", "newPassword": "..." }` | `204 No Content` | `400` when current password is wrong or data is invalid |

Profile JSON:

```json
{
  "id": "00000000-0000-0000-0000-000000000001",
  "displayName": "Nguyen Van A",
  "email": "learner@example.com",
  "avatarUrl": "/api/v1/profile/avatar",
  "phone": "0912345678",
  "dateOfBirth": "2001-06-15",
  "targetScore": 750,
  "hasPassword": true
}
```

- `avatarUrl`, `phone`, and `targetScore` are `null` if not set.
- `avatarUrl` is a relative path requiring a Bearer token.
- Avatar size limit is 2 MiB, formats `image/png`, `image/jpeg`, or `image/webp`.
- Changing password requires 8-128 characters and revokes all existing refresh sessions.

## Admin Users

All endpoints under `/api/v1/admin/users` require the `AccountAdministration` policy (`Admin` role).
Errors return `application/problem+json` per RFC 9457 with `traceId` (and `code` for business conflicts or rate limits).

### 1. List users

`GET /api/v1/admin/users`

| Query | Default | Constraints |
| :--- | :--- | :--- |
| `page` | 1 | 1 to 10000 |
| `pageSize` | 10 | 1 to 100 |
| `q` | | Search in full name and email, up to 100 characters |
| `role` | | `USER`, `ADMIN` |
| `status` | | `ACTIVE`, `PENDING`, `LOCKED` |
| `sort` | `createdAt:desc` | `fullName`, `createdAt`, or `lastLoginAt`, with `:asc` or `:desc` |

Response `200 OK`:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000001",
      "fullName": "Nguyen Van A",
      "email": "learner@example.com",
      "avatarUrl": null,
      "role": "USER",
      "status": "ACTIVE",
      "emailVerified": true,
      "authProvider": "PASSWORD",
      "createdAt": "2026-10-01T00:00:00+00:00",
      "lastLoginAt": null
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalItems": 1,
  "totalPages": 1,
  "stats": { "totalUsers": 1, "activeUsers": 1, "pendingUsers": 0, "lockedUsers": 0 }
}
```

- `status`: `ACTIVE` when `IsActive`; `PENDING` when inactive and email not verified; `LOCKED` when inactive and email verified.
- `authProvider`: `GOOGLE` when the account has no password, otherwise `PASSWORD`.
- `lastLoginAt`: timestamp of the newest login session creation, excluding sessions created via refresh.
- `avatarUrl` is always `null` because the avatar endpoint only serves the currently authenticated user.
- `stats` is calculated across all users, independent of filters.

### 2. Get user details

`GET /api/v1/admin/users/{id}`

- Response `200 OK`: `AdminUserDto` (the same 10 fields as an element in `items`).
- Response `404 Not Found`: ProblemDetails when the user is not found.

### 3. Create new user

`POST /api/v1/admin/users`

```json
{
  "fullName": "Nguyen Van B",
  "email": "userb@example.com",
  "role": "USER",
  "sendActivationEmail": true
}
```

- `role`: `USER` or `ADMIN`.
- When `sendActivationEmail` is `true`: creates the account in `PENDING` state (`emailVerified: false`) and sends an activation code email; mail-sending errors release the cooldown.
- When `sendActivationEmail` is `false`: creates an `ACTIVE` account (`emailVerified: true`) without sending an email.
- Response `201 Created`: `Location` header pointing to `/api/v1/admin/users/{id}` and body `AdminUserDto`.
- Errors: `400 ValidationProblemDetails` (invalid input), `409 Conflict` (email already exists).

### 4. Update user role

`PATCH /api/v1/admin/users/{id}`

```json
{
  "role": "ADMIN"
}
```

- Response `200 OK`: updated `AdminUserDto` (idempotent if the role is unchanged).
  Demoting `ADMIN -> USER` revokes all active refresh sessions for that user.
- Errors: `400 ValidationProblemDetails`, `404 Not Found`, `409 Conflict` (`code: "CANNOT_DEMOTE_SELF"` when demoting oneself; `code: "LAST_ACTIVE_ADMIN"` when demoting the last active administrator).

### 5. Delete user

`DELETE /api/v1/admin/users/{id}`

- Response `204 No Content`: deletes the user within a transaction (clears self-FK `ReplacedBySessionId` before deletion and deletes the avatar in MinIO best-effort after commit).
- Errors: `404 Not Found`, `409 Conflict` (`code: "CANNOT_DELETE_SELF"`, `code: "LAST_ACTIVE_ADMIN"`, or `code: "USER_HAS_REFERENCED_CONTENT"` when the user is referenced by Knowledge content or `Restrict`-constrained data).

### 6. Lock user account

`POST /api/v1/admin/users/{id}/lock` (no request body)

- Response `204 No Content`: sets `IsActive = false`, revokes all active `UserSessions`, and removes `PasswordResetChallenges` in the same transaction.
- Errors: `404 Not Found`, `409 Conflict` (`code: "CANNOT_LOCK_SELF"`, `code: "LAST_ACTIVE_ADMIN"`, `code: "USER_PENDING_VERIFICATION"`).

### 7. Unlock user account

`DELETE /api/v1/admin/users/{id}/lock` (no request body)

- Response `204 No Content`: sets `IsActive = true` (idempotent if already `ACTIVE`).
- Errors: `404 Not Found`, `409 Conflict` (`code: "USER_PENDING_VERIFICATION"` when the account has not verified its email).

### 8. Send password reset for user

`POST /api/v1/admin/users/{id}/reset-password` (no request body)

- Response `204 No Content`: creates a password reset OTP and sends `PasswordResetMail` (mail-sending errors release the cooldown).
- Errors: `404 Not Found`, `409 Conflict` (`code: "PASSWORD_RESET_UNSUPPORTED_PROVIDER"` for Google-only accounts; `code: "USER_INACTIVE_OR_UNVERIFIED"` for locked or unverified accounts), `429 Too Many Requests` (`code: "PASSWORD_RESET_COOLDOWN"`).

## TOEIC Listening & Reading Mock Test (learner)

`GET /api/v1/exams/lr` allows viewing the list of published exams without signing in (similar to Speaking, Writing, and L&R Practice).
All remaining endpoints require a Bearer token and only allow access to attempts belonging to the authenticated user.
The server is the single source of time: the 120-minute time limit is set by the server (not sent by the client), with remaining time calculated from `startedAtUtc`.

| Method | Path | Purpose |
| ------ | ---- | ------- |
| GET | `/api/v1/exams/lr` | List of the latest Published revision for each exam, without answer keys (`AllowAnonymous`) |
| GET | `/api/v1/exams/{examId}/lr-overview` | Instructions screen: exam title, 200 questions, 120 minutes, 10-990 scale, `activeAttemptId` if an in-progress attempt exists |
| POST | `/api/v1/exams/{examId}/attempts` | Starts a new attempt, or resumes an active attempt. Any expired prior attempt is auto-submitted at its deadline before opening a new one |
| GET | `/api/v1/exams/{examId}/current-attempt` | Current in-progress attempt, `404` if none exists |
| GET | `/api/v1/exam-attempts/{attemptId}` | Reloads the exam and saved answers (`phase`: `listening` or `reading`) |
| POST | `/api/v1/exam-attempts/{attemptId}/start-reading` | Switches to Reading. From this point, questions 1-100 are locked |
| PUT | `/api/v1/exam-attempts/{attemptId}/answers` | Autosave: `[{ "questionId", "selectedKey", "isFlagged" }]`, up to 200 questions per call. Null or empty `selectedKey` clears the selection |
| PUT | `/api/v1/exam-attempts/{attemptId}/questions/{questionId}/flag` | Flags or unflags a question: `{ "isFlagged": true }` |
| POST | `/api/v1/exam-attempts/{attemptId}/submit` | Submits the attempt and returns the result |
| POST | `/api/v1/exam-attempts/{attemptId}/abandon` | Abandons the current in-progress attempt |
| GET | `/api/v1/exam-attempts/{attemptId}/listening-audio-urls` | Ordered list of Part 1-4 audio presigned URLs (`items: [{ blockId, part, firstQuestionNumber, audioAssetId, durationMs, url, expiresAtUtc }]`); returns `409` if Listening is locked or status is not `InProgress` |
| GET | `/api/v1/exam-attempts/{attemptId}/audio/{assetId}` | Presigned URL for an audio asset belonging to the attempt (`409` if `InProgress` and Listening is locked; allowed during Review when `Completed`) |
| GET | `/api/v1/exam-attempts/{attemptId}/images/{assetId}` | Presigned URL for an image belonging to the attempt |
| GET | `/api/v1/exam-attempts/{attemptId}/result` | Score report: Listening/Reading scores, total, correct/wrong/skipped counts, performance by Part (`parts`) |
| GET | `/api/v1/exam-attempts/{attemptId}/review?filter=All` | Review: `All`, `Incorrect` (wrong or skipped), `Flagged`. Questions are grouped by `blocks` with passages (`contextBlocks`), `audioAssetId`, and `audioDurationMs` for Parts 1-4; `status` is `correct`, `wrong`, or `skipped` |
| GET | `/api/v1/exam-attempts/history` | Attempt history |

Server rules:

- Answers must be `A`-`D` (Part 2 only `A`-`C`) and the question must belong to the attempt's exam; otherwise `400`.
- After `start-reading`, editing or flagging Listening questions returns `409`.
- Past the deadline plus 5 seconds, saving answers returns `409`.
  Reopening or submitting after the deadline grades the attempt at the deadline with `isTimeExpired = true`.
- `review` and `result` are unavailable before submission (`409`).
- Unanswered questions count as `skipped`.
  Scaled scores use the exam's score profile; if the profile is missing entries, `400` is returned instead of interpolating scores.
- `explanation` and `translation` are the only two text fields currently available per question.
  The Traps, Notes, and Skills sections on the Review screen do not yet have data.

Migration `AddExamAttemptReadingPhase` adds the `ExamAttempts.ReadingStartedAtUtc` column.

## Listening & Reading Practice (learner)

| Method | Path | Auth | Purpose and Status |
| ------ | ---- | ---- | ------------------ |
| GET | `/api/v1/lr-practice/parts/{part}/items?pageNumber=1&pageSize=20` | Anonymous | List of Published items for Parts 1-7, up to 50 items per page; returns `400` if Part or page is invalid |
| GET | `/api/v1/lr-practice/items/{id}` | Anonymous | Published item details; omits `correctKey`, `translation`, and `explanation`; nonexistent or Draft items return `404` |
| POST | `/api/v1/lr-practice/items/{id}/submit` | Bearer | Grades and saves the learner's attempt; returns `200`, `400`, `401`, or `404` |
| GET | `/api/v1/lr-practice/parts/{part}/statistics` | Bearer | Learner-specific statistics by item within the Part; returns `200`, `400`, or `401` |

The submit request has the shape `{ "answers": [{ "questionId": "<uuid>", "selectedKey": "A" }] }`.
A missing or empty `selectedKey` counts as skipped, while any other value must be an existing option for the question and at most 8 characters long.
The user ID is always read from the JWT `sub` or `NameIdentifier` claim.
Statistics are aggregated in PostgreSQL and do not read attempts from other users.

## Authentication Rate Limiting

Authentication endpoints apply a fixed-window rate limit by IP address per API instance:

| Group | Endpoints | Local Limit |
| --- | --- | --- |
| Email sending | `register`, `resend-verification`, `forgot-password` | 5 requests/minute/IP |
| Credentials | `verify-email`, `verify-password-reset`, `reset-password`, `login`, `google` | 10 requests/minute/IP |
| Sessions | `refresh`, `logout` | 30 requests/minute/IP |

When exceeded, the API returns `429 Too Many Requests`, a `Retry-After` header, and `ProblemDetails` with `traceId`.
This rate limit supplements the per-account resend cooldown and OTP failure limit; it does not replace those two mechanisms.
The local build stores counters in process memory.
When deploying multiple API instances, move the shared rate-limiting layer to a gateway or distributed store and configure trusted proxies before using forwarded client IPs.

## Local Account Registration

`POST /api/v1/auth/register`

Request:

```json
{
  "displayName": "Nguyen Van A",
  "email": "learner@example.com",
  "password": "at-least-eight-characters"
}
```

- `displayName`: required, 1-100 characters after trimming.
- `email`: required, valid email format, maximum 254 characters.
  The backend trims and uses a case-insensitive normalized form for duplicate checks.
- `password`: preserves whitespace, 8-128 characters.
  The backend only stores a BCrypt enhanced hash, never plaintext.

| Status | Description |
| --- | --- |
| `202 Accepted` | Creates an inactive account and sends an OTP; empty response body. |
| `400 Bad Request` | `ValidationProblemDetails`, errors keyed by `DisplayName`, `Email`, `Password`. |
| `409 Conflict` | Email belongs to an already activated account. |

New local accounts cannot sign in before verification.
The OTP consists of 6 digits, expires after 10 minutes, is stored only as an HMAC in PostgreSQL, and allows at most 5 failed attempts.
Re-registering an email that is pending verification does not overwrite the stored name or password; the backend only sends a new code if the 60-second cooldown has passed.
During the cooldown, it still returns `202` to avoid creating an enumeration/spam vector.

`POST /api/v1/auth/verify-email`

```json
{ "email": "learner@example.com", "code": "123456" }
```

Returns `204 No Content` on successful verification.
An invalid, expired, or 5-times-failed code returns `400 ProblemDetails` with a generic message.
After receiving `204`, the frontend redirects the user to the sign-in page.

`POST /api/v1/auth/resend-verification`

```json
{ "email": "learner@example.com" }
```

Always returns `202 Accepted` for valid requests, even if the email does not exist, is already active, or is within the cooldown.
Only inactive accounts past the cooldown are issued and sent a new code; this prevents email enumeration and SMTP spam.

## Forgot Password

The password reset flow consists of three steps.
OTPs and reset tokens are stored only as hashes, are single-use, and are completely separate from account activation OTPs.

### Request a password reset code

`POST /api/v1/auth/forgot-password`

```json
{ "email": "learner@example.com" }
```

Valid requests always return `202 Accepted` with an empty body, even if the email does not exist, belongs to a Google-only account, is inactive, or is within the 60-second cooldown.
Only activated and email-verified local accounts receive a 6-digit OTP.
The OTP expires after 10 minutes and allows at most 5 failed attempts.

### Verify the password reset code

`POST /api/v1/auth/verify-password-reset`

```json
{ "email": "learner@example.com", "code": "123456" }
```

Response `200 OK`:

```json
{
  "resetToken": "<opaque-one-time-token>",
  "expiresAtUtc": "2026-10-15T12:10:00+00:00"
}
```

The response includes `Cache-Control: no-store`.
Successful verification invalidates the OTP so it cannot be reused; the reset token has a 10-minute lifetime and can only be used once.
An invalid, expired, already used, or 5-times-failed code returns `400 ProblemDetails` with a generic message.

### Set a new password

`POST /api/v1/auth/reset-password`

```json
{
  "resetToken": "<opaque-one-time-token>",
  "newPassword": "new-password"
}
```

- `newPassword`: preserves whitespace, 8-128 characters.
- Success returns `204 No Content`, consumes the reset token (single-use), and revokes all active refresh sessions for the account.
  Previously issued access tokens expire naturally according to their maximum 15-minute lifetime.
- Invalid, expired, or already used tokens return `400 ProblemDetails` with a generic message.
- The response includes `Cache-Control: no-store`.

## Local Sign-In

`POST /api/v1/auth/login`

Request:

```json
{
  "email": "learner@example.com",
  "password": "user-password",
  "rememberMe": true
}
```

The backend trims and case-insensitively normalizes the email; the password is checked verbatim.
Nonexistent accounts, unverified emails, accounts without a local password, disabled accounts, or wrong passwords all receive the same `401` response so account status is not disclosed.

Response `200 OK`:

```json
{
  "tokenType": "Bearer",
  "accessToken": "<jwt>",
  "accessTokenExpiresAtUtc": "2026-10-12T00:15:00+00:00",
  "refreshToken": "<opaque-random-token>",
  "refreshTokenExpiresAtUtc": "2026-11-11T00:00:00+00:00",
  "user": {
    "id": "00000000-0000-0000-0000-000000000000",
    "displayName": "Nguyen Van A",
    "email": "learner@example.com"
  }
}
```

- Access tokens live for 15 minutes.
  The JWT contains `sub`, `email`, `name`, `jti`, `iss`, `aud`, `iat`, `nbf`, and `exp`; there is no role claim yet because the authorization model has not been finalized.
- Refresh sessions live for 1 day when `rememberMe=false`, and 30 days when `rememberMe=true`.
- PostgreSQL only stores the SHA-256 hash of the refresh token.
  The raw token only appears in the response when issued.
- The Next.js BFF should store the refresh token in a `Secure`, `HttpOnly`, `SameSite` cookie; do not store refresh tokens in `localStorage` or browser-side JavaScript.

| Status | Description |
| --- | --- |
| `200 OK` | Token pair and user information. |
| `400 Bad Request` | `ValidationProblemDetails` keyed by `Email`, `Password`. |
| `401 Unauthorized` | `ProblemDetails` with the generic title `"Email or password is incorrect."` |

## Session Refresh and Revocation

`POST /api/v1/auth/refresh`

```json
{ "refreshToken": "<opaque-random-token>" }
```

The `200` response has the same structure as login.
On each refresh, the old token is revoked and replaced by a new token within the same original absolute expiration window.
If two concurrent requests use the same refresh token, only one succeeds; the other receives `401`.

`POST /api/v1/auth/logout`

```json
{ "refreshToken": "<opaque-random-token>" }
```

Logout returns `204 No Content` and is idempotent.
Nonexistent or already revoked refresh tokens still return `204`.
Previously issued access tokens are not revoked immediately and expire naturally after at most 15 minutes.

## Google Sign-In

`POST /api/v1/auth/google`

The frontend uses Google Identity Services with the same Web Client ID configured on the backend, receives the `credential` field from Google, and forwards that raw ID token through the Next.js BFF:

```json
{
  "credential": "<google-signed-id-token>",
  "rememberMe": true
}
```

The backend uses the official Google library to validate the signature, `aud`, `iss`, and `exp`, and requires a verified email.
Google accounts are identified by the `sub` claim; do not use browser-submitted email or profile data as the identifier.
The `200` response uses the same token/session structure as local sign-in.

On first sign-in:

- If the email does not exist in the system, the backend creates a user without a local password and links `(google, sub)`.
- If the email matches an existing account and is a Gmail or Google Workspace address, the backend may auto-link it because Google is authoritative for verifying that email.
- If a Gmail/Workspace email matches an unverified local registration, the backend discards the unverified local password, deletes old OTPs, activates the account using the Google identity, and links `(google, sub)`.
  This prevents whoever initiated the pending registration from continuing to use a password they know.
- If the email matches an existing account with a non-Gmail/Workspace third-party email, the backend returns `409`; the user must sign in locally and perform a separate account-linking flow first.
  Account linking from the account page is outside the scope of this task.

| Status | Description |
| --- | --- |
| `200 OK` | Token pair and user information. |
| `400 Bad Request` | Missing or invalid-length `Credential`. |
| `401 Unauthorized` | Invalid or expired Google ID token, wrong audience, or unverified email. |
| `409 Conflict` | Unsafe to automatically link an existing account with the same email. |

If the FE uses popup/callback and then calls the Next.js BFF, the BFF sends JSON as above.
If switching later to direct redirect mode from Google, the callback endpoint must add double-submit CSRF token validation per Google's guidance; do not use this JSON endpoint as a direct redirect callback.

---

## TOEIC Writing Exam & Question Bank Administration

Endpoints under `/api/v1/admin/writing/*` use the `WritingAdministration` policy requiring the `Admin` role.
See [Writing Admin API](../features/writing/admin-api.md) for data structures, Excel import, and images.

- `/exercises`: list, create, edit, and archive standalone exercises; `/exercises/imports/validate` and `/exercises/import` validate and import Excel files by Part.
- `/tests`: create/edit/delete drafts, publish complete 5/2/1 tests, archive and restore published tests; `/tests/import-template`, `/tests/imports/validate`, and `/tests/import` support Excel test import.
  Delete, archive, and restore require the current version.
- `/media/images`: upload Part 1 images; `modelAnswer` reference answers are internal options and are not returned to learners.

## TOEIC Writing Mock Test & Practice

Endpoints under `/api/v1/writing` identify the owner via JWT or the `X-Writing-Anonymous-Session` header; attempts belonging to another owner receive `404`.
See [Writing Attempt API](../features/writing/attempt-api.md) and [AI Evaluation](../features/writing/ai-evaluation.md).

- `GET /exercises?part=1&pageNumber=1&pageSize=20` lists published standalone exercises; `POST /exercises/{id}/attempts` starts an untimed practice attempt.
- `GET /tests`, `GET /tests/{id}` list published tests; `POST /attempts` with `{ "testId": "uuid", "mode": "FULL" }` starts a test.
- `GET /attempts`, `GET /attempts/{id}`, `PUT /attempts/{id}/answers/{position}`, `POST /attempts/{id}/next`, and `POST /attempts/{id}/submit` support history, saving, navigating questions, and submitting.
- `POST /attempts/{id}/evaluation-consent` only sends responses and images to AI after consent is given; the FE polls `GET /attempts/{id}`.
  Statuses include `CONSENT_REQUIRED`, `QUEUED`, `PROCESSING`, `RETRYING`, `COMPLETED`, and `FAILED`; re-submit consent when `FAILED` to retry.
- Guests can create at most one new evaluation in 24 hours; retries of the same attempt do not consume new quota.
  Sample answers are not displayed to learners.
  AI scores are for practice only.

## IPA Pronunciation Practice

### `POST /api/v1/pronunciation/assessments`

Requires a valid Bearer JWT and `Content-Type: multipart/form-data`.
Each API instance enforces a one-minute fixed window of up to 30 requests per authenticated user ID, even across token rotation.
Requests missing a token or with an invalid token share a quota by connecting IP address and receive `401` while within quota.
Requests exceeding quota receive `429` with `Retry-After` as an integer number of seconds to wait.

| Field | Required | Constraints |
| --- | --- | --- |
| `file` | Yes | WebM, Ogg, MP4, or WAV recording detected by file signature. Maximum 1 MiB and 10 seconds of audio after decoding. |
| `referenceText` | Yes | 1-40 characters after trimming leading and trailing whitespace. Accepts only English letters, spaces, apostrophes, and hyphens. |
| `locale` | No | `en-US` (default) or `en-GB`. |

`200 OK` returns `PronunciationAssessmentDto` as `application/json` with the following structure.

```json
{
  "recognitionStatus": "Recognized",
  "referenceText": "think",
  "recognizedText": "Think.",
  "accuracyScore": 78.0,
  "pronunciationScore": 80.5,
  "completenessScore": 100.0,
  "words": [
    {
      "word": "think",
      "accuracyScore": 78.0,
      "errorType": "None",
      "phonemes": [
        {
          "phoneme": "θ",
          "accuracyScore": 54.0,
          "candidates": [{ "phoneme": "s", "score": 71.0 }]
        }
      ]
    }
  ]
}
```

Scores range from 0-100 and are rounded to one decimal place.
`recognitionStatus` is `Recognized` or `NoSpeech`.
When `NoSpeech`, all scores are 0, `recognizedText` is `""`, and `words` is `[]`.

Errors use `application/problem+json` with `traceId` and `code` when applicable.

| HTTP | `code` | When |
| --- | --- | --- |
| `400` | `INVALID_AUDIO` | Missing recording, invalid format, undecodable, or longer than 10 seconds. |
| `400` | None | Invalid `referenceText` or `locale`. `ValidationProblemDetails` includes the `errors` field. |
| `401` | None | Missing valid Bearer JWT. |
| `413` | `INVALID_AUDIO` | Recording exceeds 1 MiB. |
| `422` | `SPEECH_REJECTED` | Scoring service rejected the recording. |
| `429` | None | Quota exceeded. Response includes `Retry-After` header. |
| `502` | `SPEECH_PROVIDER_ERROR` | Network error, scoring service error, or unreadable response from the provider. |
| `503` | `NOT_CONFIGURED` | Scoring service not configured, provider authentication failed, or server is missing `ffmpeg`. |
| `504` | `SPEECH_TIMEOUT` | Timed out converting the recording or waiting for the scoring provider. |
