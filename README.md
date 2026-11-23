# TOEICMaster Backend

API for the TOEIC learning and exam preparation application, organized with Clean Architecture and CQRS in a single application.

**Tech stack:** ASP.NET Core .NET 10, EF Core 10, Neon PostgreSQL, Redis, MinIO, and SMTP. MediatR 14.2.0, FluentValidation 12.1.1, and AutoMapper 16.2.0 are preconfigured.

## Prerequisites

- Git.
- .NET SDK 10, minimum 10.0.303.
- Docker Desktop running Linux containers, or Docker Engine with Compose v2.
- A Neon connection string provided separately by the team for each member.

Check your environment:

```sh
git --version
dotnet --version
docker info
docker compose version
```

## Local Machine Setup

1. Clone the backend repository and open a terminal in the repository directory:

    ```sh
    git clone <BACKEND_REPO_URL> toeicmaster-be
    cd toeicmaster-be
    ```

    Replace `<BACKEND_REPO_URL>` with the team's GitHub URL.

2. Create the configuration file:

    Windows PowerShell:

    ```powershell
    Copy-Item .env.example .env
    ```

    macOS/Linux:

    ```sh
    cp .env.example .env
    ```

    Open `.env`, replace `DATABASE_CONNECTION_STRING` with the team's Neon connection string, and keep the double quotes. Also replace `JWT_SIGNING_KEY` with an environment-specific random string of at least 32 characters. Fill in the SMTP settings from your email provider; the sender address must be verified with the provider:

    ```dotenv
    DATABASE_CONNECTION_STRING="Host=<NEON_HOST>;Database=<DATABASE>;Username=<USER>;Password=<PASSWORD>;SSL Mode=VerifyFull;Channel Binding=Require;"
    JWT_SIGNING_KEY=<RANDOM_SECRET_WITH_AT_LEAST_32_CHARACTERS>
    GOOGLE_CLIENT_ID=<GOOGLE_WEB_CLIENT_ID>.apps.googleusercontent.com
    EMAIL_VERIFICATION_HASH_KEY=<ANOTHER_RANDOM_SECRET_WITH_AT_LEAST_32_CHARACTERS>
    SMTP_HOST=<EXTERNAL_SMTP_HOST>
    SMTP_PORT=587
    SMTP_SECURITY=StartTls
    SMTP_FROM_ADDRESS=<VERIFIED_SENDER_ADDRESS>
    SMTP_FROM_NAME=TOEICMaster
    SMTP_USERNAME=<SMTP_USERNAME>
    SMTP_PASSWORD=<SMTP_PASSWORD_OR_APP_PASSWORD>
    ```

    This uses the Npgsql format, not a `postgresql://...` URL. The `.env` file is ignored by Git.

3. Start Redis and MinIO:

    ```sh
    docker compose up -d redis minio
    ```

4. Restore dependencies and run the API:

    ```sh
    dotnet tool restore
    dotnet restore TOEICMaster.slnx --locked-mode
    dotnet run --project src/TOEICMaster.Api
    ```

5. Verify in your browser:

    - `https://localhost:7287/scalar`: Scalar API Reference in the Development environment.
    - `https://localhost:7287/api/v1/system/ping`: API returns JSON.
    - `https://localhost:7287/health/ready`: returns `Healthy` when Neon, Redis, MinIO, and SMTP are reachable.
    - `https://localhost:7287/openapi/v1.json`: OpenAPI specification used by Scalar.

The `https` profile in `launchSettings.json` opens Scalar automatically. If your machine does not yet trust the HTTPS development certificate, run `dotnet dev-certs https --trust` once and restart the API.

The API, `dotnet ef`, and Package Manager Console automatically locate `.env` from the project/solution directory and map variables into .NET configuration. Actual environment variables in a deployment environment still take precedence over `.env`. Keep the default Redis/MinIO credentials during initial setup; do not commit `.env`, SMTP passwords, or app passwords. The SMTP account must be allowed to send from `SMTP_FROM_ADDRESS`.

## Run the Full Backend with Docker

After filling in `.env`, you can run the API and supporting services with a single command; this approach does not require installing the .NET SDK:

```sh
docker compose up -d --build
```

The API remains available at `http://localhost:5287`.

If the frontend also runs in Docker, create the shared network once and use the override in both repositories:

```sh
docker network create toeicmaster-local
docker compose -f docker-compose.yml -f docker-compose.shared.yml up -d --build
```

If the network already exists, skip the create command. The frontend will call the API via `http://backend:8080`.

## Local Services

| Service       | Address                | Purpose                     |
| ------------- | ---------------------- | --------------------------- |
| API (HTTPS)   | https://localhost:7287 | Backend and Scalar docs     |
| API (HTTP)    | http://localhost:5287  | Local frontend compatibility |
| Redis         | localhost:6379         | Cache                       |
| MinIO API     | http://localhost:9000  | Object storage              |
| MinIO console | http://localhost:9001  | Object storage viewer       |
| PostgreSQL    | Neon                   | Database outside Docker     |

## Local MinIO for L&R Media

The new L&R exam media workspace uses the private bucket `exam-media`. The browser does not receive access keys or secret keys: the API issues only a short-lived presigned PUT URL, the browser uploads directly, and then the API verifies the object before attaching the asset to a revision.

1. Install and start Docker Desktop, then confirm the daemon is running:

   ```sh
   docker version
   ```

2. Create `.env` from `.env.example` if it does not exist yet. Set `MINIO_ROOT_USER` and `MINIO_ROOT_PASSWORD` to dedicated local credentials; do not commit `.env`.

3. Start MinIO on its own and check its health:

   ```sh
   docker compose up -d minio
   docker compose ps minio
   curl http://localhost:9000/minio/health/live
   ```

   The local console is at `http://localhost:9001`. Use the credentials in `.env` to browse buckets; there is no need to create a public bucket or a public policy.

4. When running the API with `dotnet run`, `appsettings.Development.json` sets both `Minio:Endpoint` and `Minio:PublicEndpoint` to `localhost:9000`.

   When running the API inside Docker Compose, `Minio__Endpoint=minio:9000` is the internal hostname between containers, whereas `Minio__PublicEndpoint=localhost:9000` is the hostname embedded into presigned URLs for the browser. Keeping these two values distinct is required for direct browser uploads to work locally.

5. Run migrations only after verifying that the connection string points to the correct development database. The `AddAdminLrMediaWorkspace` migration only adds metadata/intents; it does not migrate legacy media objects:

   ```sh
   dotnet ef database update --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api
   ```

For future cloud environments, configure `Minio__Endpoint`, `Minio__PublicEndpoint`, `Minio__AccessKey`, `Minio__SecretKey`, and `Minio__UseSsl=true` using the endpoint/credentials of self-hosted MinIO, S3-compatible storage, or R2. The bucket must remain private and the public endpoint must be a hostname reachable by browsers.

The backend connects directly to the configured external SMTP provider. Therefore, emails sent by the application are delivered to real inboxes; there is no longer a fake mail service running in Docker Compose.

Information required from your email provider:

- SMTP hostname and port; the project's default configuration uses port `587` with `StartTls`.
- SMTP username and password (some providers refer to this password as an SMTP key or app password).
- Verified sender address; this value must match `SMTP_FROM_ADDRESS`.
- If the provider requires implicit TLS on port 465, set `SMTP_SECURITY=SslOnConnect` and `SMTP_PORT=465`.

After entering the credentials, call the registration endpoint with a real email address. The API returns `202`; the received email should contain a 6-digit OTP.
Never put SMTP keys into source code, logs, screenshots, or `.env.example`.

MinIO runs on a digest-pinned Chainguard Docker image from the same image provider used in the UGTM repository. The server software inside is MinIO Community; [upstream maintenance has been discontinued](https://github.com/minio/minio). This setup is intended for local development. Before deploying to production, the team must choose a storage solution with an appropriate update and support plan.

Check status, inspect logs, and stop services:

```sh
docker compose ps
docker compose logs -f backend
docker compose down
```

When the API runs via `dotnet run`, API logs appear in that terminal. Running `down` preserves data inside Docker volumes.

## Structure and Development

```text
src/
  TOEICMaster.Domain/          Entities and business rules
  TOEICMaster.Application/     Commands/queries, handlers, validators, DTOs, and interfaces
  TOEICMaster.Infrastructure/  EF Core, cache, storage, and SMTP
  TOEICMaster.Api/             HTTP endpoints, response mapping, error handling, and DI
tests/
  TOEICMaster.Tests/
    Architecture/              Checks dependency rules between layers
    Application/               Unit tests for use cases and the MediatR pipeline
    Infrastructure/            Tests for SMTP adapters, security, and configuration
    Api/                       Integration tests for HTTP contracts and middleware
    TestDoubles/               Shared fakes; contains no test cases
.github/                      CI, image publishing, and Issue templates
docs/                         Architecture documentation
```

[Architecture details](docs/foundations/architecture.md) and [API contract](docs/contracts/api-contract.md). The full documentation map is described in [docs/README.md](docs/README.md). APIs for registration, local/Google login, refresh, and logout are already implemented; TOEIC domain features are added according to the backlog.

The TOEIC Writing module (Part 1/2/3), test-taking flow, and AI grading are specified in detail in the [TOEIC Writing module spec](docs/features/writing/spec.md) and [Writing UAT runbook](docs/runbooks/writing-uat-runbook.md).

Auth endpoints enforce IP-based rate limits: sending email at 5 requests/minute, checking credentials at 10 requests/minute, and session operations at 30 requests/minute. Exceeding the limit returns `429` with `Retry-After`; see the API contract for details.

Controllers call MediatR's `ISender.Send`; Application handles use cases and references only Domain. Infrastructure implements Application interfaces for data access, caching, storage, and email. Domain is independent of frameworks and databases.

CQRS separates commands (writes/actions) from queries (reads); currently both still use a single Neon database. Existing patterns:

- **Query:** `System/Queries/GetSystemStatus` is called by the ping endpoint, preserving the contract with the FE.
- **Command:** `Email/Commands/SendEmail` has a handler that calls `IMailSender` and a validator that checks inputs before sending. There is no HTTP endpoint for sending email yet.
- **Validation:** `Common/Behaviors/ValidationBehavior` calls `ValidateAsync` before the handler; the API translates errors into `400 ValidationProblemDetails` with property-level `errors` and `traceId`.
- **Mapping:** `Api/Mapping/SystemProfile` maps Application DTOs to HTTP responses. When adding Domain → DTO mappings, place the profile in Application; DI scans both assemblies.

## MediatR and AutoMapper Licenses

The current version uses the Lucky Penny Software licensing mechanism. According to the [official FAQ](https://luckypennysoftware.com/faq), development, CI/CD, staging, and testing environments do not require a license key; academic coursework projects qualify for a free Community License. Teammates can set up and run locally without a key. When deploying for end users, register a key through [MediatR](https://mediatr.io/) or [AutoMapper](https://automapper.io/) and verify usage terms if the project scope changes.

If you already have a key, set `MEDIATR_LICENSE_KEY` and `AUTOMAPPER_LICENSE_KEY` in your local `.env` file. A bundle key can be used for both variables if the license covers both products. The libraries may log a warning about a missing key; the configuration does not suppress this warning. Do not commit keys to the repository.

## Code Verification

```sh
dotnet restore TOEICMaster.slnx --locked-mode
dotnet format TOEICMaster.slnx --verify-no-changes --no-restore
dotnet build TOEICMaster.slnx -c Release --no-restore
dotnet test TOEICMaster.slnx -c Release --no-build
```

Tests do not require real Neon credentials. When changing packages, run `dotnet restore TOEICMaster.slnx` to update lock files and commit them together with the changes.

## EF Core Migrations

The `InitialUsers` migration creates the accounts table; `AddUserSessions` creates the sessions table and refresh token hashes; `AddExternalIdentities` stores Google external identities; `AddEmailVerification` adds verification status and OTP challenges; `AddPasswordReset` adds OTP challenges and one-time reset tokens for the forgot-password flow.
For subsequent schema changes:

```sh
dotnet ef migrations add <MigrationName> --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api --output-dir Persistence/Migrations
```

Before updating the schema, set the direct connection string of the target Neon branch in `DATABASE_CONNECTION_STRING` inside `.env`; the migration hostname should not contain `-pooler`. Then run from the solution root:

```sh
dotnet ef database update --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api
```

In Package Manager Console, select `TOEICMaster.Infrastructure` as the Default project, keep `TOEICMaster.Api` as the Startup project, and run `Update-Database`; startup also reads the same `.env` file. Commit migrations together with entity/configuration changes. The API does not automatically apply migrations on startup.

## Seeding a Local Admin Account

Roles are stored in the `users.Role` column; normal user registrations always receive the `USER` role. To create an Admin for the Development environment, add the following variables to your local `.env` (do not commit this file):

```dotenv
ADMIN_SEED_EMAIL=admin@toeicmaster.local
ADMIN_SEED_PASSWORD=<password-8-to-128-characters>
ADMIN_SEED_DISPLAY_NAME=TOEICMaster Admin
```

After reviewing and applying migrations, run once from the solution root:

```powershell
dotnet run --project src/TOEICMaster.Api -- --seed-admin
```

This command only runs in `Development`, does not execute automatically on API startup, and never logs the password. If the email does not exist yet, it creates a verified/active local account with a BCrypt hash. If the email already belongs to an active account, it only promotes the role to `ADMIN` without changing the password. Emails belonging to pending-verification accounts are rejected to avoid bypassing ownership checks.
Sign out and sign back in after seeding to receive a new JWT/session with the `ADMIN` role; you can remove `ADMIN_SEED_PASSWORD` from `.env` afterward.

## Common Troubleshooting

- **Cannot connect to Docker:** start Docker Desktop, wait for the Engine to become ready, and check `docker info`.
- **Missing/invalid Postgres configuration:** check `DATABASE_CONNECTION_STRING` in `.env` and run commands from inside the solution directory tree so startup can find the file.
- **Readiness returns 503:** inspect services using `docker compose ps`, API logs, and Neon connectivity.
- **Port already in use:** stop the application using the port before running Compose.
- **SDK restore error:** install the .NET SDK 10 version matching `global.json`.
- **Missing MediatR/AutoMapper license warning:** no key is needed locally or in CI; see the licensing section when preparing a production deployment for users.

## CI and Team Workflow

The workflow is adapted from [UGTM](https://github.com/karasuma-2401/UGTM/tree/772b2157457bd9d4aabc316c770dfbd90ee79549) and runs CI on PRs/pushes to `dev` or `main`. Publishing images to GHCR runs only after CI succeeds on pushes from this repository itself.

Feature/fix branches open PRs into `dev`; releases go through PRs from `dev` into `main`. When setting up the GitHub repository, push workflows to the default `main` branch and allow Actions to publish packages with `GITHUB_TOKEN`. Azure deployment will be configured once the VPS is ready.

GitHub Actions are pinned by commit SHA and use Node 24 for JavaScript actions. NuGet caching includes lock files; restore uses `--locked-mode`, and dependency auditing includes transitive packages. Before exposing domain APIs to end users, complete authentication, authorization, and testing according to the corresponding stories.

<details>
<summary>UGTM Reference Configuration License (MIT)</summary>

```text
MIT License

Copyright (c) 2026 Win Transparent Pear

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

</details>
