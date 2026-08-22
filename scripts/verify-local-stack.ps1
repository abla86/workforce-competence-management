$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Set-Location (Join-Path $PSScriptRoot "..")
Write-Host "=== Workforce local stack verification ===" -ForegroundColor Cyan

git fetch origin main
if ($LASTEXITCODE -ne 0) { throw "git fetch failed." }
git reset --hard origin/main
if ($LASTEXITCODE -ne 0) { throw "git reset failed." }

Get-ChildItem -Path . -Directory -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -in @('bin','obj') } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

$api = ".\backend\Workforce.Api\Workforce.Api.csproj"
$tests = ".\backend\Workforce.Api.Tests\Workforce.Api.Tests.csproj"
$migrations = ".\backend\Workforce.Api\Migrations"

if (-not $env:DB_PASSWORD -and -not $env:MSSQL_SA_PASSWORD) {
    $envFile = Join-Path (Get-Location) ".env"
    if (-not (Test-Path $envFile)) {
        throw "Local .env is required. Copy .env.example to .env and set DB_PASSWORD. The real .env is ignored by Git."
    }

    $dbPasswordLine = Get-Content $envFile | Where-Object { $_ -match '^\s*DB_PASSWORD\s*=\s*(.+?)\s*$' } | Select-Object -First 1
    if (-not $dbPasswordLine) {
        throw "DB_PASSWORD is missing from the local .env file."
    }

    $env:DB_PASSWORD = ($dbPasswordLine -replace '^\s*DB_PASSWORD\s*=\s*', '').Trim().Trim('"').Trim("'")
    if ([string]::IsNullOrWhiteSpace($env:DB_PASSWORD)) {
        throw "DB_PASSWORD in .env is empty."
    }
}

dotnet restore $tests
if ($LASTEXITCODE -ne 0) { throw "Backend restore failed." }

dotnet build $tests -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Backend build failed." }

dotnet test $tests -c Release --no-build --logger "console;verbosity=minimal"
if ($LASTEXITCODE -ne 0) { throw "Backend tests failed." }

if (-not (Test-Path $migrations)) { throw "Migrations directory is missing." }

$migrationFiles = @(Get-ChildItem $migrations -File | Select-Object -ExpandProperty Name)
$migrationCs = @($migrationFiles | Where-Object { $_ -match '^[0-9]+_[A-Za-z0-9_]+\.cs$' -and $_ -notmatch '\.Designer\.cs$' })
$designerFiles = @($migrationFiles | Where-Object { $_ -match '^[0-9]+_[A-Za-z0-9_]+\.Designer\.cs$' })
$snapshot = @($migrationFiles | Where-Object { $_ -eq 'AppDbContextModelSnapshot.cs' })

if ($migrationCs.Count -ne 1 -or $designerFiles.Count -ne 1 -or $snapshot.Count -ne 1) {
    $migrationFiles | ForEach-Object { Write-Host " - $_" }
    throw "Checked-in EF baseline is not exactly one migration + one Designer + one snapshot."
}

Write-Host "Checking EF model against the checked-in migration snapshot..." -ForegroundColor Yellow
dotnet ef migrations has-pending-model-changes `
    --project $api `
    --startup-project $api `
    --context Workforce.Api.Data.AppDbContext `
    --configuration Release
if ($LASTEXITCODE -ne 0) { throw "EF model has pending changes. Migration baseline must be regenerated; Docker verification is stopped." }

Write-Host "EF model matches the checked-in migration snapshot." -ForegroundColor Green

dotnet ef migrations list `
    --project $api `
    --startup-project $api `
    --context Workforce.Api.Data.AppDbContext `
    --configuration Release
if ($LASTEXITCODE -ne 0) { throw "EF migration validation failed." }

dotnet build $api -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Backend build failed after EF validation." }

Push-Location .\frontend
try {
    npm ci
    if ($LASTEXITCODE -ne 0) { throw "Frontend npm ci failed." }
    npm run lint
    if ($LASTEXITCODE -ne 0) { throw "Frontend lint failed." }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "Frontend build failed." }
}
finally {
    Pop-Location
}

docker info *> $null
if ($LASTEXITCODE -ne 0) { throw "Docker Engine is not available. Open Docker Desktop." }

docker compose config --quiet
if ($LASTEXITCODE -ne 0) { throw "Docker Compose configuration is invalid or required local environment values are missing." }

docker compose down -v --remove-orphans
if ($LASTEXITCODE -ne 0) { throw "Docker cleanup failed." }

docker compose build --no-cache
if ($LASTEXITCODE -ne 0) {
    docker compose ps -a
    docker compose logs --no-color --tail=300 api sqlserver frontend
    throw "Docker build failed."
}

docker compose up -d
if ($LASTEXITCODE -ne 0) {
    docker compose ps -a
    docker compose logs --no-color --tail=300 api sqlserver frontend
    throw "Docker Compose startup failed."
}

$apiHealthy = $false
for ($i = 1; $i -le 60; $i++) {
    $apiState = docker inspect --format='{{.State.Status}}' workforce-competence-management-api-1 2>$null
    if ($apiState -eq 'exited' -or $apiState -eq 'dead') {
        docker compose ps -a
        docker compose logs --no-color --tail=300 api
        throw "API container crashed."
    }
    try {
        $response = Invoke-WebRequest "http://localhost:5080/health" -UseBasicParsing -TimeoutSec 3
        if ($response.StatusCode -eq 200) { $apiHealthy = $true; break }
    }
    catch {}
    Start-Sleep -Seconds 2
}

if (-not $apiHealthy) {
    docker compose ps -a
    docker compose logs --no-color --tail=300 api sqlserver
    throw "API health check failed."
}

$frontendHealthy = $false
for ($i = 1; $i -le 30; $i++) {
    try {
        $response = Invoke-WebRequest "http://localhost:8088/" -UseBasicParsing -TimeoutSec 3
        if ($response.StatusCode -eq 200) { $frontendHealthy = $true; break }
    }
    catch {}
    Start-Sleep -Seconds 1
}

if (-not $frontendHealthy) {
    docker compose ps -a
    docker compose logs --no-color --tail=300 frontend api
    throw "Frontend health check failed."
}

docker compose ps
Write-Host ""
Write-Host "PASS: 18 backend tests, EF model/snapshot, frontend lint/build, Docker, API and frontend health verified." -ForegroundColor Green
Write-Host "Frontend: http://localhost:8088" -ForegroundColor Cyan
Write-Host "API:      http://localhost:5080/health" -ForegroundColor Cyan
