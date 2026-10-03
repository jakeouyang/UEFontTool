param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot 'UEFontTool/UEFontTool.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project
$version = [string]$projectXml.Project.PropertyGroup.Version
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne "v$version") { throw 'Tag must match the project version.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot ('artifacts/build-' + [guid]::NewGuid().ToString('N')) }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new output directory; existing files will not be removed.' }
New-Item -ItemType Directory -Path $output | Out-Null
& (Join-Path $PSScriptRoot 'Get-Dependencies.ps1')
$app = Join-Path $output 'app'
dotnet publish $project -c Release -r win-x64 --self-contained true -o $app -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:ContinuousIntegrationBuild=true
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
$exe = Join-Path $app 'UEFontTool.exe'
$testReport = Join-Path $output 'self-test.txt'
$test = Start-Process -FilePath $exe -ArgumentList @('--self-test', ('"' + $testReport + '"')) -WindowStyle Hidden -Wait -PassThru
if ($test.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $testReport)) {
    if (Test-Path -LiteralPath ($testReport + '.error.txt')) { Get-Content -LiteralPath ($testReport + '.error.txt') }
    throw 'Self-test failed.'
}
Get-Content -LiteralPath $testReport
# Stage an explicit allowlist, never the entire development/publish directory.
$package = Join-Path $output "UEFontTool-v$version-win-x64"
New-Item -ItemType Directory -Path (Join-Path $package 'tools') -Force | Out-Null
Copy-Item -LiteralPath $exe -Destination $package
foreach ($name in @('README.md', 'README.en.md', 'LICENSE', 'THIRD_PARTY.md', 'CHANGELOG.md')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination $package
}
foreach ($name in @('repak.exe', 'LICENSE-MIT', 'LICENSE-APACHE')) {
    Copy-Item -LiteralPath (Join-Path $app "tools/$name") -Destination (Join-Path $package 'tools')
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs') -Destination $package -Recurse
$zip = Join-Path $output "UEFontTool-v$version-win-x64.zip"
Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($zip))" | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ascii
Write-Output "Release package: $zip"
