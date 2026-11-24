#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"

DEFAULT_PORT=15000
DEFAULT_DB_PORT=15433
DEFAULT_DB_NAME="toeicmaster_test"
DEFAULT_DB_USER="toeicmaster_test"

state_file() {
  local port="${1:-$DEFAULT_PORT}"
  echo "/tmp/toeicmaster-be-verify-${port}/state.env"
}

cmd_launch() {
  local port="$DEFAULT_PORT"
  local db_port="$DEFAULT_DB_PORT"
  local skip_seeders="false"
  local seed_admin="true"

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --port) port="$2"; shift 2 ;;
      --db-port) db_port="$2"; shift 2 ;;
      --skip-seeders) skip_seeders="true"; shift ;;
      --no-seed-admin) seed_admin="false"; shift ;;
      *) echo "Unknown option: $1" >&2; return 1 ;;
    esac
  done

  local run_dir="/tmp/toeicmaster-be-verify-${port}"
  mkdir -p "$run_dir"
  local container_name="toeicmaster-verify-pg-${port}"

  echo "[control-be] Starting disposable Postgres container ${container_name} on port ${db_port}..."
  docker rm -f "$container_name" >/dev/null 2>&1 || true
  docker run -d --name "$container_name" \
    --label toeicmaster.verification="be-${port}" \
    -e POSTGRES_DB="$DEFAULT_DB_NAME" \
    -e POSTGRES_USER="$DEFAULT_DB_USER" \
    -e POSTGRES_HOST_AUTH_METHOD=trust \
    -p "127.0.0.1:${db_port}:5432" \
    postgres:16-alpine >/dev/null

  for attempt in {1..30}; do
    if docker exec "$container_name" pg_isready -h 127.0.0.1 -p 5432 -U "$DEFAULT_DB_USER" -d "$DEFAULT_DB_NAME" >/dev/null 2>&1; then
      break
    fi
    sleep 1
  done

  local conn_str="Host=127.0.0.1;Port=${db_port};Database=${DEFAULT_DB_NAME};Username=${DEFAULT_DB_USER}"

  echo "[control-be] Applying EF Core migrations to local container..."
  (
    cd "$REPO_ROOT"
    ConnectionStrings__Postgres="$conn_str" dotnet ef database update \
      --project src/TOEICMaster.Infrastructure \
      --startup-project src/TOEICMaster.Api \
      --configuration Release \
      --no-build >/dev/null
  )

  if [[ "$seed_admin" == "true" ]]; then
    echo "[control-be] Bootstrapping admin account in Development environment..."
    ASPNETCORE_ENVIRONMENT="Development" \
    ConnectionStrings__Postgres="$conn_str" \
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
    dotnet "$REPO_ROOT/src/TOEICMaster.Api/bin/Release/net10.0/TOEICMaster.Api.dll" --seed-admin >/dev/null
  fi

  echo "[control-be] Starting API server on http://127.0.0.1:${port}..."
  local log_file="${run_dir}/server.log"
  ASPNETCORE_ENVIRONMENT="Development" \
  ConnectionStrings__Postgres="$conn_str" \
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
  Startup__SkipSeeders="$skip_seeders" \
  Jwt__Issuer="TOEICMaster.Api" \
  Jwt__Audience="TOEICMaster.Client" \
  Jwt__SigningKey="test-only-signing-key-with-32-characters" \
  EmailVerification__HashKey="test-email-verification-key-32-characters" \
  ASPNETCORE_URLS="http://127.0.0.1:${port}" \
  setsid dotnet "$REPO_ROOT/src/TOEICMaster.Api/bin/Release/net10.0/TOEICMaster.Api.dll" \
    --urls "http://127.0.0.1:${port}" </dev/null >"$log_file" 2>&1 &
  local server_pid=$!
  disown "$server_pid" 2>/dev/null || true

  cat << STATE > "$(state_file "$port")"
PORT=${port}
DB_PORT=${db_port}
CONTAINER_NAME=${container_name}
SERVER_PID=${server_pid}
LOG_FILE=${log_file}
CONNECTION_STRING=${conn_str}
STATE

  echo "[control-be] Waiting for API readiness on http://127.0.0.1:${port}/health/live..."
  local ready=false
  for _ in {1..30}; do
    if curl -fs "http://127.0.0.1:${port}/health/live" >/dev/null 2>&1; then
      if curl -fs "http://127.0.0.1:${port}/openapi/v1.json" >/dev/null 2>&1; then
        ready=true
        break
      fi
    fi
    if ! kill -0 "$server_pid" 2>/dev/null; then
      echo "[control-be] Server process exited unexpectedly. Log:" >&2
      cat "$log_file" >&2
      return 1
    fi
    sleep 1
  done

  if [[ "$ready" != "true" ]]; then
    echo "[control-be] API failed to answer readiness checks. Log:" >&2
    cat "$log_file" >&2
    return 1
  fi

  echo "[control-be] TOEICMaster API is live and ready on http://127.0.0.1:${port} (PID: ${server_pid})"
}

cmd_doctor() {
  local port="$DEFAULT_PORT"
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --port) port="$2"; shift 2 ;;
      *) echo "Unknown option: $1" >&2; return 1 ;;
    esac
  done

  local sf
  sf="$(state_file "$port")"
  if [[ ! -f "$sf" ]]; then
    echo "{\"status\":\"not_running\",\"message\":\"State file not found at ${sf}\"}"
    return 1
  fi

  # shellcheck disable=SC1090
  source "$sf"

  local pid_alive="false"
  if kill -0 "$SERVER_PID" 2>/dev/null; then
    pid_alive="true"
  fi

  local container_running="false"
  if docker ps --filter "name=^/${CONTAINER_NAME}$" --format '{{.Names}}' | grep -q "$CONTAINER_NAME"; then
    container_running="true"
  fi

  local live_status
  live_status=$(curl -s -o /dev/null -w "%{http_code}" "http://127.0.0.1:${PORT}/health/live" || echo "000")
  local openapi_status
  openapi_status=$(curl -s -o /dev/null -w "%{http_code}" "http://127.0.0.1:${PORT}/openapi/v1.json" || echo "000")

  echo "{\"port\":${PORT},\"server_pid\":${SERVER_PID},\"pid_alive\":${pid_alive},\"container_running\":${container_running},\"health_live_http_status\":${live_status},\"openapi_http_status\":${openapi_status}}"
  if [[ "$pid_alive" == "true" && "$container_running" == "true" && "$live_status" == "200" && "$openapi_status" == "200" ]]; then
    return 0
  fi
  return 1
}

cmd_cleanup() {
  local port="$DEFAULT_PORT"
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --port) port="$2"; shift 2 ;;
      *) echo "Unknown option: $1" >&2; return 1 ;;
    esac
  done

  local sf
  sf="$(state_file "$port")"
  if [[ ! -f "$sf" ]]; then
    echo "[control-be] No state file found for port ${port}; nothing to clean."
    return 0
  fi

  # shellcheck disable=SC1090
  source "$sf"

  if [[ -n "${SERVER_PID:-}" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
    echo "[control-be] Stopping API server process (PID: ${SERVER_PID})..."
    kill -TERM "$SERVER_PID" 2>/dev/null || true
    for _ in {1..10}; do
      if ! kill -0 "$SERVER_PID" 2>/dev/null; then
        break
      fi
      sleep 0.5
    done
    if kill -0 "$SERVER_PID" 2>/dev/null; then
      kill -9 "$SERVER_PID" 2>/dev/null || true
    fi
  fi

  if [[ -n "${CONTAINER_NAME:-}" ]]; then
    echo "[control-be] Removing disposable Postgres container (${CONTAINER_NAME})..."
    docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true
  fi

  local run_dir="/tmp/toeicmaster-be-verify-${port}"
  rm -f "$sf"
  rm -f "${run_dir}/server.log"
  if [[ -d "$run_dir" ]]; then
    find "$run_dir" -mindepth 1 -maxdepth 1 ! -name "evidence" -exec rm -rf {} +
    rmdir "$run_dir" 2>/dev/null || true
  fi
  echo "[control-be] Cleanup finished for instance on port ${port}."
}

case "${1:-}" in
  launch) shift; cmd_launch "$@" ;;
  doctor) shift; cmd_doctor "$@" ;;
  cleanup) shift; cmd_cleanup "$@" ;;
  *)
    echo "Usage: $0 {launch|doctor|cleanup} [options]" >&2
    exit 1
    ;;
esac
