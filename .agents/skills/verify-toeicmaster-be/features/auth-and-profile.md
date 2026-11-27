# Authentication and Profile

This feature covers learner registration, credential authentication, session refresh, and learner profile retrieval and updates.
Users register with an email and password, sign in to receive short-lived JWT access tokens and long-lived refresh tokens, and manage personal profile details including display names and target scores.

## Sub-features

- `AUTH-REG`: Register a new local account and receive a 202 Accepted status.
- `AUTH-LOG`: Sign in with email and password to obtain access and refresh tokens.
- `AUTH-REF`: Exchange a valid refresh token for a newly rotated session.
- `PROF-GET`: Retrieve the authenticated learner's profile details.
- `PROF-UPD`: Update learner display name, phone, or target TOEIC score.
- `PROF-PWD`: Change account password and revoke all active refresh tokens.

## How to get to it (user POV)

- `POST /api/v1/auth/register` initiates new account registration.
- `POST /api/v1/auth/login` authenticates an active account and issues session tokens.
- `POST /api/v1/auth/refresh` rotates a refresh token and returns a fresh access token.
- `POST /api/v1/auth/logout` revokes the refresh session.
- `GET /api/v1/profile` reads the authenticated user profile.
- `PATCH /api/v1/profile` updates select profile fields such as display name.
- `PUT /api/v1/profile` replaces profile metadata including phone and target score.
- `POST /api/v1/profile/password` updates account credentials.

## Driving it with HTTP (curl)

Preconditions:
The API instance must be running on `http://127.0.0.1:15100` and the test database must be initialized.

- Sign in with administrative credentials:
  ```bash
  curl -s -X POST http://127.0.0.1:15100/api/v1/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email":"admin@example.test","password":"Password123!"}'
  ```
  Observable result: HTTP 200 with JSON payload containing `tokenType`, `accessToken`, and `refreshToken`.

- Retrieve user profile:
  ```bash
  curl -s -X GET http://127.0.0.1:15100/api/v1/profile \
    -H "Authorization: Bearer $TOKEN"
  ```
  Observable result: HTTP 200 with JSON payload containing `id`, `email`, `displayName`, and role information.

- Update user profile display name:
  ```bash
  curl -s -X PATCH http://127.0.0.1:15100/api/v1/profile \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"displayName":"Updated Admin Name"}'
  ```
  Observable result: HTTP 200 returning updated `displayName` matching the payload.

- Verify refreshed session:
  ```bash
  curl -s -X POST http://127.0.0.1:15100/api/v1/auth/refresh \
    -H "Content-Type: application/json" \
    -d "{\"refreshToken\":\"$REFRESH_TOKEN\"}"
  ```
  Observable result: HTTP 200 with rotated `accessToken` and `refreshToken`.

## Gotchas

- Unverified accounts cannot log in directly until their email verification is confirmed in the database.
- Refresh tokens are single-use; reusing a previously exchanged refresh token revokes the entire session chain.
- The authentication endpoints enforce a fixed-window rate limit per IP address that returns 429 when exceeded.
- Avatar uploads require multipart form data with PNG, JPEG, or WebP payloads under 2 MiB.
