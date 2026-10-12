param(
    [string]$EnvFile = (Join-Path $PSScriptRoot '../.env'),
    [switch]$ValidateOnly,
    [string[]]$ApplicationArguments = @()
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $EnvFile -PathType Leaf)) {
    throw 'Local .env file not found. Copy .env.example to .env and configure it first.'
}

$taskSettings = @{}
$taskLineNumber = 0
foreach ($taskLine in [System.IO.File]::ReadAllLines((Resolve-Path -LiteralPath $EnvFile).Path)) {
    $taskLineNumber++
    $taskText = $taskLine.Trim()
    if (-not $taskText -or $taskText.StartsWith('#')) { continue }
    if ($taskText -notmatch '^(?:export\s+)?(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>.*)$') {
        throw "Invalid .env assignment at line $taskLineNumber."
    }
    $taskKey = $matches['key']
    $taskValue = $matches['value'].Trim()
    if ($taskValue.StartsWith('"') -or $taskValue.StartsWith("'")) {
        if ($taskValue -notmatch '^(?<quote>["''])(?<content>.*)\k<quote>\s*(?:#.*)?$') {
            throw "Invalid quoted .env value at line $taskLineNumber."
        }
        $taskValue = $matches['content']
    } else {
        $taskValue = ($taskValue -replace '\s+#.*$', '').Trim()
    }
    $taskSettings[$taskKey] = $taskValue
}

$taskOriginalEnvironment = @{}
function Set-LocalDefault([string]$Name, [string]$Value) {
    if ($null -eq [Environment]::GetEnvironmentVariable($Name, 'Process')) {
        $taskOriginalEnvironment[$Name] = $null
        [Environment]::SetEnvironmentVariable($Name, $Value, 'Process')
    }
}

try {
    foreach ($taskKey in $taskSettings.Keys) {
        Set-LocalDefault -Name $taskKey -Value $taskSettings[$taskKey]
    }

    $taskAliases = @{
        DATABASE_CONNECTION_STRING = 'ConnectionStrings__Postgres'
        MINIO_ROOT_USER = 'Minio__AccessKey'
        MINIO_ROOT_PASSWORD = 'Minio__SecretKey'
        MEDIATR_LICENSE_KEY = 'MediatR__LicenseKey'
        AUTOMAPPER_LICENSE_KEY = 'AutoMapper__LicenseKey'
        JWT_KEY = 'Jwt__Key'
        JWT_ISSUER = 'Jwt__Issuer'
        JWT_AUDIENCE = 'Jwt__Audience'
    }
    foreach ($taskAlias in $taskAliases.Keys) {
        $taskAliasValue = [Environment]::GetEnvironmentVariable($taskAlias, 'Process')
        if (-not [string]::IsNullOrWhiteSpace($taskAliasValue)) {
            Set-LocalDefault -Name $taskAliases[$taskAlias] -Value $taskAliasValue
        }
    }

    $taskRedisPassword = [Environment]::GetEnvironmentVariable('REDIS_PASSWORD', 'Process')
    if (-not [string]::IsNullOrWhiteSpace($taskRedisPassword)) {
        Set-LocalDefault -Name 'ConnectionStrings__Redis' -Value (
            "localhost:6379,password=$taskRedisPassword,abortConnect=false,connectTimeout=3000,syncTimeout=3000")
    }
    Set-LocalDefault -Name 'ASPNETCORE_ENVIRONMENT' -Value 'Development'

    $taskDatabase = [Environment]::GetEnvironmentVariable('ConnectionStrings__Postgres', 'Process')
    if ([string]::IsNullOrWhiteSpace($taskDatabase)) {
        throw 'Set DATABASE_CONNECTION_STRING in .env or ConnectionStrings__Postgres in the environment.'
    }
    if ($taskDatabase -match '^postgres(?:ql)?://') {
        throw 'Use an Npgsql connection string (Host=...;Database=...), rather than a PostgreSQL URL.'
    }

    $taskJwtKey = [Environment]::GetEnvironmentVariable('Jwt__Key', 'Process')
    if ([string]::IsNullOrWhiteSpace($taskJwtKey) -or $taskJwtKey.Length -lt 32) {
        throw 'Set JWT_KEY in .env or Jwt__Key in the environment to a random key of at least 32 characters.'
    }

    if ($ValidateOnly) {
        Write-Output 'PASS: Local .env parsed; database and JWT configuration mapped. No credentials displayed; no database connection opened.'
        return
    }

    $taskProject = Join-Path $PSScriptRoot '../src/TOEICMaster.Api'
    $global:LASTEXITCODE = 0
    & dotnet run --project $taskProject -- @ApplicationArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet run failed with exit code $LASTEXITCODE."
    }
} finally {
    foreach ($taskKey in $taskOriginalEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($taskKey, $taskOriginalEnvironment[$taskKey], 'Process')
    }
}
