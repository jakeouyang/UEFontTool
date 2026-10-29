# Third-party notices and format references

## repak v0.2.3

https://github.com/trumank/repak/tree/v0.2.3

Copyright 2024 Truman Kilen, spuds. Dual MIT / Apache-2.0 license.
The build downloads the upstream Windows release and verifies the archive and executable SHA-256. Release packages include the unmodified repak executable and its LICENSE-MIT / LICENSE-APACHE files in `tools/`. Source files are not vendored.

## Unreal PAK format references

https://github.com/FabianFG/CUE4Parse

CUE4Parse is Apache-2.0 licensed. Its FPakInfo, FPakEntry, PakFileReader and DragonSword reader/decryption definitions were consulted to understand game-specific binary layouts. This project implements a small read-only adapter and does not link or ship the CUE4Parse library. Upstream license and notice are retained in `docs/licenses/` as attribution. No upstream game keys are included.

Relevant references:
- https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Pak/Objects/FPakInfo.cs
- https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Pak/Objects/FPakEntry.cs
- https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/GameTypes/DragonSword/Encryption/Aes/DragonSwordPakFileReader.cs
- https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/GameTypes/DragonSword/Encryption/Aes/DragonSwordAes.cs

## .NET

https://github.com/dotnet/runtime

Self-contained releases include the Microsoft .NET runtime. Its license and third-party notices are retained in `docs/licenses/`. WinForms uses Windows platform components.

## Not distributed

Oodle, game fonts/assets, AES keys, Signature Bypass, UE4SS, Python prototypes, and local FModel files are not part of this repository or its release packages. Users must provide any required compatible Oodle library from their own licensed installation.

FontFace and loading-policy documentation:
https://dev.epicgames.com/documentation/en-us/unreal-engine/font-asset-and-editor-in-unreal-engine

The supplied application artwork is project branding; it does not grant rights to unrelated game artwork or trademarks.

## v0.6 dependencies

- retoc (MIT), pinned source commit https://github.com/trumank/retoc/tree/885a8dae740cb1ce1e41ff2e74f67f9f0c118237 . Since v0.8.9 the build verifies the source archive SHA-256, applies `scripts/retoc-partitions.patch`, tests partition boundaries and builds with Rust 1.90.0 and the upstream Cargo.lock. The modification adds direct `_sN.ucas` partition reads without merging game files. Releases include the modified tools/retoc.exe and the upstream retoc-LICENSE; the patch is available in this repository under MIT.
- UAssetAPI 1.1.0 (MIT): https://github.com/atenfyr/UAssetAPI . Used for package imports, exports and reserialization. Includes Newtonsoft.Json 13.0.3 (MIT) and ZstdSharp.Port 0.8.1 (MIT); their license texts are in docs/licenses.
- ZenTools-UE4 (https://github.com/WistfulHopes/ZenTools-UE4) was evaluated as an extraction alternative; it is not bundled or invoked. retoc supplies both conversion directions.

FontFace native serialization reference consulted for v0.8 external-to-inline conversion:
https://github.com/FabianFG/CUE4Parse/blob/master/CUE4Parse/UE4/Assets/Exports/Engine/Font/UFontFace.cs
The bounded UE5.6/5.7 writer is implemented locally; CUE4Parse is not linked or distributed.

PBZ container-loading behavior reference (mod author's report, runtime confirmation is still game/build dependent):
https://www.nexusmods.com/phantombladezero/mods/12
