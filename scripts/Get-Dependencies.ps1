$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $repoRoot 'tools/repak'
$archiveHash = '6720d602144d75df477a99d5bedb6ea780997546afc335901d4937cafeaa73fa'
$binaryHash = 'fcd538e5994b9bb833622d425ae346f4e0692f02d4b0025114a559f9b6286022'
$exe = Join-Path $destination 'repak.exe'
if ((Test-Path -LiteralPath $exe) -and (Get-FileHash -LiteralPath $exe).Hash -eq $binaryHash -and
    (Test-Path (Join-Path $destination 'LICENSE-MIT')) -and (Test-Path (Join-Path $destination 'LICENSE-APACHE'))) {
    Write-Output 'Verified repak v0.2.3 is already present.'
    exit 0
}
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$stage = Join-Path $tempRoot ('UEFontTool-deps-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
try {
    $zip = Join-Path $stage 'repak.zip'
    Invoke-WebRequest 'https://github.com/trumank/repak/releases/download/v0.2.3/repak_cli-x86_64-pc-windows-msvc.zip' -OutFile $zip
    if ((Get-FileHash -LiteralPath $zip).Hash -ne $archiveHash) { throw 'repak archive checksum mismatch.' }
    Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $stage 'unpacked')
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    foreach ($name in @('repak.exe', 'LICENSE-MIT', 'LICENSE-APACHE')) {
        $matches = @(Get-ChildItem (Join-Path $stage 'unpacked') -Recurse -File -Filter $name)
        if ($matches.Count -ne 1) { throw "Expected exactly one dependency file: $name" }
        Copy-Item -LiteralPath $matches[0].FullName -Destination (Join-Path $destination $name)
    }
    if ((Get-FileHash -LiteralPath $exe).Hash -ne $binaryHash) { throw 'repak binary checksum mismatch.' }
    Write-Output 'Downloaded and verified repak v0.2.3.'
}
finally {
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    if ($resolvedStage.StartsWith($tempRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path $resolvedStage -Leaf).StartsWith('UEFontTool-deps-')) {
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
}
