param([switch]$Test, [switch]$Publish, [string]$PublishDirectory = 'dist/UniversalBridge-win-x64')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools/packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $projectRoot '.tools/http-cache'
$env:UNIVERSALBRIDGE_ARTIFACTS = Join-Path $projectRoot 'artifacts'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$dotnetExe = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $dotnetExe)) { $dotnetExe = (Get-Command dotnet -ErrorAction Stop).Source }
& $dotnetExe build UniversalControl.sln -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
if ($Test) {
  & $dotnetExe test tests/UniversalControl.Tests -c Release --no-build --logger 'trx;LogFileName=tests.trx' --results-directory artifacts/tests
  if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}
if ($Publish) {
  & $dotnetExe publish src/UniversalControl.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $PublishDirectory
  if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
  Copy-Item README*.md,CONTRIBUTING.md,SETUP.md,ARCHITECTURE.md,KNOWN_LIMITATIONS.md,TEST_REPORT.md,LICENSE,THIRD_PARTY_NOTICES.md -Destination $PublishDirectory
  Copy-Item licenses -Destination $PublishDirectory -Recurse -Force
  Copy-Item docs -Destination $PublishDirectory -Recurse -Force
  Compress-Archive -Path (Join-Path $PublishDirectory '*') -DestinationPath ($PublishDirectory.TrimEnd('/','\') + '.zip') -Force
}
