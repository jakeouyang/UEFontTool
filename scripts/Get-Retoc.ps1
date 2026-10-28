$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$destination=Join-Path $repoRoot 'tools/retoc/dist'
$binaryHash='a145f3557ec60b163ed1b78b2f380232d2753fa30350c8013c98e171db3ae7e7'
$exe=Join-Path $destination 'retoc.exe'
if((Test-Path -LiteralPath $exe) -and (Get-FileHash -LiteralPath $exe).Hash -eq $binaryHash -and (Test-Path (Join-Path $destination 'LICENSE'))){Write-Output 'Verified retoc v0.1.5 is present.';return}
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$archive=Join-Path (Split-Path $destination -Parent) 'retoc-v0.1.5.zip'
Invoke-WebRequest 'https://github.com/trumank/retoc/releases/download/v0.1.5/retoc_cli-x86_64-pc-windows-msvc.zip' -OutFile $archive
if((Get-FileHash -LiteralPath $archive).Hash -ne 'cc036b06ad3bdcf7003690b00d82719980c374e48a95bf0654f9959148d263aa'){throw 'retoc archive checksum mismatch'}
Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force
if((Get-FileHash -LiteralPath $exe).Hash -ne $binaryHash){throw 'retoc executable checksum mismatch'}
