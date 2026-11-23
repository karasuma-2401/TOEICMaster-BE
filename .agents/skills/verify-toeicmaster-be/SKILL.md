---
name: verify-toeicmaster-be
description: Launch, health-check, drive via HTTP, and verify TOEICMaster-BE ASP.NET Core REST API and OpenAPI surfaces against an isolated ephemeral database. Use when testing user-facing backend behavior or verifying API feature contracts.
---

# Verify TOEICMaster Backend

This skill provides deterministic verification for the TOEICMaster ASP.NET Core backend.
It operates against disposable local PostgreSQL instances and never touches Neon, production, or external databases.

## Launch

Start an isolated verification instance backed by a disposable Docker container.
The launch sequence creates the database container, applies all EF Core migrations, seeds sample exams, optionally bootstraps an admin account, and starts the compiled API server:

```bash
# 1. Build the release binaries if not already built
dotnet build TOEICMaster.slnx -c Release --no-restore

# 2. Launch the isolated verification stack on ports 15100 (API) and 15434 (PostgreSQL)
.agents/skills/verify-toeicmaster-be/scripts/control-be.sh launch --port 15100 --db-port 15434
```

To run launch manually without the helper:

```bash
# 1. Start disposable PostgreSQL container
docker run -d --name toeicmaster-verify-pg-15100 \
  --label toeicmaster.verification=be-15100 \
  -e POSTGRES_DB=toeicmaster_test \
  -e POSTGRES_USER=toeicmaster_test \
  -e POSTGRES_HOST_AUTH_METHOD=trust \
  -p 127.0.0.1:15434:5432 \
  postgres:16-alpine

# 2. Apply EF Core migrations to the isolated container
ConnectionStrings__Postgres="Host=127.0.0.1;Port=15434;Database=toeicmaster_test;Username=toeicmaster_test" \
  dotnet ef database update --project src/TOEICMaster.Infrastructure --startup-project src/TOEICMaster.Api --configuration Release --no-build

# 3. Bootstrap initial development admin account
ASPNETCORE_ENVIRONMENT="Development" \
ConnectionStrings__Postgres="Host=127.0.0.1;Port=15434;Database=toeicmaster_test;Username=toeicmaster_test" \
ConnectionStrings__Redis="127.0.0.1:1,abortConnect=false,connectTimeout=100,syncTimeout=100" \
Minio__Endpoint="127.0.0.1:1" \
Minio__PublicEndpoint="127.0.0.1:9000" \
Minio__AccessKey="test-access" \
Minio__SecretKey="test-secret" \
Smtp__Host="127.0.0.1" \
Smtp__Port="1" \
Smtp__FromAddress="noreply@toeicmaster.test" \
Smtp__Security="None" \
Google__ClientId="test-client.apps.googleusercontent.com" \
Jwt__SigningKey="test-only-signing-key-with-32-characters" \
EmailVerification__HashKey="test-email-verification-key-32-characters" \
AdminBootstrap__Email="admin@example.test" \
AdminBootstrap__Password="Password123!" \
AdminBootstrap__DisplayName="System Admin" \
dotnet src/TOEICMaster.Api/bin/Release/net10.0/TOEICMaster.Api.dll --seed-admin

# 4. Start the API server in background
ASPNETCORE_ENVIRONMENT="Development" \
ConnectionStrings__Postgres="Host=127.0.0.1;Port=15434;Database=toeicmaster_test;Username=toeicmaster_test" \
ConnectionStrings__Redis="127.0.0.1:1,abortConnect=false,connectTimeout=100,syncTimeout=100" \
Minio__Endpoint="127.0.0.1:1" \
Minio__PublicEndpoint="127.0.0.1:9000" \
Minio__AccessKey="test-access" \
Minio__SecretKey="test-secret" \
Smtp__Host="127.0.0.1" \
Smtp__Port="1" \
Smtp__FromAddress="noreply@toeicmaster.test" \
Smtp__Security="None" \
Google__ClientId="test-client.apps.googleusercontent.com" \
Startup__SkipSeeders="false" \
Jwt__Issuer="TOEICMaster.Api" \
Jwt__Audience="TOEICMaster.Client" \
Jwt__SigningKey="test-only-signing-key-with-32-characters" \
EmailVerification__HashKey="test-email-verification-key-32-characters" \
ASPNETCORE_URLS="http://127.0.0.1:15100" \
setsid dotnet src/TOEICMaster.Api/bin/Release/net10.0/TOEICMaster.Api.dll \
  --urls "http://127.0.0.1:15100" </dev/null >/tmp/toeicmaster-be-verify-15100/server.log 2>&1 &
```

Ready indicator:
The instance is ready when `GET /health/live` returns HTTP 200 with body `Healthy` and `GET /openapi/v1.json` returns HTTP 200 with the valid OpenAPI contract.

## Doctor

Execute the doctor command before driving to confirm instance health, process ownership, and endpoint accessibility:

```bash
.agents/skills/verify-toeicmaster-be/scripts/control-be.sh doctor --port 15100
```

Doctor output schema:

```json
{
  "port": 15100,
  "server_pid": 1082294,
  "pid_alive": true,
  "container_running": true,
  "health_live_http_status": 200,
  "openapi_http_status": 200
}
```

If `pid_alive` or `container_running` is false, inspect `/tmp/toeicmaster-be-verify-15100/server.log` before driving.

## Drive

Drive mapped features through authenticated HTTP client recipes described in `.agents/skills/verify-toeicmaster-be/features/`.
All requests use `http://127.0.0.1:15100` as the API base URL.

Standard flow pattern:

1. Obtain a Bearer token by signing in as admin or registering and activating a test user.
2. Send the target HTTP request with proper headers (`Authorization: Bearer <token>`, `Content-Type: application/json`).
3. Assert response status code, header contract, and JSON payload attributes.
4. Verify database side effects directly using `psql` or `docker exec` against the ephemeral container.

Example quick-drive:

```bash
# Obtain admin token
TOKEN=$(curl -s -X POST http://127.0.0.1:15100/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.test","password":"Password123!"}' | jq -r '.accessToken')

# Fetch admin user roster
curl -s -X GET http://127.0.0.1:15100/api/v1/admin/users \
  -H "Authorization: Bearer $TOKEN" | jq .
```

## Evidence

Store all captured outputs in an evidence directory outside the temporary runtime scratch path so evidence survives teardown.
The standard directory for verification evidence is `/home/phamhoangthai/Documents/project/TOEICMaster/.pipeline/poteto-correct-verify/outputs/be-evidence/`.

Every verified feature should produce:

1. An HTTP request and response trace showing status code, headers, and body.
2. An OpenAPI schema excerpt validating the endpoint contract.
3. A database confirmation query proving persistent state changes.
4. A transcript record linking the test action with its observable outcome.

## Cleanup

Cleanup terminates only the specific processes and containers initialized for that run.
It never runs destructive sweeps such as `pkill dotnet` and never touches external containers or Neon databases:

```bash
.agents/skills/verify-toeicmaster-be/scripts/control-be.sh cleanup --port 15100
```

To clean up manually:

```bash
# Read exact PID and container name from state file
source /tmp/toeicmaster-be-verify-15100/state.env

# Terminate exact server process
kill -TERM "$SERVER_PID" 2>/dev/null || true
docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true
rm -rf /tmp/toeicmaster-be-verify-15100
```

Confirm evidence files in the evidence output directory remain intact after cleanup completes.

## Helpers

The executable helper script is located at:
`.agents/skills/verify-toeicmaster-be/scripts/control-be.sh`

Supported commands:

```bash
# Launch verification stack
.agents/skills/verify-toeicmaster-be/scripts/control-be.sh launch [--port PORT] [--db-port DB_PORT] [--skip-seeders] [--no-seed-admin]

# Verify stack health
.agents/skills/verify-toeicmaster-be/scripts/control-be.sh doctor [--port PORT]

# Gracefully teardown verification stack
.agents/skills/verify-toeicmaster-be/scripts/control-be.sh cleanup [--port PORT]
```
