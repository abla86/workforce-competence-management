# Workforce & Competence Management — V2 Upgrade

This patch upgrades the existing project with interactive management features.

## Added

### Employees
- Create employee
- Edit name, role, position percentage and active status
- Delete employee
- Assign or update competence
- Set competence level
- Set validity date
- Remove competence

### Competence
- Create competence
- See workforce coverage per competence
- Highlight missing competence in red
- Highlight healthy coverage in green
- Delete unused competence

### Shifts
- Create shift
- Delete shift
- Assign employees
- Remove employees from shift
- Add or update competence requirements
- Remove competence requirements
- Immediate staffing and competence recalculation

### Dashboard
- Stronger green/red operational summary
- Covered shifts highlighted green
- Gaps highlighted red
- Missing staffing displayed directly
- Competence coverage visualization

### Backend
- Strongly typed shift assignments in coverage results
- Additional DELETE/PUT endpoints
- Existing SQL Server schema remains compatible

## Verification after installing the patch

Run:

```powershell
dotnet test .\backend\Workforce.Api.Tests\Workforce.Api.Tests.csproj --configuration Release

cd .\frontend
npm run lint
npm run build

cd ..
docker compose up --build
```
