param([string]$AppPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (!$AppPath) { $AppPath = Join-Path $projectRoot 'dist/UniversalBridge-win-x64/UniversalControl.exe' }
$appExecutable = (Resolve-Path -LiteralPath $AppPath).Path
$resultDirectory = Join-Path $projectRoot ('artifacts/crash-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$childDirectory = Join-Path $resultDirectory 'child'
$recoveryDirectory = Join-Path $resultDirectory 'recovery'
$childProcess = Start-Process -FilePath $appExecutable -ArgumentList @('--hold-local-input','--data-dir',('"' + $childDirectory + '"')) -WindowStyle Hidden -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    $readyFile = Join-Path $childDirectory 'ready.json'
    while (!(Test-Path -LiteralPath $readyFile)) {
        $childProcess.Refresh()
        if ($childProcess.HasExited -or [DateTime]::UtcNow -gt $deadline) { throw 'Local-only hook helper did not become ready' }
        Start-Sleep -Milliseconds 50
    }
    $ready = Get-Content -Raw -LiteralPath $readyFile | ConvertFrom-Json
    if (!$ready.HooksInstalled -or $ready.InputSuppressed) { throw 'Unexpected capture state in crash helper' }
    Stop-Process -Id $childProcess.Id -Force
    if (!$childProcess.WaitForExit(5000)) { throw 'Crash helper did not exit' }
    $recovery = Start-Process -FilePath $appExecutable -ArgumentList @('--verify-local-input','--data-dir',('"' + $recoveryDirectory + '"')) -WindowStyle Hidden -PassThru -Wait
    if ($recovery.ExitCode -ne 0) { throw 'Hook/hotkey re-registration failed after forced termination' }
    $verified = Get-Content -Raw -LiteralPath (Join-Path $recoveryDirectory 'input-verification.json') | ConvertFrom-Json
    if (!$verified.Success) { throw 'Recovery verification did not pass' }
    [pscustomobject]@{ Success=$verified.Success; ForcedTermination=$true; HooksAndHotkeysReacquired=$verified.HooksInstalled; InputSuppressed=$false; RemoteHeldKeyReleaseTested=$false; At=[DateTimeOffset]::Now } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $resultDirectory 'crash-verification.json')
    Get-Content -Raw -LiteralPath (Join-Path $resultDirectory 'crash-verification.json')
    Copy-Item -LiteralPath (Join-Path $resultDirectory 'crash-verification.json') -Destination (Join-Path $projectRoot 'artifacts/crash-latest.json') -Force
}
finally {
    $childProcess.Refresh()
    if (!$childProcess.HasExited) { Stop-Process -Id $childProcess.Id -Force }
}
