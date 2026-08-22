$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$project = 'backend/Workforce.Api/Workforce.Api.csproj'
$startup = 'backend/Workforce.Api/Workforce.Api.csproj'
$migrations = 'backend/Workforce.Api/Migrations'

if (Test-Path $migrations) {
    Get-ChildItem $migrations -File | Remove-Item -Force
}

$tool = Get-Command dotnet-ef -ErrorAction SilentlyContinue
if (-not $tool) {
    dotnet tool install --global dotnet-ef --version 10.0.11
}

dotnet restore $project
dotnet ef migrations add InitialCreate --project $project --startup-project $startup --output-dir Migrations --context AppDbContext
if ($LASTEXITCODE -ne 0) { throw 'EF migration generation failed.' }

dotnet ef migrations list --project $project --startup-project $startup --context AppDbContext
if ($LASTEXITCODE -ne 0) { throw 'EF migration validation failed.' }

dotnet build $project -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }
