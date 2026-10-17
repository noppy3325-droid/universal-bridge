$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$projectRoot = Split-Path -Parent $PSScriptRoot
$toolDirectory = Join-Path $projectRoot '.tools'
$sdkDirectory = Join-Path $toolDirectory 'dotnet'
$sdkArchive = Join-Path $toolDirectory 'dotnet-sdk.zip'
$sdkVersion = '10.0.402'
$sdkHash = 'ae4fd528cb7e9f6af6aaf7de36715a69f761f183744f1bb3025d19f5a92447090ec561410e46266c1d10a63781ed6f3de771760bc76c876d87daabc367b898cd'
New-Item -ItemType Directory -Force -Path $toolDirectory | Out-Null
$env:DOTNET_CLI_HOME = Join-Path $toolDirectory 'dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
if (!(Test-Path -LiteralPath (Join-Path $sdkDirectory 'dotnet.exe'))) {
    Invoke-WebRequest -Uri "https://dotnetcli.blob.core.windows.net/dotnet/Sdk/$sdkVersion/dotnet-sdk-$sdkVersion-win-x64.zip" -OutFile $sdkArchive -TimeoutSec 600
    if ((Get-FileHash -LiteralPath $sdkArchive -Algorithm SHA512).Hash -ne $sdkHash) { throw 'Official SDK SHA512 mismatch' }
    Expand-Archive -LiteralPath $sdkArchive -DestinationPath $sdkDirectory -Force
}
& (Join-Path $sdkDirectory 'dotnet.exe') --info
if ($LASTEXITCODE -ne 0) { throw 'SDK validation failed' }
