[CmdletBinding()]
param([switch]$Sql)
$ErrorActionPreference='Stop'
$workspacePath=Split-Path $PSScriptRoot -Parent
& dotnet build (Join-Path $workspacePath 'SQLServerDashboard.sln') --configuration Release
if($LASTEXITCODE -ne 0){throw 'Build failed.'}
if($Sql){
    & sqlcmd -S '(localdb)\SQLDashboardDev' -d SQLDashboard_Development -E -b -r 1 -f 65001 -i (Join-Path $workspacePath 'sql/verification/VerifyDevelopmentSchema.sql')
    if($LASTEXITCODE -ne 0){throw 'Schema verification failed.'}
    if(-not $env:DashboardDevelopmentConnection){throw 'Set DashboardDevelopmentConnection to the dedicated local development database.'}
    & dotnet run --project (Join-Path $workspacePath 'tests/Dashboard.Verification/Dashboard.Verification.csproj') --configuration Release --no-build -- --sql
}else{
    & dotnet run --project (Join-Path $workspacePath 'tests/Dashboard.Verification/Dashboard.Verification.csproj') --configuration Release --no-build
}
if($LASTEXITCODE -ne 0){throw 'Calculation verification failed.'}
