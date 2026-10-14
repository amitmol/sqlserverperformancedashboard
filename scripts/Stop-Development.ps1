[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$workspacePath=Split-Path $PSScriptRoot -Parent
$pidPath=Join-Path $workspacePath 'artifacts/dashboard.pid'
if(-not(Test-Path -LiteralPath $pidPath)){Write-Output 'No recorded dashboard process.';return}
$dashboardProcessId=[int](Get-Content -LiteralPath $pidPath)
$dashboardProcess=Get-CimInstance Win32_Process -Filter "ProcessId=$dashboardProcessId"
$expectedDll=Join-Path $workspacePath 'src/Dashboard.Web/bin/Release/net9.0/Dashboard.Web.dll'
if($dashboardProcess){
    if($dashboardProcess.Name -ne 'dotnet.exe' -or -not $dashboardProcess.CommandLine.Contains($expectedDll)){
        throw 'The recorded PID belongs to another process; it was not stopped.'
    }
    Stop-Process -Id $dashboardProcessId
}
Remove-Item -LiteralPath $pidPath
Write-Output 'Dashboard stopped. LocalDB and its data were retained.'
