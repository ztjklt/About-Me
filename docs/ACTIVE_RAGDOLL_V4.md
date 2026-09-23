# V4 主动布娃娃与打击反馈

本轮未发起 Tripo 任务。保留 22 根变形骨、外观配置与 Persona 存档；沿用已有 Tripo 动作及本地动作目标。

## 实现

- `ActiveDollPhysics.cs`：每局独立 PhysicsScene，15 个动态刚体、14 个 ConfigurableJoint。隐藏动画骨架提供目标，物理骨架决定最后姿势，客户端只插值服务器姿态。身体采用统一的服装包络，外观不会改变物理尺寸或攻击范围。
- `BodyCombat.cs`：Standing → Stagger / Falling → Grounded → GettingUp → Standing。真实躯干接地后开始倒地计时，起身前检查当前位置附近的地面、墙体与头部空间。起身和其后 0.5 秒保护，释放拖拽。
- 站立时有限驱动力保持平衡；髋、胸和脚部肌肉追随动作。倒地时撤销直立支撑，四肢自由碰撞。贴墙仍被脚部支撑住时，用受限力矩辅助失衡，不指定最终摔躺姿势。起身使用本地仰躺/俯卧目标，经物理驱动恢复。
- 业务 Tick 为 50 Hz；高速接触或三人以上倒地堆叠时，一个 Tick 内使用两次求解子步，降低关节拉长。关节限制、相邻碰撞过滤、速度上限、投影与求解迭代共同控制稳定性。
- `DollPhysicsPresentation.cs`：15 骨姿态以 20 Hz 随快照发送，插值世界坐标骨骼；镜头跟随骨盆对应的位置，不跟头翻滚。网络协议为 3，客户端和权威服务器必须一起更新。
- `ImpactFeedback.cs`：确认命中后播放几何星芒/冲击环、落地尘团与合成音效；35 / 65 ms 顿挫只冻结参与角色的表现。设置中可关闭或调整震动，默认低档。

## 规则

击倒本身不掉 Persona。韧性耗尽掉 1 个并恢复至 65%；高处跌落在落地时清空韧性、掉 1 个，不立即回填。同次失衡中的击打与落地共用去重标志。二层跌到一层保持位置，掉出地图才返回 Arena。

大手、强力攻击、HeavyWeapon 重击以及蓄力至少 90% 的伤害型攻击携带击倒标志。牵引、辅助与屏障不凭技能身份自动击倒。躺地追击只产生推力，不重复扣韧性或重置倒地计时。

## 验证入口

Editor 的物理样例必须进入 Play Mode，所以执行以下入口时不要添加 `-quit`，入口在完成后退出：

- `AboutMe.Editor.BodyPhysicsTests.Validate`：真实物理、规则、楼梯、四向击倒、墙体、拖拽、堆叠、实际蒙皮和帧率检查。
- `AboutMe.Editor.BodyPhysicsTests.RenderReview`：正侧视角动作序列与 V3 预设姿势 / V4 物理姿态对比。对比左侧是现有 V3 姿势代码重放同一事件，不冒充旧安装包实录。
- 桌面构建 `--body-physics-probe --probe-output <目录>`：真实六角色 Match 与普通 ToyWorld 校园渲染；加 `--probe-capture` 记录画面。性能测量必须使用不截图的一次运行。
- `scripts/test-network.py --players 6 --binary 'builds/v4-mac/About Me.app/Contents/MacOS/About Me' --port 7796 --output artifacts/characters-v4/network-6`。
- 网络探针可加 `--delay-ms 100 --reconnect`，通过独立 TCP 延迟代理施加单程延迟，并断线重连原角色；不代表公网或手机实测。

构建输出由 `ABOUTME_MAC_OUTPUT`、`ABOUTME_WEB_OUTPUT`、`ABOUTME_SERVER_OUTPUT` 分别指定为独立的 `builds/v4-mac`、`builds/v4-web`、`builds/v4-linux`。旧试玩包不覆盖。

## 实际边界

手机流畅度需要真实设备验证；本机桌面与浏览器成绩不能代替手机验收。碰撞几何是统一简化包络，离线采样只能验证已覆盖动作，不能据此承诺任意多人组合绝对零穿模。录像渲染流程没有录制声轨；命中音效应在运行包中试听。

实现参考：[Unity 2022.3 关节驱动](https://docs.unity3d.com/2022.3/Documentation/Manual/configurable-joints-driving-forces.html)、[关节与布娃娃稳定性](https://docs.unity3d.com/2022.3/Documentation/Manual/RagdollStability.html)。

## 本轮实测结果

| 检查 | 结果与证据 |
|---|---|
| 引擎物理 | `artifacts/characters-v4/validation-release.log`：四向击倒、真实接地、墙体、上下楼梯、跳跃、倒地拖拽、六人堆叠、掉落去重、起身免伤通过 |
| 外观 / 帧率 | 50 个目录选项分别覆盖，30 / 60 / 120 FPS 的实际摔落蒙皮采样通过；最低顶点高于地面约 1.1 cm。不代表穷举全部服饰组合与所有多人接触 |
| 打击位移 | 平地基准轻击 1.06 m、普通重击 2.06 m；轻击略高于 1.0 m 目标上界，方向、接触和摩擦会改变实际位移 |
| 原有规则 | 26 条规则 + 11 条菜单 / NPC；25 条战斗 / 输入；API 20 条通过（仅上游依赖弃用警告） |
| 联机 | `network-2`、`network-4`、`network-6`：真实本机 WebSocket 通过；6 人局有实际吞噬结算 |
| 延迟 / 重连 | `network-delay-reconnect`：单程 100 ms 延迟，房主断线后同身份重连，外观与 15 骨快照验证通过 |
| 六角色原生性能 | `runtime-performance/metrics.json`：M5，1280×720，约 59.1 FPS，p95 17.40 ms；物理平均 2.16 ms、p95 3.15 ms。六角色校园渲染与物理诊断，未含菜单 UI；排除前 3 秒初始化，不截图的一次运行 |
| 网页实测 | `web-console.json`、`web-runtime.png`：主菜单 → 单人 → Reveal → NPC 战斗 → 退出，约 59.3–60 FPS，p95 17–18 ms。内嵌 Chromium 的锁定鼠标仍出现 UnknownError；Esc 释放后退出正常 |
| 构建 | `build-release.log`：独立 macOS / WebGL / Linux Dedicated Server 全部成功。Linux 仅构建，未在 Linux 主机运行 |

目录均相对项目根下 `artifacts/characters-v4`，另有机器可读的 `verification-summary.json`。桌面本机有既有试玩服务保留运行，因此性能不作为独占设备基准。

## 交付与启动

- 单人：打开 `builds/v4-mac/About Me.app`，选择「单人游戏 → 使用练习道具开始」。
- 联机：运行 `scripts/start-v4.command`，启动独立 7794 权威服务及 8094 网页；不结束旧版进程。原生连接地址仅在本次进程覆盖，不自动写回旧版设置。网页多人连接在设置中填 `ws://127.0.0.1:7794`。
- 客户端与服务器必须都使用 V4；V3 协议会明确拒绝混连。
- `v4-actions-comparison.mp4` 为引擎渲染的正侧动作序列，末段比较 V3 预设姿势代码重放与 V4 物理结果。

- `v4-campus-capture.mp4` 是新版原生窗口的校园镜头截图序列，按录制文件时间间隔合成；采样录制不代表游戏实际帧率。`runtime-fall-sheet.jpg` 展示其中的撞地、摊躺与起身连续画面。
- `scripts/start-v4.py --no-open` 已实际启动并正常清理独立服务；原 API 与旧试玩服务保留。
- 可分发压缩包：`builds/v4-mac/AboutMe-V4-macOS.zip`。
