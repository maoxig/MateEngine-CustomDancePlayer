# CustomDancePlayer 0.2 开发入口

先读工作区上级 `AGENT_FLOW_MateEngine.md`、本仓库 `docs/dev/DEVELOPMENT.md` 和本地 `docs/dev/internal/MAINTENANCE.md`。本仓库从已发布 v0.1.2 / `7f7c41013201990ed7c86ca3fb48d6e349dc5df4` 独立建立，分支 `dev/v0.2`；原两个脏工作区不要覆盖。本轮 CustomLLMAPI 待定。

- 0.2 是本地候选版，验收前不推送、不发布。`dist/` 是旧发行基线；输出只放上级 `.audit/` 或显式指定的独立输出目录。
- UI 在代码中构建，但必须带 `Assets/customdanceplayer.bundle` 和 `Assets/dance-theme.bundle`。保留旧 prefab 的公共序列化字段和脚本类型身份，不把仅构建 DLL 当成完整部署。
- 官方 GitHub 3.3 与 Steam 3.4 build `24920588` 已分别用原始 Managed 构建，并在隔离游戏运行。后续宿主更新仍需重新分层验证，不能沿用本轮结论。
- VMD 统一使用 native PMX + rest-rig 重定向；增强／兼容模式选择已移除，旧 useNativeVmd 配置不再控制后端。目标静止骨架来自 Avatar.skeleton，禁止使用当前 idle 作为 bind pose，禁止移动 Spine 来强行匹配髋到头角度。校准按父到子更新独立骨架，目标关节局部位置保持原值。缺少 native／参考文件时报错，不静默降级。
- VMD 默认直立模式把 native 中心／腰公共分支的首帧 yaw 作为固定朝向基线，只消除动作／参考模型的常量偏置并保留后续相对转身。修改根旋转时必须继续用多首动作的 0% 与后续采样同时验证，不能用全程锁 yaw 代替。
- VMD 方向根优先使用 `全ての親`；`グルーブ` 是半标准位移层，不能用它的动画旋转去全局反向修正四肢。朝向基线只消除中心／腰公共分支首帧的固定 yaw，保留后续转身。腿链按源膝盖角与目标实际腿长解算，保留校准脚掌方向。专项回归使用 ODDS 39 秒、Angelite、Melt、Marine Bloomin、III Marine 的 IK/FK、idle 污染与停止恢复，不能仅以有限坐标／躯干角度宣称姿态正常。
- VMD 跟随与主镜头切换由 `PoseApplied` 回调触发；面板跟随需要把该事件排到同帧较晚执行槽，等 HumanPose 骨骼 Transform 可见后只应用一次，不能只读回调瞬间或重复平滑。MMD 运镜必须使用真实主镜头，在正交正面视图与舞蹈镜头间切换，不得恢复叠加 RenderTexture 预览。关闭、停止和切歌必须恢复位置、旋转、FOV、投影和启用状态；运镜期间距离保持不得覆盖创作镜头。
- `.unity3d` 的真实 MMD `Body` 与 VRM dummy→UniversalBlendshapes 是两条路径。VRM 桥接需要 UniversalBlendshapes 开启；不能全局禁用。回归必须检测含表情曲线的文件和实际面部权重。
- 旧 `.unity3d` / `.me` 跳转使用活动 Animator 状态完整路径哈希与 `CrossFadeInFixedTime` 固定时间偏移；Steam X3.4 裁剪 API 没有 `Animator.Play`，不能把该成员存在性作为进度条可用条件。
- 系统文件选择器使用官方 SFB，平台初始化在主线程，Windows 对话框在独立 STA 线程执行。不要退回普通 WinForms 控件。
- 维护完整 / 迷你两种模式，图标切换、悬停提示、独立位置保存和制作草稿。列表用行池；新增控件必须验证滚轮命中区域与可见反馈。
- 素材只在本地压力测试副本使用，不打入插件包。版本变化集中写 `CHANGELOG_zh.md` / `CHANGELOG.md`，README 保留安装与使用说明。
- Steam 启动卡顿要分别记录宿主模型帧、播放器首次 UI 帧、后台扫描和主线程应用；内部 `--cdp-host-baseline` / `--cdp-frame-audit` 只用于隔离副本。不能把宿主模型／物理帧归因给舞蹈库，也不能把测试帧探针装入候选包。
- 用户舞蹈只有 `MateEngineX_Data/StreamingAssets/CustomDances` 一个入口；不要恢复插件内部 `Dances` 扫描。导入写入该目录，制作预览写入其 `VmdComposer` 子目录；官方 `StreamingAssets/Mods` 舞蹈读取独立保留。


文档维护：公开用户说明沿用 docs/user、图片放 docs/images；公开更新说明按 0.2 对比 0.1 汇总。内部阶段记录与绝对本机路径放忽略的 docs/dev/internal 或工作区 .audit。安装包不复制 docs、README 或 CHANGELOG，只带英文 INSTALL.txt、必要通知及迁移入口。
