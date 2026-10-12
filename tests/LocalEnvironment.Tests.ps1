$ErrorActionPreference = 'Stop'
$taskRunner = Join-Path $PSScriptRoot '../scripts/run-local.ps1'
if (-not (Test-Path -LiteralPath $taskRunner)) { throw 'The local .env runner has not been implemented.' }

$taskTestDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('toeic-env-test-' + [Guid]::NewGuid())
[System.IO.Directory]::CreateDirectory($taskTestDirectory) | Out-Null
$taskFixture = Join-Path $taskTestDirectory '.env'
$taskNames = @('DATABASE_CONNECTION_STRING', 'ConnectionStrings__Postgres', 'REDIS_PASSWORD',
    'ConnectionStrings__Redis', 'MINIO_ROOT_USER', 'MINIO_ROOT_PASSWORD', 'Minio__AccessKey',
    'Minio__SecretKey', 'MEDIATR_LICENSE_KEY', 'MediatR__LicenseKey', 'ASPNETCORE_ENVIRONMENT',
    'JWT_KEY', 'JWT_ISSUER', 'JWT_AUDIENCE', 'Jwt__Key', 'Jwt__Issuer', 'Jwt__Audience')
$taskPrevious = @{}
foreach ($taskName in $taskNames) {
    $taskPrevious[$taskName] = [Environment]::GetEnvironmentVariable($taskName, 'Process')
    [Environment]::SetEnvironmentVariable($taskName, $null, 'Process')
}
$taskOriginalDotnet = Get-Item Function:global:dotnet -ErrorAction SilentlyContinue
$taskPreviousExitCode = $global:LASTEXITCODE

function Assert-LocalSetting($condition, [string]$message) {
    if (-not $condition) { throw $message }
}

try {
    function global:dotnet {
        $global:ToeicEnvTestObservation = @{}
        foreach ($taskName in @('ConnectionStrings__Postgres', 'ConnectionStrings__Redis',
            'Minio__AccessKey', 'Minio__SecretKey', 'MediatR__LicenseKey', 'ASPNETCORE_ENVIRONMENT',
            'Jwt__Key', 'Jwt__Issuer', 'Jwt__Audience')) {
            $global:ToeicEnvTestObservation[$taskName] = [Environment]::GetEnvironmentVariable($taskName, 'Process')
        }
        $global:LASTEXITCODE = 0
    }

    @'
# Only fake test credentials are used here.
DATABASE_CONNECTION_STRING="Host=localhost;Database=test;Username=test;Password=space = # value" # comment
REDIS_PASSWORD=test-redis
MINIO_ROOT_USER=test-access
MINIO_ROOT_PASSWORD='test # secret'
export MEDIATR_LICENSE_KEY=test-license # comment
JWT_KEY=test-only-random-looking-key-for-config-tests
JWT_ISSUER=TestIssuer
JWT_AUDIENCE=TestAudience
'@ | Set-Content -LiteralPath $taskFixture -Encoding UTF8
    & $taskRunner -EnvFile $taskFixture
    Assert-LocalSetting ($global:ToeicEnvTestObservation['ConnectionStrings__Postgres'].EndsWith('Password=space = # value')) 'Quoted database credential was altered.'
    Assert-LocalSetting ($global:ToeicEnvTestObservation['ConnectionStrings__Redis'] -match 'localhost:6379,password=test-redis,') 'Local Redis configuration was not mapped.'
    Assert-LocalSetting ($global:ToeicEnvTestObservation['Minio__AccessKey'] -eq 'test-access') 'MinIO access key was not mapped.'
    Assert-LocalSetting ($global:ToeicEnvTestObservation['Minio__SecretKey'] -eq 'test # secret') 'Single-quoted credential was altered.'
    Assert-LocalSetting ($global:ToeicEnvTestObservation['MediatR__LicenseKey'] -eq 'test-license') 'Export or inline comment was not handled.'
    Assert-LocalSetting ($global:ToeicEnvTestObservation['ASPNETCORE_ENVIRONMENT'] -eq 'Development') 'Local runner did not select Development.'
    Assert-LocalSetting ($global:ToeicEnvTestObservation['Jwt__Key'] -eq 'test-only-random-looking-key-for-config-tests') 'JWT signing key was not mapped.'
    Assert-LocalSetting ($global:ToeicEnvTestObservation['Jwt__Issuer'] -eq 'TestIssuer') 'JWT issuer was not mapped.'
    Assert-LocalSetting ($global:ToeicEnvTestObservation['Jwt__Audience'] -eq 'TestAudience') 'JWT audience was not mapped.'
    Assert-LocalSetting ($null -eq [Environment]::GetEnvironmentVariable('ConnectionStrings__Postgres', 'Process')) 'Runner leaked credentials into the parent environment.'

    $env:ConnectionStrings__Postgres = 'Host=existing;Database=existing'
    $env:REDIS_PASSWORD = 'existing-redis'
    & $taskRunner -EnvFile $taskFixture
    Assert-LocalSetting ($global:ToeicEnvTestObservation['ConnectionStrings__Postgres'] -eq $env:ConnectionStrings__Postgres) 'Existing database environment configuration was overwritten.'
    Assert-LocalSetting ($global:ToeicEnvTestObservation['ConnectionStrings__Redis'] -match 'password=existing-redis,') 'An existing Redis password was overwritten.'
    Assert-LocalSetting ($env:ConnectionStrings__Postgres -eq 'Host=existing;Database=existing') 'Existing database configuration was not restored.'

    'DATABASE_CONNECTION_STRING="unterminated' | Set-Content -LiteralPath $taskFixture -Encoding UTF8
    $taskRejected = $false
    try { & $taskRunner -EnvFile $taskFixture } catch { $taskRejected = $true }
    Assert-LocalSetting $taskRejected 'Malformed quoted .env content was accepted.'

    @'
DATABASE_CONNECTION_STRING=Host=test
JWT_KEY=test-only-random-looking-key-for-config-tests
'@ | Set-Content -LiteralPath $taskFixture -Encoding UTF8
    function global:dotnet { $global:LASTEXITCODE = 7 }
    $taskRejected = $false
    try { & $taskRunner -EnvFile $taskFixture } catch { $taskRejected = $true }
    Assert-LocalSetting $taskRejected 'A failed dotnet process was reported as success.'
    Assert-LocalSetting ($env:ConnectionStrings__Postgres -eq 'Host=existing;Database=existing') 'Environment was not restored after dotnet failed.'

    Write-Output 'PASS: .env quoting, aliases, existing environment precedence, environment restoration, malformed input, and process failure.'
} finally {
    foreach ($taskName in $taskNames) {
        [Environment]::SetEnvironmentVariable($taskName, $taskPrevious[$taskName], 'Process')
    }
    if ($taskOriginalDotnet) { Set-Item Function:global:dotnet $taskOriginalDotnet.ScriptBlock }
    else { Remove-Item Function:global:dotnet -ErrorAction SilentlyContinue }
    Remove-Variable ToeicEnvTestObservation -Scope Global -ErrorAction SilentlyContinue
    $global:LASTEXITCODE = $taskPreviousExitCode
    # The temporary fixture contains fake data and is confined to this generated directory.
    Remove-Item -LiteralPath $taskFixture -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $taskTestDirectory -Force -ErrorAction SilentlyContinue
}
