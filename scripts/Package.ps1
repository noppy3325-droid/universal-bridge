param([string]$PublishDirectory = 'dist/UniversalBridge-win-x64')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
$version = ([xml](Get-Content -LiteralPath 'src/UniversalControl.App/UniversalControl.App.csproj' -Raw)).Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'A semantic source version is required.' }
$published = (Resolve-Path -LiteralPath $PublishDirectory).Path
if (!(Test-Path -LiteralPath (Join-Path $published 'UniversalBridge.exe'))) { throw 'Publish the app before packaging.' }
$properties = ([xml](Get-Content -LiteralPath 'src/UniversalControl.App/UniversalControl.App.csproj' -Raw)).Project.PropertyGroup
$exe = Join-Path $published 'UniversalBridge.exe'
if ($properties.AssemblyName -ne 'UniversalBridge' -or (Test-Path -LiteralPath (Join-Path $published 'UniversalControl.exe'))) {
  throw 'Release executable names must be UniversalBridge.exe. Use a new, empty publish directory.'
}
$exeInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
if ($exeInfo.ProductName -ne 'Universal Bridge' -or $exeInfo.ProductVersion.Split('+')[0] -ne $version) { throw 'Executable product or version differs from the release configuration.' }
$manifest = [ordered]@{Schema=1;Version=$version;Tag=$properties.ReleaseTag;File='UniversalBridge.exe';Size=(Get-Item -LiteralPath $exe).Length;Sha256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()}
$manifest | ConvertTo-Json | Set-Content -LiteralPath 'dist/update.json' -Encoding utf8
Copy-Item -LiteralPath $exe -Destination 'dist/UniversalBridge.exe' -Force
Copy-Item README*.md,CONTRIBUTING.md,SETUP.md,ARCHITECTURE.md,KNOWN_LIMITATIONS.md,TEST_REPORT.md,LICENSE,THIRD_PARTY_NOTICES.md -Destination $published -Force
Copy-Item docs,licenses -Destination $published -Recurse -Force
$binaryZip = Join-Path $projectRoot "dist/UniversalBridge-$version-win-x64.zip"
$sourceZip = Join-Path $projectRoot "dist/UniversalBridge-$version-source.zip"
Compress-Archive -Path (Join-Path $published '*') -DestinationPath $binaryZip -Force
$stage = Join-Path $projectRoot ('artifacts/source-package-' + $version + '-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Get-ChildItem -LiteralPath $projectRoot -File | Where-Object { $_.Extension -in '.md','.json','.props','.sln' -or $_.Name -in 'NuGet.Config','.gitignore','.gitattributes','LICENSE' } | Copy-Item -Destination $stage
foreach ($folder in 'src','tests','scripts','docs','licenses','.github') {
  foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $projectRoot $folder) -Recurse -File -Force | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })) {
    $relative = [IO.Path]::GetRelativePath($projectRoot, $file.FullName)
    $destination = Join-Path $stage $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
  }
}
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $sourceZip -Force
$hashes = foreach ($asset in @((Join-Path $published 'UniversalBridge.exe'), $binaryZip, $sourceZip, (Join-Path $projectRoot 'dist/update.json'))) {
  $hash = Get-FileHash -LiteralPath $asset -Algorithm SHA256
  $hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($asset)
}
$hashes | Set-Content -LiteralPath 'dist/SHA256SUMS.txt' -Encoding utf8
Get-Item -LiteralPath $binaryZip,$sourceZip,(Join-Path $projectRoot 'dist/SHA256SUMS.txt') | Select-Object Name,Length
