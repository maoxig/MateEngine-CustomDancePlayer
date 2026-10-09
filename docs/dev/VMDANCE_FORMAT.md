# `.vmdance` 包字段

`.vmdance` 是包含一个 `dance.json` 的 ZIP。制作页会写入并验证下面的常用字段；资源路径均相对于包根目录。

| 字段 | 类型 | 用途 |
| --- | --- | --- |
| `id` | string | 文件名与舞蹈库唯一标识。制作页显示为“文件名标识” |
| `title` | string | 舞蹈库显示名称 |
| `motionVmd` | string | 必需的身体动作 VMD |
| `faceVmd` / `lipVmd` / `cameraVmd` | string | 可选表情、口型和镜头轨道 |
| `additionalVmdFiles` | string[] | 可选附加层；后层覆盖同名轨道 |
| `audioFile` | string | 可选 OGG / WAV / MP3 |
| `referencePmx` | string | 可选动作专用参考 PMX |
| `audioOffsetSeconds` | number | 音频时间偏移 |
| `positionScale` | number | MMD 位移导入比例 |
| `loop` | boolean | 包的循环建议 |
| `footIk` | boolean | 可选。`true` 启用脚 / 脚尖 IK，并遵守 VMD 自己的逐帧 IK 开关；`false` 强制关闭；省略时使用播放器全局默认 |

例如，需要对所有 VRM 关闭一套动作的脚步 IK：

```json
{
  "formatVersion": 1,
  "id": "my-dance",
  "title": "My Dance",
  "motionVmd": "motion.vmd",
  "positionScale": 0.08,
  "footIk": false,
  "loop": false
}
```

制作页的 **包内脚步 IK** 提供“播放器默认 / 启用（跟随 VMD）/ 始终关闭”，通常无需手写 JSON。

native 播放时，`footIk:true` 允许参考 PMX 按 VMD 的逐帧 IK 状态解析脚／脚尖，再按当前 VRM 的实际大腿和小腿长度重建腿链位置。脚踝与脚尖方向由 HumanPose 重定向写入，腿链解算会保留该方向；不要在其后再叠加一次 PMX 世界旋转，否则会造成脚掌 roll 或扭转。这样包作者只设置一次，换用不同身材的 VRM 时无需重新写脚位参数。`footIk:false` 会跳过 native IK 和目标腿链位置重建，仍保留动作中直接写入的 FK 脚踝／脚尖旋转；适合已经烘焙成 FK、或启用 IK 后出现重复约束的动作。

## 镜头参考

| 字段 | 类型 | 用途 |
| --- | --- | --- |
| `cameraReferenceEyeHeight` | 可选 number | 作者参考角色的静止视线高度，单位米；制作页自动获取，忽略滚轮显示缩放 |
| `cameraAuthoringScale` | 可选 number | 作者通过预览滑块调好的参考倍率 |

最终自动倍率为 `当前模型视线高度 / cameraReferenceEyeHeight × cameraAuthoringScale`。
例如参考视线 1.4 m、参考倍率 0.5，换为视线 0.7 m 的模型，得到 0.25 倍。

```json
{
  "formatVersion": 1,
  "id": "my-camera-dance",
  "motionVmd": "motion.vmd",
  "cameraVmd": "camera.vmd",
  "cameraReferenceEyeHeight": 1.4,
  "cameraAuthoringScale": 0.5,
  "positionScale": 0.08
}
```

制作页新建时获取当前角色视线，点击“获取当前模型”可重新标定；打开已有包时保留作者的参考值。开始预览后拖动参考倍率滑块，镜头实时变化，暂停也可调。保存修改或导出写入这两个数值。

制作预览始终按上述公式计算，个人镜头微调暂不参与，原设置保留。普通播放仍遵循设置页的自动换算开关和个人微调。

参考视线须为正数且不超过 10 米，参考倍率须为正数且不超过 100。滑块默认范围 0.01–4，打开倍率大于 4 的旧包时扩展范围以保留原值。运行时最终倍率范围 0.01–100。

没有眼骨时，利用静止身高按 `1.6 / 1.65` 估计视线。包没有参考信息时使用 1.65 m 视线与倍率 1。早期测试包中的 `cameraReferenceBodyHeight` 保留读取兼容，制作页转成视线后保存为上述两个字段。参考数值用于镜头，`referencePmx` 用于动作骨架。

字段是格式版本 1 的可选扩展；旧包继续可用，不支持这些扩展的播放器忽略参考信息。



## 缩放基准

缺少参考信息的包以及旧 unity3d／me 使用 1.65 m 默认视线；明确记录的参考值保留。模型内部骨骼比例在导入后保存，用于静止测量和 VMD 校准；Animator 根节点的显示大小单独由世界变换使用一次。运镜原点采用测量用的静止脚底位置。

格式仍只有上述镜头参考字段。旧版本测量有误的参考值需要在原参考角色上重新获取并保存。

## 借物表

可选 `credits` 字段为纯文本字符串，支持换行及链接，例如 `"credits": "Camera: XXX\nMotion: XXX"`。制作页按原文保存，打开包时恢复；省略时显示空白。格式版本仍为 1。
