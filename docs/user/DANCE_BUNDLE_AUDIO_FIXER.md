# DanceBundleAudioFixer

部分旧 `.unity3d` 舞蹈会在加载音频时卡顿。这个工具用于修复此类包，可从 [0.1.2 Release 附件](https://github.com/maoxig/MateEngine-CustomDancePlayer/releases/tag/v0.1.2) 下载 `DanceBundleAudioFixer.rar`。

1. 关闭游戏，备份舞蹈目录。
2. 打开工具，手动选择 MateEngine 的 `CustomDances` 文件夹。默认目录是七日杀的舞蹈目录。
3. 扫描后选择“修复并保留备份”。
4. 在游戏中播放修复后的舞蹈，检查声音与同步。

工具保留 `*.unity3d.vroidaudio.bak` 备份，遇到异常可恢复原文件。它不重编码音频，会跳过不支持或损坏的包，仅用于旧 `.unity3d`；`.me` 和 `.vmdance` 不适用。

## English

Download `DanceBundleAudioFixer.rar` from the [0.1.2 release assets](https://github.com/maoxig/MateEngine-CustomDancePlayer/releases/tag/v0.1.2) for audio loading stalls in some old `.unity3d` dances.

Exit the game, back up the dance folder, choose MateEngine's `CustomDances` folder manually, scan and repair with backups. The default folder targets 7 Days to Die. Test sound and synchronization afterward.

Backups use `*.unity3d.vroidaudio.bak`. Restore the original if needed. The tool does not re-encode audio and skips unsupported or damaged bundles. It applies to old `.unity3d` files, not `.me` or `.vmdance`.
