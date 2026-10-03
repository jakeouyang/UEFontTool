# Changelog / 更新记录

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
