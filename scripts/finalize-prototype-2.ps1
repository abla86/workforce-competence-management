[CmdletBinding()]
param([switch]$SkipDocker)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
function Fail([string]$Message){ Write-Host "FAILED: $Message" -ForegroundColor Red; exit 1 }
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path; Set-Location $root

# Local demo credentials. These are only for the runnable prototype environment.
$defaultDbPassword='WorkforceLocalDb_2026_StrongPassword_ChangeMe!'
$defaultJwtSecret='WorkforceLocalJwtSecret_2026_ChangeMe_AtLeast32Bytes_9X7K4M2P8Q6R5T3Y1'
$defaultBootstrap='WorkforceBootstrap_2026_ChangeMe_OneTimeKey_9X7K4M2P8Q6R5T3Y1'

if(-not(Test-Path '.env')){
@"
DB_PASSWORD=$defaultDbPassword
JWT_SECRET_KEY=$defaultJwtSecret
VAKTKLAR_BOOTSTRAP_KEY=$defaultBootstrap
SECURITY_COOKIE_SECURE=false
"@ | Set-Content '.env' -Encoding UTF8
}

# Load .env and repair missing/invalid local prototype secrets deterministically.
$dotenv=@{}
foreach($line in (Get-Content '.env')){
    if($line -match '^\s*([^#=\s]+)\s*=\s*(.*)\s*$'){$dotenv[$Matches[1]]=$Matches[2]}
}
if(-not $dotenv.ContainsKey('DB_PASSWORD') -or [string]::IsNullOrWhiteSpace($dotenv['DB_PASSWORD'])){$dotenv['DB_PASSWORD']=$defaultDbPassword}
if(-not $dotenv.ContainsKey('JWT_SECRET_KEY') -or [string]::IsNullOrWhiteSpace($dotenv['JWT_SECRET_KEY']) -or [Text.Encoding]::UTF8.GetByteCount($dotenv['JWT_SECRET_KEY']) -lt 32){$dotenv['JWT_SECRET_KEY']=$defaultJwtSecret}
if(-not $dotenv.ContainsKey('VAKTKLAR_BOOTSTRAP_KEY') -or [string]::IsNullOrWhiteSpace($dotenv['VAKTKLAR_BOOTSTRAP_KEY'])){$dotenv['VAKTKLAR_BOOTSTRAP_KEY']=$defaultBootstrap}
if(-not $dotenv.ContainsKey('SECURITY_COOKIE_SECURE')){$dotenv['SECURITY_COOKIE_SECURE']='false'}
@"
DB_PASSWORD=$($dotenv['DB_PASSWORD'])
JWT_SECRET_KEY=$($dotenv['JWT_SECRET_KEY'])
VAKTKLAR_BOOTSTRAP_KEY=$($dotenv['VAKTKLAR_BOOTSTRAP_KEY'])
SECURITY_COOKIE_SECURE=$($dotenv['SECURITY_COOKIE_SECURE'])
"@ | Set-Content '.env' -Encoding UTF8
foreach($key in $dotenv.Keys){[Environment]::SetEnvironmentVariable($key,$dotenv[$key],'Process')}

# Repair known source-level issues before building.
$seed='backend/Workforce.Api/Data/SeedData.cs'
if(Test-Path $seed){
    $c=Get-Content $seed -Raw
    $c=$c -replace 'Level = "Basic"','Level = CompetenceLevel.Basic'
    $c=$c -replace 'Level = "Intermediate"','Level = CompetenceLevel.Intermediate'
    $c=$c -replace 'Level = "Advanced"','Level = CompetenceLevel.Advanced'
    $c=$c -replace 'MinimumLevel = "Basic"','MinimumLevel = CompetenceLevel.Basic'
    $c=$c -replace 'MinimumLevel = "Intermediate"','MinimumLevel = CompetenceLevel.Intermediate'
    $c=$c -replace 'MinimumLevel = "Advanced"','MinimumLevel = CompetenceLevel.Advanced'
    $c=$c.Replace('GetMigrationsAsync()','GetMigrations()')
    Set-Content $seed $c -Encoding UTF8
}

$auth='backend/Workforce.Api/Security/VaktklarAuthentication.cs'
if(Test-Path $auth){
    $c=Get-Content $auth -Raw
    # Enum cannot be passed directly to Csv(string?).
    $c=$c.Replace('Csv(x.Level), x.ValidUntil?.ToString("yyyy-MM-dd") ?? ""','Csv(x.Level.ToString()), x.ValidUntil?.ToString("yyyy-MM-dd") ?? ""')
    $pattern='(?s)\s*var level = Get\("Level"\);.*?else \{ item\.Level = level; item\.ValidUntil = validUntil; updated\+\+; \}'
    $replacement=@'
            var levelText = Get("Level");
            if (string.IsNullOrWhiteSpace(levelText)) levelText = "Basic";
            if (!Enum.TryParse<CompetenceLevel>(levelText, true, out var level)) { errors.Add(new { row = r + 1, message = $"Invalid competence level '{levelText}'. Allowed values: Basic, Intermediate, Advanced." }); continue; }
            var validUntil = DateOnly.TryParse(Get("ValidUntil"), out var parsedDate) ? parsedDate : (DateOnly?)null;
            var item = await db.EmployeeCompetences.FindAsync(employee.Id, competence.Id);
            if (item is null) { db.EmployeeCompetences.Add(new EmployeeCompetence { EmployeeId = employee.Id, CompetenceId = competence.Id, Level = level, ValidUntil = validUntil }); created++; }
            else { item.Level = level; item.ValidUntil = validUntil; updated++; }
'@
    if($c -match $pattern){$c=[regex]::Replace($c,$pattern,"`n$replacement")}
    Set-Content $auth $c -Encoding UTF8
}

$coverage='backend/Workforce.Api/Services/CoverageService.cs'
if(Test-Path $coverage){
    $c=Get-Content $coverage -Raw
    $c=$c -replace '\.ShiftId\.ToString\(\)','.Id.ToString()'
    $c=$c -replace 'x\.ShiftId == shiftId','x.Id == shiftId'
    Set-Content $coverage $c -Encoding UTF8
}

# The Compose file previously failed when the local .env was missing or stale.
# Make the prototype compose interpolation self-contained while still allowing env overrides.
$compose='docker-compose.yml'
if(Test-Path $compose){
    $c=Get-Content $compose -Raw
    $c=$c.Replace('${DB_PASSWORD:?Set DB_PASSWORD in .env}','${DB_PASSWORD:-WorkforceLocalDb_2026_StrongPassword_ChangeMe!}')
    $c=$c.Replace('${JWT_SECRET_KEY:?Set JWT_SECRET_KEY in .env}','${JWT_SECRET_KEY:-WorkforceLocalJwtSecret_2026_ChangeMe_AtLeast32Bytes_9X7K4M2P8Q6R5T3Y1}')
    $c=$c.Replace('${VAKTKLAR_BOOTSTRAP_KEY:?Set VAKTKLAR_BOOTSTRAP_KEY in .env}','${VAKTKLAR_BOOTSTRAP_KEY:-WorkforceBootstrap_2026_ChangeMe_OneTimeKey_9X7K4M2P8Q6R5T3Y1}')
    Set-Content $compose $c -Encoding UTF8
}

# Build backend and frontend before touching Docker so compile errors are explicit.
dotnet build './backend/Workforce.Api/Workforce.Api.csproj' --configuration Release
if($LASTEXITCODE){Fail 'Backend build failed'}

if(Test-Path './frontend/package.json'){
    Push-Location './frontend'
    npm install
    if($LASTEXITCODE){Pop-Location;Fail 'Frontend npm install failed'}
    npm run lint
    if($LASTEXITCODE){Pop-Location;Fail 'Frontend lint failed'}
    npm run build
    if($LASTEXITCODE){Pop-Location;Fail 'Frontend build failed'}
    Pop-Location
}

if(-not $SkipDocker){
    docker info *> $null
    if($LASTEXITCODE -ne 0){
        $dd=Join-Path $env:ProgramFiles 'Docker/Docker/Docker Desktop.exe'
        if(Test-Path $dd){Start-Process $dd; for($i=0;$i-lt 30;$i++){Start-Sleep 2; docker info *> $null; if($LASTEXITCODE -eq 0){break}}}
    }
    docker info *> $null
    if($LASTEXITCODE -ne 0){Fail 'Docker engine unavailable'}

    docker compose config --quiet
    if($LASTEXITCODE){Fail 'Docker Compose config invalid'}

    # Recreate local SQL volume so old SA credentials cannot poison the demo startup.
    docker compose down --remove-orphans
    docker compose down -v --remove-orphans
    if($LASTEXITCODE){Fail 'Could not clean previous prototype containers/volumes'}

    docker compose build --no-cache api frontend
    if($LASTEXITCODE){docker compose logs --tail 150 api; Fail 'Docker image build failed'}

    docker compose up -d sqlserver
    if($LASTEXITCODE){docker compose logs --tail 150 sqlserver; Fail 'SQL Server failed to start'}

    $sqlReady=$false
    for($i=0;$i-lt 36;$i++){
        Start-Sleep 5
        $status=docker inspect --format='{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' workforce-competence-management-sqlserver-1 2>$null
        if($status -eq 'healthy'){$sqlReady=$true;break}
        if($status -eq 'unhealthy'){docker compose logs --tail 150 sqlserver;Fail 'SQL Server became unhealthy'}
    }
    if(-not $sqlReady){docker compose logs --tail 150 sqlserver;Fail 'SQL Server did not become healthy'}

    docker compose up -d api frontend
    if($LASTEXITCODE){docker compose ps; docker compose logs --tail 200 api; Fail 'API/frontend failed to start'}

    $apiHealthy=$false
    for($i=0;$i-lt 36;$i++){
        Start-Sleep 5
        try{$r=Invoke-WebRequest 'http://localhost:5080/health' -UseBasicParsing -TimeoutSec 5; if($r.StatusCode -eq 200){$apiHealthy=$true;break}}catch{}
    }
    if(-not $apiHealthy){docker compose ps; docker compose logs --tail 200 api; Fail 'API health check failed'}

    try{$f=Invoke-WebRequest 'http://localhost:8088' -UseBasicParsing -TimeoutSec 10; if($f.StatusCode -ne 200){Fail 'Frontend returned non-200'}}catch{docker compose ps; docker compose logs --tail 100 frontend; Fail 'Frontend health check failed'}
}

Write-Host ''
Write-Host '=============================================' -ForegroundColor Green
Write-Host ' VAKTKLAR PROTOTYPE 2 IS READY TO OPEN' -ForegroundColor Green
Write-Host ' Frontend: http://localhost:8088' -ForegroundColor Green
Write-Host ' API:      http://localhost:5080' -ForegroundColor Green
Write-Host ' Health:   http://localhost:5080/health' -ForegroundColor Green
Write-Host '=============================================' -ForegroundColor Green

git status --short
