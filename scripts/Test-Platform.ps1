param([string]$AppPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
if (!$AppPath) { $AppPath = Join-Path $projectRoot 'dist/UniversalBridge-win-x64/UniversalBridge.exe' }
$appExecutable = (Resolve-Path -LiteralPath $AppPath).Path
$resultDirectory = Join-Path $projectRoot ('artifacts/platform-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
foreach ($mode in '--probe-gatt','--verify-server','--verify-local-input','--smoke-test') {
    $modeDirectory = Join-Path $resultDirectory $mode.TrimStart('-')
    $testProcess = Start-Process -FilePath $appExecutable -ArgumentList @($mode,'--data-dir',('"' + $modeDirectory + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($testProcess.ExitCode -ne 0) { throw "$mode failed; inspect $resultDirectory" }
    $resultFile = switch ($mode) {
        '--probe-gatt' { 'adapter-probe.json' }
        '--verify-server' { 'server-verification.json' }
        '--verify-local-input' { 'input-verification.json' }
        '--smoke-test' { 'gui-smoke.json' }
    }
    $result = Get-Content -Raw -LiteralPath (Join-Path $modeDirectory $resultFile) | ConvertFrom-Json
    $passed = if ($mode -eq '--probe-gatt') { $result.AdapterFound -and $result.LowEnergy -and $result.Peripheral -and $result.HidServiceResult -eq 'Success' } else { $result.Success -eq $true }
    if (!$passed) { throw "$mode result did not pass; inspect $modeDirectory" }
}
$latestDirectory = Join-Path $projectRoot 'artifacts/platform-latest'
New-Item -ItemType Directory -Force -Path $latestDirectory | Out-Null
Copy-Item -Path (Join-Path $resultDirectory '*') -Destination $latestDirectory -Recurse -Force
Write-Output $resultDirectory
