# TOEICMaster Backend Verification Map

This directory is the maintained source for verifying the user-facing behavior of the TOEICMaster backend REST API.
Read this index before driving the API, then use the matching feature file as the operational recipe.

## Baseline preconditions

- Launch the backend API using `.agents/skills/verify-toeicmaster-be/scripts/control-be.sh launch --port 15100 --db-port 15434`.
- Run `.agents/skills/verify-toeicmaster-be/scripts/control-be.sh doctor --port 15100` and require HTTP 200 on both live health and OpenAPI endpoints.
- Maintain an ephemeral PostgreSQL instance that was created specifically for this verification run.
- Bootstrap the administrative account with email `admin@example.test` and password `Password123!`.
- Set the base URL variable to `http://127.0.0.1:15100`.
- Never drive an instance or database that was not started by this verification run.

## Driving conventions

- Start every recipe from the baseline state unless its preconditions specify otherwise.
- Format all payloads as valid JSON with `Content-Type: application/json` unless exercising multipart upload endpoints.
- Pass authentication tokens as `Authorization: Bearer <accessToken>` headers.
- Capture HTTP status codes, headers, and response bodies in evidence artifacts.
- Verify state mutations using direct database inspection against the ephemeral test database.
- Do not remove proof artifacts during instance teardown.

## Proof and skip reporting

- Capture the user action and the resulting state, not only the final response.
- HTTP proof includes the exact curl command, status code, response headers, and formatted JSON body.
- Mutation proof includes a secondary read through a separate API query or direct database inspection.
- Schema proof includes verification of the endpoint in `/openapi/v1.json`.
- Record the feature ID and endpoint path with every artifact.
- Report an unreachable path with the attempted command and the unmet precondition.
- Do not report a skipped entry point as verified through a different path.

## Feature entry contract

Each feature file starts with an H1 title and one paragraph describing the user-visible behavior.
It then uses exactly four H2 sections in this order:

1. `Sub-features` lists short identifiers with one line for each behavior.
2. `How to get to it (user POV)` lists every user entry point and HTTP endpoint.
3. `Driving it with HTTP (curl)` starts with `Preconditions:` and pairs each user action with an exact command and observable result.
4. `Gotchas` lists traps that can waste or invalidate a verification run.

Keep internal implementation details out of the map and document only observable user interactions and responses.

## Features

- [Authentication and Profile](./auth-and-profile.md) covers user registration, credential login, token refresh, and profile management.
- [Exams and Attempts](./exams-and-attempts.md) covers full Listening and Reading exam retrieval, starting attempts, saving answers, and submission.
- [Flashcards](./flashcards.md) covers deck catalog discovery, custom deck creation, card addition, study progress tracking, and match games.
- [Admin Writing and Users](./admin-writing-and-users.md) covers user administration, account locking, writing question bank management, and Excel import.
