# DanceBundleAudioFixer / 舞蹈音频修复工具

An optional Windows utility included as `DanceBundleAudioFixer.rar` in the CustomDancePlayer release. It was originally made for 7 Days to Die CustomAvatars; select MateEngine's dance folder manually. The existing tool is redistributed unchanged in v0.1.2.

这是随 Release 提供的可选 Windows 工具，原本面向七日杀 CustomAvatars，MateEngine 用户需手动选择舞蹈目录。v0.1.2 沿用已发布的工具，不是重写或升级工具本身。

## 操作 / Steps

1. Close MateEngine / 关闭游戏。
2. Extract the RAR and run `DanceBundleAudioFixer.exe` / 解压并运行。
3. Switch the upper-right language selector if needed / 右上角可切换语言。
4. Select `MateEngineX_Data\StreamingAssets\CustomDances` and scan / 选择目录并扫描。
5. Fix and keep backups / 修复并保留备份。
6. Test playback, then keep or clean backups / 在游戏内验证后决定是否清理备份。
7. Restore from backups if playback regresses / 如出现异常，从备份恢复。

You can pass the folder as the first startup argument / 也可以将目录作为第一个启动参数：

```powershell
.\DanceBundleAudioFixer.exe 'E:\SteamLibrary\steamapps\common\MateEngine\MateEngineX_Data\StreamingAssets\CustomDances'
```

## What it changes / 修改内容

Only safely matched Vorbis + FSB5 AudioClips in legacy UnityFS `.unity3d` bundles are patched: Streaming, no preload, background loading, LZ4 bundle compression. Encoded audio is preserved. The tool reopens the output and checks audio payload hashes, non-AudioClip object hashes, CAB entries, Unity version, and PathIDs before atomic replacement.

仅处理旧 UnityFS `.unity3d` 中可安全匹配的 Vorbis + FSB5 音频，改为流式、无预加载、后台加载，并采用 LZ4 封装。音频不重编码。重新解析并检查音频 payload、非音频对象、CAB 条目、Unity 版本、PathID 后才原子替换文件。

`Compressed In Memory`, unknown formats, encrypted/damaged bundles are skipped. Backups are named `original.unity3d.vroidaudio.bak`; cleanup targets only this dedicated suffix. Do not use this tool to rewrite `.me` or `.vmdance`. Improved loading is not a guarantee that every dance or machine will run without lag.

Compressed In Memory、未知格式、加密或损坏包会跳过。备份后缀为 `.unity3d.vroidaudio.bak`，清理只针对专用备份文件。不处理 `.me`、`.vmdance`，不能保证消除所有舞蹈或所有机器上的卡顿。
