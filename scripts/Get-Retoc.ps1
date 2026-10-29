$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$destination=Join-Path $repoRoot 'tools/retoc/dist'
$commit='885a8dae740cb1ce1e41ff2e74f67f9f0c118237'
$archiveHash='de9882d738d82df40f992b41210ca68139e69f1dd30846c118b39617c12e00e4'
$patch=Join-Path $PSScriptRoot 'retoc-partitions.patch'
$patchHash=(Get-FileHash -LiteralPath $patch -Algorithm SHA256).Hash
$exe=Join-Path $destination 'retoc.exe'
$stamp=Join-Path $destination 'build-info.json'
if((Test-Path -LiteralPath $exe) -and (Test-Path -LiteralPath $stamp) -and (Test-Path -LiteralPath (Join-Path $destination 'LICENSE'))){
    $built=Get-Content -LiteralPath $stamp -Raw | ConvertFrom-Json
    if($built.Commit -eq $commit -and $built.PatchSha256 -eq $patchHash -and $built.BinarySha256 -eq (Get-FileHash -LiteralPath $exe).Hash){Write-Output 'Verified retoc with UCAS partition support is present.';return}
}
if(-not (Get-Command rustup -ErrorAction SilentlyContinue)){throw 'Building the pinned retoc partition adapter requires Rust/rustup and Visual C++ build tools. Install Rust, then rerun the build.'}
rustup toolchain install 1.90.0 --profile minimal
if($LASTEXITCODE -ne 0){throw 'Rust toolchain installation failed.'}
$archive=Join-Path $repoRoot 'tools/retoc/retoc-885a8da-source.zip'
New-Item -ItemType Directory -Force -Path (Split-Path $archive -Parent) | Out-Null
if(-not (Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive).Hash -ne $archiveHash){Invoke-WebRequest "https://codeload.github.com/trumank/retoc/zip/$commit" -OutFile $archive}
if((Get-FileHash -LiteralPath $archive).Hash -ne $archiveHash){throw 'retoc source archive checksum mismatch.'}
$buildRoot=Join-Path $repoRoot ('tools/retoc/build-'+[guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $archive -DestinationPath $buildRoot
$source=Join-Path $buildRoot "retoc-$commit"
git -C $source apply --check $patch
if($LASTEXITCODE -ne 0){throw 'retoc patch validation failed.'}
git -C $source apply $patch
if($LASTEXITCODE -ne 0){throw 'retoc patch application failed.'}
Push-Location -LiteralPath $source
try{
    cargo +1.90.0 test --locked --release -p retoc file_pool::partition_tests --lib
    if($LASTEXITCODE -ne 0){throw 'retoc partition regression test failed.'}
    cargo +1.90.0 build --locked --release -p retoc_cli
    if($LASTEXITCODE -ne 0){throw 'retoc build failed.'}
}finally{Pop-Location}
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$targetDir=if($env:CARGO_TARGET_DIR){[IO.Path]::GetFullPath($env:CARGO_TARGET_DIR)}else{Join-Path $source 'target'}
Copy-Item -LiteralPath (Join-Path $targetDir 'release/retoc.exe') -Destination $exe -Force
Copy-Item -LiteralPath (Join-Path $source 'LICENSE') -Destination $destination -Force
@{Commit=$commit;PatchSha256=$patchHash;BinarySha256=(Get-FileHash -LiteralPath $exe).Hash} | ConvertTo-Json | Set-Content -LiteralPath $stamp -Encoding utf8
Write-Output 'Built pinned retoc with tested UCAS partition support.'
