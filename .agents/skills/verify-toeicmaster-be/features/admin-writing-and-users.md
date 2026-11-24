# Admin Writing and Users

This feature enables administrators to manage user accounts and author writing question banks and tests.
Administrators review user rosters, promote or lock accounts, issue password reset triggers, author standalone writing exercises across Parts 1-3, and validate or import bulk exercises and tests via Excel spreadsheets.

## Sub-features

- `ADM-USER-LIST`: Retrieve paginated user roster with filtering by role and status.
- `ADM-USER-LOCK`: Lock or unlock a target user account to control access.
- `ADM-USER-PWD`: Trigger administrative password reset for a learner account.
- `ADM-WRIT-EX`: Author, update, or archive standalone writing exercises.
- `ADM-WRIT-VAL`: Validate Excel workbooks containing bulk exercises prior to committing.
- `ADM-WRIT-IMP`: Import validated Excel workbooks and persist exercises with a typed count response.
- `ADM-WRIT-TEST`: Manage structured 5/2/1 writing tests and draft publication lifecycles.

## How to get to it (user POV)

- `GET /api/v1/admin/users` returns paginated user records.
- `GET /api/v1/admin/users/{id}` retrieves single user details.
- `POST /api/v1/admin/users/{id}/lock` locks the target user account.
- `DELETE /api/v1/admin/users/{id}/lock` unlocks the target user account.
- `POST /api/v1/admin/users/{id}/reset-password` initiates a password reset for the user.
- `GET /api/v1/admin/writing/exercises` lists question bank exercises.
- `POST /api/v1/admin/writing/exercises` creates a new writing exercise.
- `POST /api/v1/admin/writing/exercises/imports/validate` validates an exercise spreadsheet upload.
- `POST /api/v1/admin/writing/exercises/import` imports exercises and returns `ImportWritingItemsResponse`.
- `GET /api/v1/admin/writing/tests` lists writing tests.
- `POST /api/v1/admin/writing/tests/{id}/publish` publishes a draft writing test.

## Driving it with HTTP (curl)

Preconditions:
The API instance must be running on `http://127.0.0.1:15100` and an administrative access token `$ADMIN_TOKEN` must be obtained.

- Retrieve administrative user roster:
  ```bash
  curl -s -X GET "http://127.0.0.1:15100/api/v1/admin/users?page=1&pageSize=20" \
    -H "Authorization: Bearer $ADMIN_TOKEN"
  ```
  Observable result: HTTP 200 with JSON payload containing user items array and total count.

- Lock a user account:
  ```bash
  curl -s -X POST "http://127.0.0.1:15100/api/v1/admin/users/${TARGET_USER_ID}/lock" \
    -H "Authorization: Bearer $ADMIN_TOKEN"
  ```
  Observable result: HTTP 204 No Content confirming the lock status change.

- List writing exercises in question bank:
  ```bash
  curl -s -X GET "http://127.0.0.1:15100/api/v1/admin/writing/exercises?part=1&pageSize=10" \
    -H "Authorization: Bearer $ADMIN_TOKEN"
  ```
  Observable result: HTTP 200 returning writing exercises with `id`, `code`, `topic`, and `difficulty`.

- Validate writing exercise import spreadsheet:
  ```bash
  curl -s -X POST "http://127.0.0.1:15100/api/v1/admin/writing/exercises/imports/validate?part=3" \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    -F "file=@exercises.xlsx;type=application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
  ```
  Observable result: HTTP 200 returning `WritingExerciseImportPreviewDto` with valid row count and error lists.

- Import exercises from spreadsheet:
  ```bash
  curl -s -X POST "http://127.0.0.1:15100/api/v1/admin/writing/exercises/import?part=3" \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    -F "file=@exercises.xlsx;type=application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
  ```
  Observable result: HTTP 200 returning typed JSON `{"importedCount": 2}`.

## Gotchas

- Standard learner tokens lack the `AccountAdministration` and `WritingAdministration` policies and receive HTTP 403 Forbidden.
- Self-locking or deleting the currently authenticated administrator is rejected to prevent lockout.
- Empty or missing file uploads on import endpoints return RFC 9457 `ValidationProblemDetails` with status 400 and `traceId`.
- Publishing a writing test requires exact composition: five Part 1 exercises, two Part 2 exercises, and one Part 3 exercise.
