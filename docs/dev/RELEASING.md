# 发行文件维护

安装包由 `scripts/Build-ReviewPackage.ps1` 和 `scripts/Build-CompletePackage.ps1` 生成。版本参数必须与播放器 DLL 和插件包 manifest 一致；脚本会拒绝版本不匹配的输入。

完整包用于首次安装，插件包用于已经安装过兼容 BepInEx 环境的更新。两者都包含英文安装提示和 0.1.x 迁移入口。用户文档、图片和更新说明在仓库维护，不复制进安装包。

`scripts/Prepare-Release.ps1` 接收插件构建目录、整合构建目录和新的输出目录，校验版本与文件后生成发版附件：两份 ZIP、`SHA256SUMS.txt` 与从中文 CHANGELOG 生成的更新说明。Release 页面中的文档链接转换为对应标签的仓库链接。构建来源、运行证据和工作区状态记录保存在本地输出目录，安装包 manifest 只包含版本及文件校验信息。

发布前核对：

- DLL、版本框、加载日志、文件名与 manifest 的版本一致。
- 实际附件的运行文件与隔离宿主测试使用的文件一致。
- 完整包中 BepInEx 配置、运行库、UI 资源、native 文件与迁移脚本齐全。
- 检查中英文界面、迁移、制作包往返保存、播放与停止、原生菜单和镜头恢复。
- README 的本地链接、图片与模型来源可追溯，更新说明按正式版本汇总。

`dist` 保留 0.1 的历史发行文件。`.github/workflows/release.yml` 仅处理 `v0.1.*` 标签；0.2 及以后的标签不会触发这条旧流程上传附件。

程序集 `AssemblyVersion` 保持 `1.0.0.0`，以保留配套 Unity 资源的脚本身份；正式产品版本记录在 `AssemblyFileVersion`、`AssemblyInformationalVersion` 和 BepInEx 插件信息中。
