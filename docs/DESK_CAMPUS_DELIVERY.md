# 书桌上的记忆校园

> 本文保留首版交付及性能记录。最新的南侧补全、材质与灯光升级见 [V2 交付记录](DESK_CAMPUS_V2.md)；V2 预算为 12,087 三角面 / 8 个共享材质。

背景将现有校园置于巨大的书桌模型上，以书本、文具、照片和窗光建立微缩尺度，呼应玩具角色、Persona 与天降大手。校园建筑布局与对战规则保持原样。

## 已实现的环境

- 沿 `ground_main`、`entry_bridge` 和两块 `front_corner` 的实际轮廓向下补齐模型底座，保留入口两侧缺口。
- 桌面位于 Y=-14；原有玩家救回线为 Y<-8。实测坠落最低脚位为 -8.703199，在接触桌面前完成救回，并只掉落一个 Persona。
- 后侧三本粉彩书、左侧空心薄荷笔筒及三支铅笔、右侧立体照片架与几何风景图、前侧横放巨型铅笔。
- 四面房间围合、天花板、房间地面和大窗。窗口为低成本天空色块与稀疏云形，不使用完整背景图贴片。
- 所有背景均为可编辑三维几何，关闭实时投影、光照探针与反射探针，无碰撞、刚体、网络对象。

## 文件与重建

- 概念参考：`docs/previews/desk-campus/concept.png`，通过 imagegen 生成；没有调用 Tripo。
- 独立预制体：`client/Assets/AboutMe/Resources/DeskCampus/Backdrop.prefab`。
- 生成器：`client/Assets/AboutMe/Editor/DeskBackdropTools.cs`，编辑器菜单 **About Me → Build memory desk background**。
- 渲染与规则验收：**About Me → Review memory desk background**；批处理入口 `AboutMe.Editor.DeskBackdropTools.BuildAndReview`。
- 游戏和校园预览共用 `DeskBackdrop.Create()` / `ConfigureView()`；游戏运行时按材质合并为 8 个 Renderer，源预制体保留 83 个独立物件。合并网格随背景销毁时释放；背景总面数很低，采用较大的合并包围盒减少绘制调用。
- 新增远裁剪距离 420，线性雾 110–360；玩法地图数据、碰撞文件和服务器规则未加入背景内容。

现有场景与新背景的整合截图见 `artifacts/desk-campus/views/`。其中 `overview-16x9`、`overview-4x3`、`overview-wide` 分别检查三种画幅；一层与二层各覆盖 8 个方位 × 3 个俯仰角。

## 已完成验证

- 实际资源预算：**3,727 三角面、8 个共享材质、83 个可编辑 MeshRenderer**，低于 15,000 面限制。运行时 draw calls 应以批处理后的实测为准。
- 背景组件检查：0 Collider、0 Rigidbody、0 NetworkIdentity；所有 Renderer 禁止实时投影。
- 坠落救回通过，原有 26 项规则及 menu/NPC 套件通过。最终记录：`artifacts/desk-campus/review-final.log`、`asset-budget.json`、`fall-check.json`。
- 已刷新并检查 53 张截图，覆盖三种画幅、两层各 8 个方位 × 3 个俯仰角、两个环境机位。初查发现的原有二层穿墙由镜头任务修复；复查使用同一 `CampusGeometry.CameraPosition` 避障计算，原先钻墙角度恢复正常。
- 可随工程保留的实际引擎渲染：`docs/previews/desk-campus/in-engine-overview.png`、`in-engine-environment.png`、`upper-camera-review.jpg`。这些与 `concept.png` 概念图明确区分。

## 性能复测方法

开发版 macOS 传入 `--desk-audit --audit-output <绝对输出目录>`；WebGL 使用 `?desk-audit=1`，约 45 秒后在浏览器控制台读取 `DESK_AUDIT_REPORT`。浏览器验收通过 Codex 的后台浏览器标签页完成，无需安装额外自动化依赖。

验收模式为独立离线六角色场景，使用相同全景镜头。按背景关/开/开/关（ABBA）分别等待 2 秒、记录 8 秒，输出平均帧间隔、P95、绘制调用和 Unity 已分配内存；锁定 60 FPS。它不加入网络房间，不代表多人网络压力测试或手机性能。

两种状态均保留背景资源加载：内存比较反映启停时的运行状态，不代表背景资源的完整加载成本。原生绘制调用来自 Unity ProfilerRecorder，WebGL 绘制调用来自仅在验收模式启用的 WebGL draw 调用计数。不可用的计数明确标记，不补造结果。

## 首轮桌面实测与优化依据

2026-09-23，Apple M5 / Mac17,3，团结 2022.3.62t11 开发版，1600×900。背景 A/B 共 1,898 帧，在不同时运行编译与另一套性能测试的窗口采集。

| 背景 | 平均帧间隔 | 两轮 P95 范围 | 绘制调用 / 帧 | Unity 已分配内存 |
|---|---:|---:|---:|---:|
| 关闭 | 16.772 ms | 17.438–17.462 ms | 76 | 79.829 MB |
| 开启 | 16.779 ms | 17.266–17.345 ms | 116 | 79.825 MB |

在本次 60 FPS 封顶、静态六角色场景中没有可辨的帧率下降；不能据此把 0.007 ms 当作真实 GPU 成本，也不能把微小内存下降解读为节省内存。新增约 40 次绘制调用；移动端仍需要真机预算验证。

首轮采用 Unity 自动静态批处理，原始记录保留在 `artifacts/desk-campus/mac-auto-batching/runtime-metrics.json`、`player.log`。据此改为运行时直接按材质合并网格，最终结果另行复测。

隔离源码快照的合并验证显示：场景绘制调用由 76 增至 91，即背景增加 15 次，比首轮增加 40 次减少 62.5%。8 个 Renderer 不等同于 8 次底层绘制，渲染 pass 会增加调用。对应 `mac-concurrent-smoke/` 与 `verification-snapshot.json`；这一轮和主工程编译重叠，帧耗时不作为性能结论。合并前后实际截图已核对，外观保持一致。

已检查客户端全景、朝外镜头、二层朝外机位、大手预警/接触和预设观战机位截图。坠落复测通过。验收模式使用静态离线数据，不是完整真人对局。

为避免影响用户正在进行的原生试玩，尝试过无窗口 `-batchmode` 复测；该模式未提供有效绘制计数或截图，结果明确排除，保留于 `mac-batch-unrendered/EXCLUDED.md`。优化后桌面版的独占空闲帧耗时尚未复测；采用前述有效首轮 A/B 和优化后的实际绘制调用/截图作为当前证据，不宣称已完成空闲条件下的最终桌面帧率验收。

## 最终 WebGL 实测

2026-09-23 02:47（UTC+8），Apple M5 / Chrome 152 / ANGLE Metal，1920×1080，主工程 WebGL 开发包。通过后台浏览器实际渲染，ABBA 共采样 1,905 帧，并检查最终游戏画面。数据文件：`artifacts/desk-campus/web/runtime-metrics.json`。

| 背景 | 两轮平均帧间隔 | 两轮 P95 范围 | 绘制调用 / 帧 | Unity 已分配内存 |
|---|---:|---:|---:|---:|
| 关闭 | 16.668 ms | 17–18 ms | 41 | 53.806 MB |
| 开启 | 16.715 ms | 18 ms | 49 | 53.810 MB |

背景增加 8 次绘制调用，采样平均帧率约 60 FPS。浏览器测试期间没有停止用户的原生游戏或共享开发活动，因此这些帧间隔是当前机器负载下的观察值，不能将约 0.047 ms 的差值解释为背景独立耗时。内存两组均已加载背景资源，约 0.004 MB 差异不能代表背景资源占用。WebGL 坠落验收通过，最低脚位 -8.703199，仍在桌面 Y=-14 上方。

## 当前交付与剩余验收

概念图、可编辑预制体、游戏/地图预览接入、53 张引擎检查图、原生运行截图和桌面/WebGL 对比记录均已保存。Mac 与 WebGL 构建已成功。运行截图含大手预警、接触、两层朝外视角与预设观战视角，见 `artifacts/desk-campus/mac-concurrent-smoke/`；随源码保留的截图见 `docs/previews/desk-campus/`。

尚未完成的是优化后桌面版在空闲机器上的最终帧耗时复测，以及背景资源从未加载到加载后的完整内存增量测试。上述限制不影响当前几何预算、无碰撞约束、坠落规则与绘制调用结果。
