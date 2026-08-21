$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

Write-Host "=== Workforce local stack verification ===" -ForegroundColor Cyan

# Refuse to overwrite uncommitted work. The script is allowed to update the local
# checkout only when the working tree is clean.
$status = git status --porcelain
if ($status) {
    Write-Host "Working tree is not clean. Commit or stash local changes before running this verifier." -ForegroundColor Red
    git status --short
    exit 1
}

git fetch origin main
$local = git rev-parse HEAD
$remote = git rev-parse origin/main

if ($local -ne $remote) {
    Write-Host "Local checkout is not at origin/main. Updating with fast-forward only..." -ForegroundColor Yellow
    git pull --ff-only origin main
}

Write-Host "Cleaning local .NET build outputs..." -ForegroundColor Yellow
Get-ChildItem -Path . -Directory -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -in @('bin','obj') } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Checking EF migration set..." -ForegroundColor Yellow
dotnet ef migrations list `
    --project .\backend\Workforce.Api\Workforce.Api.csproj `
    --startup-project .\backend\Workforce.Api\Workforce.Api.csproj

if ($LASTEXITCODE -ne 0) { throw "EF migration verification failed." }

$api = ".\backend\Workforce.Api\Workforce.Api.csproj"
$tests = ".\backend\Workforce.Api.Tests\Workforce.Api.Tests.csproj"

dotnet restore $tests
if ($LASTEXITCODE -ne 0) { throw "Backend restore failed." }

dotnet build $tests -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Backend build failed." }

dotnet test $tests -c Release --no-build --logger "console;verbosity=minimal"
if ($LASTEXITCODE -ne 0) { throw "Backend tests failed." }

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

docker compose down -v --remove-orphans
if ($LASTEXITCODE -ne 0) { throw "Docker cleanup failed." }

docker compose build --no-cache
if ($LASTEXITCODE -ne 0) { throw "Docker build failed." }

docker compose up -d
if ($LASTEXITCODE -ne 0) { throw "Docker Compose startup failed." }

$apiHealthy = $false
for ($i = 1; $i -le 60; $i++) {
    try {
        $response = Invoke-WebRequest "http://localhost:5080/health" -UseBasicParsing -TimeoutSec 3
        if ($response.StatusCode -eq 200) {
            $apiHealthy = $true
            break
        }
    }
    catch { }
    Start-Sleep -Seconds 2
}

if (-not $apiHealthy) {
    docker compose ps -a
    docker compose logs --no-color --tail=300 api sqlserver frontend
    throw "API health check failed."
}

$frontendHealthy = $false
for ($i = 1; $i -le 30; $i++) {
    try {
        $response = Invoke-WebRequest "http://localhost:8088/" -UseBasicParsing -TimeoutSec 3
        if ($response.StatusCode -eq 200) {
            $frontendHealthy = $true
            break
        }
    }
    catch { }
    Start-Sleep -Seconds 1
}

if (-not $frontendHealthy) {
    docker compose ps -a
    docker compose logs --no-color --tail=200 frontend api
    throw "Frontend health check failed."
}

Write-Host "" 
Write-Host "PASS: EF migrations, backend tests, frontend lint/build, Docker, API and frontend health all verified." -ForegroundColor Green
Write-Host "API:      http://localhost:5080/health" -ForegroundColor Cyan
Write-Host "Frontend: http://localhost:8088" -ForegroundColor Cyan
