# v0.1.2 validation / 核验记录

Date: 2026-10-05 (Asia/Shanghai).

Base: `3ce96081d64cadf3bb4200cd1e519c1334152cd9`, compared with the v0.1.1 tag `015bf68c5c7774553b762f2361e19d266c66f775` and the actual downloadable v0.1.1 DLL. The public DLL is 48,128 bytes; the repository's old dist DLL is 47,616 bytes. The public DLL's RefreshDanceFileList uses the two-argument Directory.GetFiles and only scans the top-level directory.

v0.1.2 uses committed source plus the camera demo integration and small null guards. It excludes uncommitted playlist/VMD development. AssemblyVersion stays 1.0.0.0 for prefab identity; AssemblyFileVersion is 0.1.2.0 and informational version is 0.1.2.

## Completed / 已完成

- Build against original GitHub X3.3-HOTFIX-1 Managed assemblies, Unity 6000.2.6f2: 0 warnings, 0 errors.
- Mono.Cecil resolution: 237 host member references checked, no failures. Only existing host APIs are used, including working around stripped Camera.CopyFrom / Canvas.renderMode setters.
- Existing UI bundle decoded: script types and mandatory UI references verified. Camera components and controls are created and bound by DanceCameraDemo at runtime; no new serialized MonoBehaviour is required in the bundle.
- Unity 6000.2.6f2 prefab/component smoke: UI bundle instantiated; settings toggle and scale controls created; toggle bound; idle without avatar guarded; position/scale and FOV synchronized; stop clears preview; close disables camera demo.
- Player zip and developer archive contain the same rebuilt DLL; checksums provided for distributed assets.
- Optional DanceBundleAudioFixer is copied from its previously published v0.1.1 asset unchanged (SHA-256 `f4aa0821fc7f973b1fa5ed1d2f51864bae7321d6ab5fe96f4d1f811f65a9bb7b`). Its local source was read to verify the README instructions and supported formats.

## Scope / 边界

Editor smoke is not full gameplay or visual verification in the original official player. Steam 3.4, avatar rig variants, animated dances, facial expressions, desktop window movement and simultaneous use of the official player still need in-game validation. No installed game DLL/configuration was replaced during preparation.

编辑器组件检查不等于原版游戏内完整播放与视觉核验。Steam 3.4、各类角色骨架、实际动作/表情、桌面窗口跟随及官方播放器争用尚需实测。

Rebuild with an explicit host:

```powershell
dotnet build .\CustomDancePlayer.csproj -c Release -p:GameManagedDir='YOUR_ORIGINAL_HOST_MANAGED' -o 'YOUR_ISOLATED_OUTPUT'
```

The Editor validation source is retained under `Tests~/Editor`; create an isolated matching Unity project with uGUI/TMP essentials, the candidate plugin, and the original host's external managed dependencies. Supply the paired bundle as `customdanceplayer.bundle` at project root and execute `CameraReleaseValidation.Run`. Do not import the test into an existing dirty Unity project or install the candidate over a separate VMD development build.
