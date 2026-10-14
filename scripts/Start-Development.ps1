[CmdletBinding()]
param([switch]$OpenChrome, [switch]$SkipDatabaseSetup)
$ErrorActionPreference='Stop'
$workspacePath=Split-Path $PSScriptRoot -Parent
$webPath=Join-Path $workspacePath 'src/Dashboard.Web'
$instanceNames=@(& sqllocaldb info)
if($instanceNames -notcontains 'SQLDashboardDev'){
    & sqllocaldb create SQLDashboardDev
    if($LASTEXITCODE -ne 0){throw 'LocalDB creation failed.'}
}
& sqllocaldb start SQLDashboardDev
if($LASTEXITCODE -ne 0){throw 'LocalDB start failed.'}
if(-not $SkipDatabaseSetup){
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Initialize-DevelopmentDatabase.ps1')
    if($LASTEXITCODE -ne 0){throw 'Development database setup failed.'}
}
& dotnet build (Join-Path $webPath 'Dashboard.Web.csproj') --configuration Release
if($LASTEXITCODE -ne 0){throw 'Application build failed.'}
$env:ASPNETCORE_ENVIRONMENT='Development'
if(-not $env:DashboardDevelopmentConnection){
    $env:DashboardDevelopmentConnection='Server=(localdb)\SQLDashboardDev;Database=SQLDashboard_Development;Integrated Security=true;Encrypt=false;Connect Timeout=5'
}
$url='http://127.0.0.1:5080'
$artifactPath=Join-Path $workspacePath 'artifacts'
New-Item -ItemType Directory -Force -Path $artifactPath | Out-Null
$existing=Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction SilentlyContinue
if($existing){
    $pidPath=Join-Path $artifactPath 'dashboard.pid'
    if(-not (Test-Path -LiteralPath $pidPath) -or [int](Get-Content -LiteralPath $pidPath) -ne $existing[0].OwningProcess){
        throw 'Port 5080 belongs to another process. It was not stopped.'
    }
    Write-Output 'Reusing the application already running on port 5080.'
}else{
    $appDll=Join-Path $webPath 'bin/Release/net9.0/Dashboard.Web.dll'
    $process=Start-Process -FilePath 'dotnet' -ArgumentList @('"'+$appDll+'"') -WorkingDirectory $webPath -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $artifactPath 'dashboard.stdout.log') -RedirectStandardError (Join-Path $artifactPath 'dashboard.stderr.log')
    Set-Content -LiteralPath (Join-Path $artifactPath 'dashboard.pid') -Value $process.Id
}
$ready=$false
for($attempt=0;$attempt -lt 30;$attempt++){
    try{$response=Invoke-WebRequest -UseBasicParsing -Uri $url -TimeoutSec 2;if($response.StatusCode -eq 200){$ready=$true;break}}catch{}
    Start-Sleep -Milliseconds 500
}
if(-not $ready){throw 'Application did not become ready. See artifacts/dashboard.stderr.log.'}
Write-Output "Dashboard is running at $url"
if($OpenChrome){
    $chromePaths=@('C:\Program Files\Google\Chrome\Application\chrome.exe','C:\Program Files (x86)\Google\Chrome\Application\chrome.exe')
    $chromePath=$chromePaths | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
    if(-not $chromePath){throw "Chrome was not found. Open $url manually."}
    Start-Process -FilePath $chromePath -ArgumentList @('--new-window',$url)
}
