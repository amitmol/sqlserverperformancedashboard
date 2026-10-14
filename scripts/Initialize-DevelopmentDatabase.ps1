[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path $PSScriptRoot -Parent
$localTarget = '(localdb)\SQLDashboardDev'
# Fixed local target and database: no configurable production endpoint.
function Invoke-DevelopmentSql([string]$databaseName, [string]$scriptPath) {
    & sqlcmd -S $localTarget -E -b -r 1 -f 65001 -d $databaseName -i $scriptPath
    if ($LASTEXITCODE -ne 0) { throw "SQL script failed: $scriptPath" }
}
Invoke-DevelopmentSql 'master' (Join-Path $workspacePath 'sql/000_create_development_database.sql')
Get-ChildItem -LiteralPath (Join-Path $workspacePath 'sql/migrations') -Filter 'V*.sql' -File |
    Sort-Object Name | ForEach-Object { Invoke-DevelopmentSql 'SQLDashboard_Development' $_.FullName }
Invoke-DevelopmentSql 'SQLDashboard_Development' (Join-Path $workspacePath 'sql/seeds/R__synthetic_scenarios.sql')
