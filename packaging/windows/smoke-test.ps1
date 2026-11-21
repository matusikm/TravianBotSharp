# Extracts the Windows release zip, starts the app and checks that it creates TBS.db next to
# the executable (the portable layout) and stays up.
#
# Usage: smoke-test.ps1 <zip>
param([Parameter(Mandatory)] [string] $Zip)
$ErrorActionPreference = 'Stop'

$work = Join-Path ([IO.Path]::GetTempPath()) "tbs-smoke-$PID"
Expand-Archive -Path $Zip -DestinationPath $work -Force
$appDir = Join-Path $work 'TravianBotSharp'
$exe = Join-Path $appDir 'TravianBotSharp.exe'
$db = Join-Path $appDir 'TBS.db'

$process = Start-Process -FilePath $exe -WorkingDirectory $appDir -PassThru
$ok = $false
for ($i = 0; $i -lt 90; $i++) {
    if (Test-Path $db) { $ok = $true; break }
    if ($process.HasExited) { break }
    Start-Sleep -Seconds 1
}
if ($ok) {
    Start-Sleep -Seconds 10
    $process.Refresh()
    if ($process.HasExited) { $ok = $false }
}
if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }

Write-Host "files in ${appDir}:"
Get-ChildItem $appDir -Name | Where-Object { $_ -in 'TBS.db', 'logs', 'Data' }
if (-not $ok) {
    Write-Host "FAILED: the app did not start and stay up (exit code: $($process.ExitCode))"
    Get-ChildItem (Join-Path $appDir 'logs') -Filter *.txt -ErrorAction SilentlyContinue | Get-Content
    exit 1
}
Write-Host "Smoke test passed"
