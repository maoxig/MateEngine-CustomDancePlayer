# CustomDancePlayer v0.1.2

## 中文

1. 关闭 MateEngine，备份原 CustomDancePlayer DLL 和 UI `.me`。
2. 将本包 `CustomDancePlayer.dll` 放到游戏 `MateEngineX_Data\Managed`。
3. 核对 `MateEngineX_Data\ScriptingAssemblies.json`：names 中存在一项 `CustomDancePlayer.dll`，types 中相同索引为 `16`。已注册时不要重复添加；两数组逐项对应。
4. 用 MateEngine 的 Mod 导入界面加载本包 `CustomDancePlayer.me`。移除重复的旧 UI 实例，保留自己的舞蹈与设置文件。
5. 将 `.unity3d` 舞蹈放在 `MateEngineX_Data\StreamingAssets\CustomDances`，可以分子目录，点击刷新；H 显示/隐藏播放器。
6. Settings 中启用 MMD camera demo 查看内嵌动画镜头轨迹，Camera scale 调节比例，预览关闭按钮关闭演示。没有镜头轨迹时显示等待提示。

可选 DanceBundleAudioFixer 工具另行下载。其默认目录为七日杀 Dances，MateEngine 用户需选择 CustomDances；先使用保留备份的修复方式，再测试游戏播放。

本版沿用传统 DLL 注册安装，没有切换 BepInEx。VMD/.vmdance 与大规模 UI、本地化改造计划在 0.2；不要用本包覆盖带 RuntimeVmd 的开发版本。本包不含舞蹈素材，原素材使用/再分发规则仍适用。

## English

1. Close MateEngine and back up the previous CustomDancePlayer DLL and UI `.me`.
2. Copy this `CustomDancePlayer.dll` to `MateEngineX_Data\Managed`.
3. Check ScriptingAssemblies.json: exactly one `CustomDancePlayer.dll` in names, with `16` at the matching index in types. Keep the two arrays aligned; do not duplicate an existing registration.
4. Import this `CustomDancePlayer.me` through the game's Mod UI. Remove duplicate old player UI instances while preserving your dances and settings.
5. Put legacy `.unity3d` dances under `StreamingAssets\CustomDances`, optionally in subfolders; refresh the list. H toggles the player UI.
6. Enable MMD camera demo in Settings. Camera scale adjusts the preview camera's offset; the close button disables it. Dances without camera tracks show a waiting message.

The optional DanceBundleAudioFixer is a separate download. Select MateEngine's CustomDances folder manually, keep backups, then verify playback.

This release retains DLL registration and does not migrate to BepInEx. VMD/.vmdance, localization and major UI changes are planned for 0.2. Do not overwrite a RuntimeVmd development installation with this package. Dance assets are not included; respect their redistribution rules.
