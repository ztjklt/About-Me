# Claude 娘：原玩偶身体与独立服饰

按用户提供的橙发礼服参考制作可在 About Me 原角色上运行的模块化外观。保留原身体比例、脸型、22 根变形骨、15 根物理姿态骨与原手部/头部/背部挂点。没有替换人体、重做动作或改变碰撞体。

## 成品造型

橙铜色渐变长发、偏分刘海、暖棕眼睛与睫毛；头侧黑色蝴蝶结、米白发带和橙色放射花饰；米白披肩与衬衫、黑领结与花形胸针、黑色束腰、米白主裙与黑色裙片、橙色斜带、金色边饰与植物纹样、袖口交叉系带和黑鞋。

长发与裙摆收拢为原玩偶的活动尺寸。参考图手持书改成腰侧小书匣，作为读书主题装饰，不占用抓取/吞噬使用的手持挂点。花瓣、绣纹、金边均为少量几何，零贴图。左右裙花、领花和头花保持统一放射造型。

## 文件与运行

- 工作树：`/Users/jitian/.codex/worktrees/claude-girl-modular/About Me`；分支 `codex/claude-girl-modular`。
- Unity 资源：`client/Assets/AboutMe/Art/ClaudeGirlModular`，包含 FBX、Mesh、材质、Shader 与对应 meta。
- 装配 Prefab：`client/Assets/AboutMe/Resources/CampusDoll/ClaudeGirlModular.prefab`。
- Blender 源文件：`artifacts/claude-girl-modular/ClaudeGirl_Modular.blend`，包括独立命名服饰部件、原玩偶参考及穿着预览身体。
- 建模、源文件刷新与录像脚本：`scripts/claude-girl-modular-assets.py`、`claude-girl-modular-source.py`、`claude-girl-modular-video.py`。
- 编辑器入口：`About Me / Claude Girl / Import modular outfit`、`Review modular outfit`、`Review face details`。
- macOS 开发包：`builds/claude-girl-modular-mac/About Me.app`。`scripts/start-claude-girl-preview.command` 启动窗口化的 14 秒六角色自动物理演示并录制截图；这是自动测试演示，不是新的角色选择界面。
- WebGL 开发构建：`builds/claude-girl-modular-web`。构建证据单独记录，不代表浏览器、手机或微信真机验收。

## 装配与动作

外观渲染标识为 `character="claude"`。`AppearanceData.Resolve` 在原 `ds` 支持上增加该值，`ToyActor` 按标识调用 `ClaudeGirlOutfit.Attach`；原身体 Skin 始终显示。服饰不经过原身体网格合并，因此独立材质、权重及吞噬避让形变得以保留。

服饰使用 7 根仅负责外观的辅助骨：后发、左右侧发和四个方向的裙摆。运行时在最终身体/物理姿态后调用 `Pose(dt, opening, belly)`。裙摆对相邻两根辅助骨连续加权，随腿部姿态有限张开；长发有少量限幅摆动，在吞噬抬头时最多反向补偿 52° 并调整辅助骨锚点，避免整片长发从后背翻到胸前。所有改动限于外观骨，不写身体骨。切换前 `Release()` 显式释放辅助骨。

暖棕虹膜、瞳孔、双高光和上眼线/睫毛合入原身体蒙皮及眨眼/吞噬形变。静止嘴位沿用此前装配版本的下移 0.035 单位处理，完全张嘴恢复原开口；喂食手部目标补偿该视觉偏移。原表情、伸手和吞噬动作继续工作。

坐标与原玩偶一致：Y 向上，Z 向前，装配相对位置 0、单位旋转、缩放 1。原根节点约在世界 Y=1 时脚底贴地。不能套用历史整模角色的旋转或缩放补偿。

服饰 Mesh 为 **8,876 三角面、5,730 顶点、1 材质、1 个额外 SkinnedMeshRenderer**。眼部相对原眼部增加 1,004 面，因此新增外观合计 **9,880 面、零贴图**。Blender 源文件保留 244 个命名部件供编辑；运行时合并为一个服饰渲染器。

## 验收证据与边界

| 检查 | 本轮实际证据 |
|---|---|
| 正面、侧面、背面、三分之四与原玩偶并排 | `review/` 实际引擎 PNG |
| 11 类动作 | 待机、走跑、跳跃、轻重击、抓取、吞噬、受击、倒地、恢复共 660 帧；骨位最大差 7.73e-7 单位、角度差 0、手挂点差小于 1e-6，见 `pose-parity.json` |
| 原 DS 回归 | 同项目原 DS 路线再次通过 660 帧动作检查，见 `ds-regression-parity.json`；DS 资源与专属运行时代码未修改 |
| 连续切换 | 原玩偶↔Claude 20 次，再进行原玩偶/DS/Claude 20 轮三方切换；身体骨和右手挂点对象身份不变，碰撞体数量不变，无服饰/辅助骨残留，见 `switch-validation.json` |
| 脸部 | 6 种表情、眨眼、吞噬 120 帧；完全张嘴位置差 0、喂食手挂点最大差 9.10e-7，见 `face-review/face-validation.json` |
| 极限姿态 | 极限伸手与显式倒置引擎图，倒置服饰顶点有限且无异常拉长；显式倒置截图不是实际物理倒地证据 |
| 实际物理 | 420 个真实 Match tick，覆盖 Standing、Falling、Grounded、GettingUp，骨位/旋转差均为 0；近景视频为这些 tick 的引擎渲染，见 `physics-parity.txt` / `physics-motion.mp4` |
| 原物理回归 | `V4_BODY_VALIDATION_PASSED`，涵盖既有跳跃、跌落、撞墙、拖动、楼梯、堆叠与恢复检查 |
| 规则 | 26 项规则测试及 13 项菜单/NPC 测试通过 |

macOS 实际运行录像、六角色短时性能对比和 WebGL 构建结果见同目录 `runtime-validation.json`。性能录制关闭截图采样；运行录像单独进行，不能把录像时性能当作对比样本。所有对局证据为本地单进程自动测试，不是六客户端联机或真人多人试玩。

本轮两种构建均成功。Apple M5（Mac17,3）下，同一自动测试场景各运行约 14 秒并排除前 3 秒：

| 六角色场景 | 平均 FPS | p95 帧时间 | 平均物理步耗时 |
|---|---:|---:|---:|
| 原玩偶 | 52.99 | 17.42 ms | 2.80 ms |
| Claude 娘 | 59.46 | 17.06 ms | 2.83 ms |

短样本中未观察到明显退化；原玩偶平均 FPS 受少量慢帧影响，这组差异不能证明 Claude 版本更快。`mac-runtime.mp4` 是约 14 秒独立运行中取得的 107 张截图，以 10 FPS 播放为 10.7 秒的采样回放，不是连续原始录屏。

手机、微信真机、浏览器实际 WebGL 运行、真人对战、远程联机未验证。裙摆和长发使用限幅辅助骨，没有布料自碰撞；极端多人挤压下的全部接触姿态仍需后续试玩补充。当前完成角色资产与本地渲染接入，未发布线上。

## 限定交付与客户端接手

`ClaudeGirl-Modular-Delivery.zip` 包含限定补丁、新增文件白名单、源文件与证据。补丁只改 `CampusDoll.cs`、`DollSkin.cs`、`AppearanceData.cs` 和自动测试用 `BodyPhysicsRuntimeProbe.cs`，以本次开始时 DS 模块化脸部版本为基线，SHA256 在 `manifest.json`。当前 HEAD `545b4ce` 是旧仓库基线，不是成品提交；不要 cherry-pick HEAD 或复制整个工作树。工作树继承的 129 份未提交文件不属于 Claude 增量。

本轮没有自行决定 Claude 的解锁条件，也没有修改服务端校验、正式菜单或存档业务。客户端接入时需要在可信角色白名单、选择/解锁入口、存档和房间同步中支持 `claude`，并保持其当前默认角色标识（本隔离基线为 `toy`；若目标分支使用 `normal`，不要改回 `toy`）。原 DS 与 GPT 路线不应因本交付被覆盖。由产品负责人安排这部分整合范围。

## 来源与费用

造型参考为用户本次提供的图片，身体与动作来自项目自身，服饰、眼部及全部配色由本地 Blender/代码制作。未使用新的外部模型、位图贴图或在线生成服务。头发制作 0 点、服饰制作 0 点、配饰/绑定/导出 0 点，Tripo 总费用 **0 点**。
