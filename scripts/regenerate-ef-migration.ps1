$ErrorActionPreference = 'Stop'
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

$required = @(
    '20260822100000_InitialCreate.cs',
    '20260822100000_InitialCreate.Designer.cs',
    'AppDbContextModelSnapshot.cs'
)

$files = Get-ChildItem $migrations -File | Select-Object -ExpandProperty Name
$missing = $required | Where-Object { $_ -notin $files }

if ($missing.Count -gt 0) {
    Write-Host 'Generated migration files:' -ForegroundColor Red
    $files | ForEach-Object { Write-Host " - $_" }
    throw ('Migration generation completed without required files: ' + ($missing -join ', '))
}

Write-Host 'Migration files verified.' -ForegroundColor Green

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
