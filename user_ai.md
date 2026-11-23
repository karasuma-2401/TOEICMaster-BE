# Trace: User List and AI Grading for Speaking

Date: 2026-11-11. Branch: `feat/flowspeaking`. Changes are uncommitted.

## 1. Unable to Load User List

### Root Cause

BE was missing the endpoint called by FE.

| Step | Result |
| --- | --- |
| `/admin/users` page calls `getAdminUsers` | Requests Next.js `/api/admin/users` |
| Next.js forwards (`src/app/api/admin/users/route.ts`) | Calls BE `GET /api/v1/admin/users` |
| BE before fix | Returned `404`, no controller existed for this route |
| Closest existing BE endpoint | `GET /api/v1/accounts`, but has different parameters and response schema |

`/api/v1/accounts` cannot be substituted because it lacks `role`, `status`, `emailVerified`, `authProvider`, `lastLoginAt`, `stats`, and does not support search, filtering, or sorting. FE strictly validates the response structure and reports invalid data.

### Fix Applied

Added `GET /api/v1/admin/users`, requiring the Admin role. No migration, no new packages, and no FE changes.

New files:

- `src/TOEICMaster.Api/Controllers/AdminUsersController.cs`
- `src/TOEICMaster.Application/Accounts/IAdminUserReader.cs`
- `src/TOEICMaster.Application/Accounts/Queries/GetAdminUsers/GetAdminUsersQuery.cs`
- `src/TOEICMaster.Application/Accounts/Queries/GetAdminUsers/GetAdminUsersQueryValidator.cs`
- `src/TOEICMaster.Infrastructure/Persistence/Accounts/AdminUserReader.cs`
- `tests/TOEICMaster.Tests/Api/Accounts/AdminUsersApiTests.cs`
- `tests/TOEICMaster.Tests/Infrastructure/Persistence/AdminUserReaderTests.cs`
- `tests/TOEICMaster.Tests/TestDoubles/EmptyAdminUserReader.cs`

Modified files:

- `src/TOEICMaster.Infrastructure/DependencyInjection.cs`: added 1 registration line.
- `tests/TOEICMaster.Tests/Application/Common/MediatorPipelineTests.cs`: added 1 registration line.
- `docs/contracts/api-contract.md`, `docs/features/auth-profile/spec.md`: documented the endpoint.

### Data Derivation

The DB does not have dedicated columns for these values yet, so the endpoint derives them from existing data:

| Field | Derivation |
| --- | --- |
| `status = ACTIVE` | `IsActive = true` |
| `status = PENDING` | `IsActive = false` and email is unverified |
| `status = LOCKED` | `IsActive = false` and email is verified |
| `authProvider` | `GOOGLE` if no password is set; otherwise `PASSWORD` |
| `lastLoginAt` | Most recent session in `user_sessions`, excluding sessions created by token refresh |
| `avatarUrl` | Always `null`, since the avatar endpoint only serves the currently signed-in user. FE renders initials |

### Verification

- `dotnet restore --locked-mode`, `dotnet format --verify-no-changes`, `dotnet build -c Release`: passed, 0 warnings.
- `dotnet test -c Release`: 382/382 passed.
- Tested the reader against a temporary PostgreSQL 17 instance in Docker with sample data: search (including `%` characters), filtering, sorting, stats, pagination, and `lastLoginAt` all worked as expected. Temporary container and tests were removed.
- Running API: unauthenticated `GET /api/v1/admin/users` returns `401` (previously `404`), and the route is present in OpenAPI.
- Not yet verified: logging in with a real Admin account and viewing the page in the browser.

### Still Missing

FE also calls the following endpoints that BE does not have yet. Their corresponding buttons will fail with `404`:

- `POST /api/v1/admin/users` (create user)
- `GET`, `PATCH`, `DELETE /api/v1/admin/users/{id}`
- `POST`, `DELETE /api/v1/admin/users/{id}/lock`
- `POST /api/v1/admin/users/{id}/reset-password`

The `User` domain model does not currently have an account lock concept, so `lock` requires a design decision before implementation.

## 2. AI Grading for Speaking Not Running

### Root Cause

Missing environment configuration, not a code bug. `GET /api/v1/speaking/evaluation-availability` returned `{"available":false}`.

`SpeakingEvaluationSettings.IsConfigured` requires all 4 values:

| Requirement | Current Status |
| --- | --- |
| `AzureSpeech:Enabled = true` | Missing, `.env` has no Azure variables |
| `AzureSpeech:Key` | Missing |
| `Gemini:Enabled = true` | Met |
| `Gemini:ApiKey` | Met, test call to `gemini-2.5-flash-lite` returned `200` |

Consequence: FE sees `available = false` and disables grading, while the `evaluation-consent` endpoint returns "Speaking AI grading service is not configured".

A second issue will surface as soon as the key is added: `ffmpeg` is not installed on this machine. The worker needs `ffmpeg` to convert recordings to WAV; if missing, grading jobs fail (`FAILED`) with code `AUDIO_CONVERTER_UNAVAILABLE`.

Note on variable names: when running via `dotnet run`, you must use `AzureSpeech__Enabled`, `AzureSpeech__Key`, and `AzureSpeech__Region`. The `AZURE_SPEECH_*` names only take effect through `docker-compose.override.yml`, because `DotEnvConfiguration` does not map those names.

### Steps to Enable

1. Create an Azure Speech resource (the free F0 tier is enough for testing, region `southeastasia`), and copy the Key.
2. Add to `.env`:

   ```sh
   AzureSpeech__Enabled=true
   AzureSpeech__Key=<AZURE_SPEECH_KEY>
   AzureSpeech__Region=southeastasia
   ```

3. Install `ffmpeg`: `brew install ffmpeg`.
4. Restart the API and verify `evaluation-availability` returns `{"available":true}`.

The Azure integration has never been tested with a live key (`notespeaking.md`), so a test attempt should be graded after enabling it.

## 3. Additional Notes

- `/health/ready` still returns `503` due to SMTP: the TLS certificate of `smtp.gmail.com` is rejected on this machine. Sign-up and forgot-password flows cannot send emails.
- API logs show `Bucket "writing-images" is not found`. The local MinIO volume only has `speaking-media` and `speaking-recordings`, so Writing test images stored in the shared DB are missing on this machine. This error pre-dates the MinIO image change.
- Local MinIO is running on port 9100 using the `minio/minio:latest` image, because the Chainguard image in compose failed with `exec format error` on this machine.
