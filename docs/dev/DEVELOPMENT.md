# 开发说明

## 目录

| 目录 | 内容 |
| --- | --- |
| `Core`、`Tools` | 播放器、界面、宿主交互与镜头 |
| `Loader` | BepInEx 加载入口 |
| `Shared/RuntimeVmd` | VMD 解析、运行时、骨架与表情适配 |
| `Shared/VmdDanceStudio` | 制作、素材检查与包生成 |
| `Locales` | 界面语言 |
| `Tests~/Integration` | 隔离宿主中的回归与截图工具 |
| `scripts` | 构建、打包和迁移脚本 |
| `docs/user`、`docs/images` | 用户说明与界面截图 |
| `docs/legacy` | 旧版使用说明 |

## 构建与资源

构建／打包脚本使用 PowerShell 7 和 .NET SDK。用户安装包中的迁移脚本兼容 Windows PowerShell 5。

`scripts/Build-ReviewPackage.ps1` 接收宿主 Managed、BepInEx Core、native 库、参考 PMX、第三方通知目录和独立输出目录。使用 `VerifiedPluginDirectory` 可复用已验证的完整运行文件，适用于只调整文档或包装的更新。

插件需要三个 DLL、Assets、Locales、Native 和 ThirdParty。UI 由代码生成，仍依赖配套 prefab 与主题资源；脚本类型、序列化字段和资源引用必须保持一致。

`scripts/Build-CompletePackage.ps1` 在插件包上加入 BepInEx、Doorstop 配置、宿主运行库和旧版迁移入口。安装包只带安装提示与运行所需文件；README、更新说明和详细文档在仓库维护。构建来源记录写在输出目录，安装包 manifest 只包含版本与文件校验信息。

## 验证

构建通过、宿主运行、界面和动作效果分别验证。使用隔离宿主，保留原设置和插件；测试运行器不进入安装包。界面变更检查中英文、完整／迷你模式和各页面布局。

文档截图由 `Tests~/Integration/DocumentationCapture.cs` 在隔离宿主采集。示例制作字段用于展示填写方式；不导出示例草稿。

用户舞蹈读取 `MateEngineX_Data/StreamingAssets/CustomDances`；官方 Mods 舞蹈入口单独保留。

## 文档

公开变化统一写 [中文更新说明](../../CHANGELOG_zh.md) 与 [English changelog](../../CHANGELOG.md)，按 0.2 相比 0.1 汇总。字段格式见 [VMDANCE_FORMAT.md](VMDANCE_FORMAT.md)，翻译见 [LOCALIZATION.md](LOCALIZATION.md)。

本机路径、阶段性对比与安装凭证保存在忽略目录 `docs/dev/internal` 和工作区 `.audit`，不复制进用户安装包。
