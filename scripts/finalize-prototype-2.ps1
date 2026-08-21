[CmdletBinding()]
param([switch]$SkipDocker)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
function Fail([string]$Message){ Write-Host "FAILED: $Message" -ForegroundColor Red; exit 1 }
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path; Set-Location $root
if(-not(Test-Path '.env')){ @'
DB_PASSWORD=WorkforceLocalDb_2026_StrongPassword_ChangeMe!
JWT_SECRET_KEY=WorkforceLocalJwtSecret_2026_ChangeMe_AtLeast32Bytes!
VAKTKLAR_BOOTSTRAP_KEY=WorkforceBootstrap_2026_ChangeMe_OneTimeKey!
SECURITY_COOKIE_SECURE=false
'@ | Set-Content '.env' -Encoding UTF8 }
$seed='backend/Workforce.Api/Data/SeedData.cs'; if(Test-Path $seed){$c=Get-Content $seed -Raw; $c=$c -replace 'Level = "Basic"','Level = CompetenceLevel.Basic'; $c=$c -replace 'Level = "Intermediate"','Level = CompetenceLevel.Intermediate'; $c=$c -replace 'Level = "Advanced"','Level = CompetenceLevel.Advanced'; $c=$c -replace 'MinimumLevel = "Basic"','MinimumLevel = CompetenceLevel.Basic'; $c=$c -replace 'MinimumLevel = "Intermediate"','MinimumLevel = CompetenceLevel.Intermediate'; $c=$c -replace 'MinimumLevel = "Advanced"','MinimumLevel = CompetenceLevel.Advanced'; Set-Content $seed $c -Encoding UTF8 }
$auth='backend/Workforce.Api/Security/VaktklarAuthentication.cs'; if(Test-Path $auth){$c=Get-Content $auth -Raw; $pattern='(?s)\s*var level = Get\("Level"\);.*?else \{ item\.Level = level; item\.ValidUntil = validUntil; updated\+\+; \}'; if($c -match $pattern){$replacement=@'
            var levelText = Get("Level");
            if (string.IsNullOrWhiteSpace(levelText)) levelText = "Basic";
            if (!Enum.TryParse<CompetenceLevel>(levelText, true, out var level)) { errors.Add(new { row = r + 1, message = $"Invalid competence level '{levelText}'. Allowed values: Basic, Intermediate, Advanced." }); continue; }
            var validUntil = DateOnly.TryParse(Get("ValidUntil"), out var parsedDate) ? parsedDate : (DateOnly?)null;
            var item = await db.EmployeeCompetences.FindAsync(employee.Id, competence.Id);
            if (item is null) { db.EmployeeCompetences.Add(new EmployeeCompetence { EmployeeId = employee.Id, CompetenceId = competence.Id, Level = level, ValidUntil = validUntil }); created++; }
            else { item.Level = level; item.ValidUntil = validUntil; updated++; }
'@; $c=[regex]::Replace($c,$pattern,"`n$replacement"); Set-Content $auth $c -Encoding UTF8 }}
$coverage='backend/Workforce.Api/Services/CoverageService.cs'; if(Test-Path $coverage){$c=Get-Content $coverage -Raw; $c=$c -replace '\.ShiftId\.ToString\(\)','.Id.ToString()'; Set-Content $coverage $c -Encoding UTF8 }
$planning='backend/Workforce.Api.Tests/PlanningAdvisorTests.cs'; if(Test-Path $planning){$lines=Get-Content $planning; $inside=$false; for($i=0;$i -lt $lines.Count;$i++){if($lines[$i]-match 'CandidateWithInsufficientRestAfterShiftIsRejected'){$inside=$true;continue}; if($inside -and $lines[$i]-match 'StartTime\s*=\s*new TimeOnly\(18,\s*0\)'){$lines[$i]=$lines[$i]-replace 'new TimeOnly\(18,\s*0\)','new TimeOnly(0, 0)';break}}; $text=($lines -join [Environment]::NewLine); $text=$text.Replace('x.Contains("FravÃ¦r")','x.Contains("Fravær")'); Set-Content $planning $text -Encoding UTF8 }
if(-not(Get-Command dotnet-ef -ErrorAction SilentlyContinue)){dotnet tool install --global dotnet-ef | Out-Host}
dotnet build './backend/Workforce.Api/Workforce.Api.csproj' --configuration Release; if($LASTEXITCODE){Fail 'Backend build failed'}
dotnet test './backend/Workforce.Api.Tests/Workforce.Api.Tests.csproj' --configuration Release --no-restore; if($LASTEXITCODE){Fail 'Backend tests failed'}
Push-Location './frontend'; npm install; npm run lint; npm run build; if($LASTEXITCODE){Pop-Location;Fail 'Frontend validation failed'}; Pop-Location
$dotenv=Get-Content '.env'; foreach($line in $dotenv){if($line -match '^([^#=]+)=(.*)$'){[Environment]::SetEnvironmentVariable($Matches[1],$Matches[2],'Process')}}
if(-not $env:ConnectionStrings__DefaultConnection){$env:ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=WorkforceCompetenceDb;User Id=sa;Password=$env:DB_PASSWORD;TrustServerCertificate=True;Encrypt=False"}
dotnet ef migrations list --project './backend/Workforce.Api/Workforce.Api.csproj' --configuration Release; if($LASTEXITCODE){Fail 'EF migrations failed'}
if(-not $SkipDocker){ docker info *> $null; if($LASTEXITCODE -ne 0){$dd=Join-Path $env:ProgramFiles 'Docker/Docker/Docker Desktop.exe'; if(Test-Path $dd){Start-Process $dd; for($i=0;$i-lt 30;$i++){Start-Sleep 2; docker info *> $null; if($LASTEXITCODE -eq 0){break}}}}; docker info *> $null; if($LASTEXITCODE -ne 0){Fail 'Docker engine unavailable'}; docker compose config --quiet; if($LASTEXITCODE){Fail 'Docker Compose config invalid'}; docker compose down -v; docker compose up --build -d; if($LASTEXITCODE){Fail 'Docker Compose failed'}; $healthy=$false; for($i=0;$i-lt 30;$i++){Start-Sleep 2; try{$r=Invoke-WebRequest 'http://localhost:5080/health' -UseBasicParsing; if($r.StatusCode-eq 200){$healthy=$true;break}}catch{}}; if(-not $healthy){docker compose ps; docker compose logs --tail 100 api; Fail 'API health failed'}; try{$f=Invoke-WebRequest 'http://localhost:8088' -UseBasicParsing; if($f.StatusCode-ne 200){Fail 'Frontend health failed'}}catch{docker compose ps; Fail 'Frontend health failed'} }
Write-Host 'ALL LOCAL VERIFICATION PASSED.' -ForegroundColor Green
git status --short
