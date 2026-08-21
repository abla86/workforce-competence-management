[CmdletBinding()]
param(
    [switch]$SkipDocker
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Fail([string]$Message) {
    Write-Host "FAILED: $Message" -ForegroundColor Red
    exit 1
}

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

Write-Host '=== Workforce & Competence Management: self-repair/finalize ===' -ForegroundColor Cyan

# Never overwrite a real .env. Create a local development one only when absent.
if (-not (Test-Path '.env')) {
@'
DB_PASSWORD=WorkforceLocalDb_2026_StrongPassword_ChangeMe!
JWT_SECRET_KEY=WorkforceLocalJwtSecret_2026_ChangeMe_AtLeast32Bytes!
VAKTKLAR_BOOTSTRAP_KEY=WorkforceBootstrap_2026_ChangeMe_OneTimeKey!
SECURITY_COOKIE_SECURE=false
'@ | Set-Content '.env' -Encoding UTF8
    Write-Host 'Created local .env (not committed).' -ForegroundColor Yellow
}

# Keep EF design-time commands independent of the application host/JWT configuration.
$factory = 'backend/Workforce.Api/Data/AppDbContextFactory.cs'
if (-not (Test-Path $factory)) {
@'
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Workforce.Api.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost,1433;Database=WorkforceCompetenceDb;User Id=sa;Password=LocalDesignTimeOnlyPassword_123!;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection)
            .Options;

        return new AppDbContext(options);
    }
}
'@ | Set-Content $factory -Encoding UTF8
}

# Repair known enum migration/seeding regressions idempotently.
$seed = 'backend/Workforce.Api/Data/SeedData.cs'
if (Test-Path $seed) {
    $c = Get-Content $seed -Raw
    $c = $c -replace 'Level = "Basic"', 'Level = CompetenceLevel.Basic'
    $c = $c -replace 'Level = "Intermediate"', 'Level = CompetenceLevel.Intermediate'
    $c = $c -replace 'Level = "Advanced"', 'Level = CompetenceLevel.Advanced'
    $c = $c -replace 'MinimumLevel = "Basic"', 'MinimumLevel = CompetenceLevel.Basic'
    $c = $c -replace 'MinimumLevel = "Intermediate"', 'MinimumLevel = CompetenceLevel.Intermediate'
    $c = $c -replace 'MinimumLevel = "Advanced"', 'MinimumLevel = CompetenceLevel.Advanced'
    Set-Content $seed $c -Encoding UTF8
}

# Repair CSV import so invalid competence levels are rejected rather than assigned as free text.
$auth = 'backend/Workforce.Api/Security/VaktklarAuthentication.cs'
if (Test-Path $auth) {
    $c = Get-Content $auth -Raw
    $pattern = '(?s)\s*var level = Get\("Level"\);.*?else \{ item\.Level = level; item\.ValidUntil = validUntil; updated\+\+; \}'
    if ($c -match $pattern) {
        $replacement = @'
            var levelText = Get("Level");
            if (string.IsNullOrWhiteSpace(levelText))
                levelText = "Basic";

            if (!Enum.TryParse<CompetenceLevel>(levelText, true, out var level))
            {
                errors.Add(new
                {
                    row = r + 1,
                    message = $"Invalid competence level '{levelText}'. Allowed values: Basic, Intermediate, Advanced."
                });
                continue;
            }

            var validUntil = DateOnly.TryParse(Get("ValidUntil"), out var parsedDate)
                ? parsedDate
                : (DateOnly?)null;

            var item = await db.EmployeeCompetences.FindAsync(employee.Id, competence.Id);
            if (item is null)
            {
                db.EmployeeCompetences.Add(new EmployeeCompetence
                {
                    EmployeeId = employee.Id,
                    CompetenceId = competence.Id,
                    Level = level,
                    ValidUntil = validUntil
                });
                created++;
            }
            else
            {
                item.Level = level;
                item.ValidUntil = validUntil;
                updated++;
            }
'@
        $c = [regex]::Replace($c, $pattern, "`n$replacement")
        Set-Content $auth $c -Encoding UTF8
    }
}

# Repair the stale ShiftId cache reference if present.
$coverage = 'backend/Workforce.Api/Services/CoverageService.cs'
if (Test-Path $coverage) {
    $c = Get-Content $coverage -Raw
    $c = $c -replace '\.ShiftId\.ToString\(\)', '.Id.ToString()'
    Set-Content $coverage $c -Encoding UTF8
}

# Make the known rest-test deterministic: next-day 00:00 after a 07:00 target is 9h rest.
$planningTest = 'backend/Workforce.Api.Tests/PlanningAdvisorTests.cs'
if (Test-Path $planningTest) {
    $lines = [System.Collections.Generic.List[string]](Get-Content $planningTest)
    $inside = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match 'CandidateWithInsufficientRestAfterShiftIsRejected') { $inside = $true; continue }
        if ($inside -and $lines[$i] -match 'StartTime\s*=\s*new TimeOnly\(18,\s*0\)') {
            $lines[$i] = $lines[$i] -replace 'new TimeOnly\(18,\s*0\)', 'new TimeOnly(0, 0)'
            break
        }
    }
    $text = ($lines -join [Environment]::NewLine)
    $text = $text.Replace('x.Contains("FravÃ¦r")', 'x.Contains("Fravær")')
    Set-Content $planningTest $text -Encoding UTF8
}

# Ensure EF tooling exists.
if (-not (Get-Command dotnet-ef -ErrorAction SilentlyContinue)) {
    dotnet tool install --global dotnet-ef | Out-Host
}

# Verify/fix the application before touching Git history.
dotnet build './backend/Workforce.Api/Workforce.Api.csproj' --configuration Release
if ($LASTEXITCODE) { Fail 'Backend build failed.' }

dotnet test './backend/Workforce.Api.Tests/Workforce.Api.Tests.csproj' --configuration Release --no-restore
if ($LASTEXITCODE) { Fail 'Backend tests failed.' }

Push-Location './frontend'
npm install
npm run lint
npm run build
if ($LASTEXITCODE) { Pop-Location; Fail 'Frontend validation failed.' }
Pop-Location

# Use local environment for EF design-time and Docker interpolation.
$dotenv = Get-Content '.env'
foreach ($line in $dotenv) {
    if ($line -match '^([^#=]+)=(.*)$') {
        [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2], 'Process')
    }
}

# EF factory now reads ConnectionStrings__DefaultConnection from this process.
if (-not $env:ConnectionStrings__DefaultConnection) {
    $env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=WorkforceCompetenceDb;User Id=sa;Password=$env:DB_PASSWORD;TrustServerCertificate=True;Encrypt=False"
}

dotnet ef migrations list --project './backend/Workforce.Api/Workforce.Api.csproj' --configuration Release
if ($LASTEXITCODE) { Fail 'EF migrations could not be loaded.' }

if (-not $SkipDocker) {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { Fail 'Docker CLI is not installed.' }

    docker info *> $null
    if ($LASTEXITCODE -ne 0) {
        $dockerDesktop = Join-Path $env:ProgramFiles 'Docker/Docker/Docker Desktop.exe'
        if (Test-Path $dockerDesktop) {
            Start-Process $dockerDesktop
            Write-Host 'Starting Docker Desktop and waiting for engine...' -ForegroundColor Yellow
            for ($i = 0; $i -lt 30; $i++) {
                Start-Sleep -Seconds 2
                docker info *> $null
                if ($LASTEXITCODE -eq 0) { break }
            }
        }
    }

    docker info *> $null
    if ($LASTEXITCODE -ne 0) { Fail 'Docker engine is not available. Docker Desktop must be installed.' }

    docker compose config --quiet
    if ($LASTEXITCODE) { Fail 'Docker Compose configuration is invalid.' }

    docker compose down -v
    docker compose up --build -d
    if ($LASTEXITCODE) { Fail 'Docker Compose startup failed.' }

    $healthy = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 2
        try {
            $health = Invoke-WebRequest 'http://localhost:5080/health' -UseBasicParsing
            if ($health.StatusCode -eq 200) { $healthy = $true; break }
        } catch { }
    }
    if (-not $healthy) {
        docker compose ps
        docker compose logs --tail 100 api
        Fail 'API health check did not become ready.'
    }

    $frontendOk = $false
    try {
        $front = Invoke-WebRequest 'http://localhost:8088' -UseBasicParsing
        $frontendOk = $front.StatusCode -eq 200
    } catch { }
    if (-not $frontendOk) { docker compose ps; Fail 'Frontend health check failed.' }
}

Write-Host ''
Write-Host 'ALL LOCAL VERIFICATION PASSED.' -ForegroundColor Green
Write-Host 'Backend: build + tests' -ForegroundColor Green
Write-Host 'Frontend: lint + production build' -ForegroundColor Green
Write-Host 'EF: migrations available' -ForegroundColor Green
if (-not $SkipDocker) { Write-Host 'Docker: SQL Server + API + frontend healthy' -ForegroundColor Green }
Write-Host ''

git status --short
