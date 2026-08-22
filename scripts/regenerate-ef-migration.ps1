$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Set-Location (Join-Path $PSScriptRoot '..')

$project = 'backend/Workforce.Api/Workforce.Api.csproj'
$startup = 'backend/Workforce.Api/Workforce.Api.csproj'
$migrations = 'backend/Workforce.Api/Migrations'

Write-Host '=== Restore ===' -ForegroundColor Cyan
dotnet restore $project
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

Write-Host '=== Ensure EF CLI ===' -ForegroundColor Cyan
$tool = Get-Command dotnet-ef -ErrorAction SilentlyContinue
if (-not $tool) {
    dotnet tool install --global dotnet-ef --version 10.0.11
    if ($LASTEXITCODE -ne 0) { throw 'dotnet-ef installation failed.' }
}

dotnet ef --version
if ($LASTEXITCODE -ne 0) { throw 'dotnet-ef is unavailable.' }

Write-Host '=== Clean old migrations ===' -ForegroundColor Cyan
if (Test-Path $migrations) {
    Get-ChildItem $migrations -File | Remove-Item -Force
}
else {
    New-Item -ItemType Directory -Path $migrations | Out-Null
}

Write-Host '=== Generate InitialCreate ===' -ForegroundColor Cyan
dotnet ef migrations add InitialCreate --project $project --startup-project $startup --output-dir Migrations --context AppDbContext
if ($LASTEXITCODE -ne 0) { throw 'EF migration generation failed.' }

$files = Get-ChildItem $migrations -File | Select-Object -ExpandProperty Name
$designer = $files | Where-Object { $_ -match '^\d+_InitialCreate\.Designer\.cs$' }
$migration = $files | Where-Object { $_ -match '^\d+_InitialCreate\.cs$' }
$snapshot = $files | Where-Object { $_ -eq 'AppDbContextModelSnapshot.cs' }

if (($migration | Measure-Object).Count -ne 1 -or ($designer | Measure-Object).Count -ne 1 -or ($snapshot | Measure-Object).Count -ne 1) {
    Write-Host 'Generated migration files:' -ForegroundColor Red
    $files | ForEach-Object { Write-Host " - $_" }
    throw 'Migration generation did not produce exactly one InitialCreate migration, one Designer and one snapshot.'
}

Write-Host 'Migration files verified:' -ForegroundColor Green
Write-Host " - $migration"
Write-Host " - $designer"
Write-Host ' - AppDbContextModelSnapshot.cs'

Write-Host '=== Validate migration set ===' -ForegroundColor Cyan
$migrationList = dotnet ef migrations list --project $project --startup-project $startup --context AppDbContext
if ($LASTEXITCODE -ne 0) { throw 'EF migration validation failed.' }

if ($migrationList -notmatch 'InitialCreate') {
    $migrationList | Write-Host
    throw 'InitialCreate was not found in EF migration list.'
}

Write-Host 'InitialCreate found in EF migration list.' -ForegroundColor Green

Write-Host '=== Build backend ===' -ForegroundColor Cyan
dotnet build $project -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }

Write-Host '=== EF migration regeneration SUCCESS ===' -ForegroundColor Green
