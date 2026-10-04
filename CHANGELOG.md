# Changelog / 更新记录

## v0.5.0

- Wukong: convert packed output to the game's runtime layout (custom 223-byte footer plus a pad byte per entry so data starts at the runtime's 54-byte local header), then verify by reading the artifact back through the Wukong adapter. Confirmed working in game on the current Steam build (2026-10); no launch flags or signature bypass required.
- Wukong: stop compressing output with Zlib; the observed runtime only loads Oodle payloads from its own paks and the converted layout is validated for uncompressed entries.
- Wukong: the adapter can now extract uncompressed (zero-block) entries; diagnostics no longer recommend `-fileopenlog` or signature bypasses (the shipping build no longer implements the flag, and legacy bypass patterns may mismatch after game updates).
- Remove the Wukong test-launch entry point (UI link and `launch-wukong` CLI); launch the game directly when verifying manually.
- 悟空：打包输出自动转换为游戏运行时布局（自定义 223 字节 footer，并为每个条目补 1 字节使数据从运行时的 54 字节本地头开始），再用悟空适配器回读校验。已在当前 Steam 版本游戏内验证生效，无需启动参数或签名绕过。
- 悟空：输出不再使用 Zlib 压缩；已验证的转换为未压缩条目布局。
- 悟空：适配器支持提取未压缩（零块）条目；诊断不再建议 `-fileopenlog` 或签名绕过（当前发行版已不实现该参数，旧绕过特征码在游戏更新后可能失配）。
- 移除“悟空测试启动”入口（界面链接与 `launch-wukong` 命令）；手动验证时直接启动游戏即可。

## v0.4.0

- Align language and GitHub header buttons; replace the former social link with the project repository.
- Add masked, session-only AES text input with hexadecimal/Base64 validation. Remove automatic local key discovery and machine-specific default paths.
- Add a DragonSword: Awakening v101 PAK reader for AES and masked index/string/entry data. Retain the Wukong layout adapter.
- Improve unsupported-format diagnostics instead of exposing the last generic V0 probe failure.
- Add synthetic encrypted-PAK and DragonSword regression tests. Game files and real game keys are not used in automated tests.
- Publish Chinese/English documentation, MIT license, and a Windows build/test/Release workflow with verified repak downloads and allowlisted packaging.

- 修复顶部语言与 GitHub 按钮对齐，链接改为项目仓库。
- 增加 AES 直接输入、遮蔽显示和格式校验，仅在本次会话使用；移除自动读取本机密钥和个人默认路径。
- 适配 DragonSword v101 加密/混淆索引，保留悟空格式支持，改进错误信息。
- 增加合成加密 PAK 回归测试、中英文文档、MIT 许可及自动 Releases 构建。

Runtime font appearance still requires per-game verification. Embedded FontFace, IoStore rewriting, offline atlases, and universal UE4/UE5 compatibility are not implemented.
游戏内字体仍需按游戏验证；尚未实现内嵌 FontFace、IoStore 重写、离线图集或全部 UE4/UE5 兼容。
