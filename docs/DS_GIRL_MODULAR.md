# DS 娘：原玩偶骨架上的服饰版本

本页记录此前已验证的服饰版本及其构建/性能证据。后续嘴位、眼部细节和睫毛增量，以及针对客户端 `abc89c4` 的新限定交付包，见 `DS_GIRL_FACE_V2.md`；旧运行包和性能数据不代表脸部增量后的完整构建验收。

本轮在独立 worktree `/Users/jitian/.codex/worktrees/ds-girl-modular/About Me`、分支 `codex/ds-girl-modular` 完成。基线为 `545b4ce` 加开始时原目录的 101 个已修改/新增文件。没有提交这些继承改动，没有修改原目录；不要把整个 worktree 的差异当作本轮改动合并。交接包只包含本轮文件及相对开始时工作树的补丁。

## 最终行为

选择已有的 DS 娘选项后，使用原玩偶的头、脸、身体和四肢，只把瞳孔改为蓝色，并装上深蓝渐变卷发、刘海、呆毛、发箍、蝴蝶结、女仆裙、鲸鱼围裙、白袜、蓝鞋及鲸鱼尾巴。原有表情、夸张吞噬、伸手、跑跳、攻击、受击、真实倒地与起身继续由原玩偶控制。

运行时复用 22 根身体变形骨和 15 根物理姿态骨。8 根服饰辅助骨只用于长发、裙摆和尾巴，不参与物理碰撞、权威判定或网络同步。手持道具、头部和背部挂点继续来自原玩偶；旧 DSGirlMotionAdapter 不再被 ToyActor 创建或调用。历史整模文件保留作档案。

沿用 `AppearanceData.version=1` 和 `character="ds"`，已有的解锁、外观保存及服务端检查保持兼容。DS 服饰为固定套装，隐藏的旧穿搭选项不会改变服饰内容。默认原玩偶仍可自由换装。

## 文件与使用

- 独立 macOS 试玩包：`builds/ds-girl-modular-mac/About Me.app`；双击 `scripts/start-ds-girl-modular.command` 打开。进入原有“我的校园穿搭”选择已解锁的 DS 娘；解锁条件未绕过。
- WebGL 输出：`builds/ds-girl-modular-web`，需要经 HTTP 提供，不能直接双击 HTML。构建结果见下方验收。
- Blender 源文件：`artifacts/ds-girl-modular/DSGirl_Modular.blend`，包含可独立编辑的配件、原玩偶拟合参考和完整穿着预览身体。
- FBX、Unity 网格与材质：`client/Assets/AboutMe/Art/DSGirlModular`。
- 配件 Prefab：`client/Assets/AboutMe/Resources/CampusDoll/DSGirlModular.prefab`。
- 装配组件：`DSGirlOutfit`。资源网格共享，不给每个玩家克隆网格；销毁/换装时显式清理挂在身体上的辅助骨。
- 制作源数据：`artifacts/ds-girl-modular/base-neutral.json`、`ds-base-neutral.json`、`accessories.json`。Blender 制作脚本和源身体整理脚本位于 `scripts/ds-girl-modular-*.py`。
- 引擎菜单：`About Me / DS Girl / Import modular outfit`、`Review modular outfit`；原 `Review in ToyActor gameplay` 已转入新版审查。

配件为 **10,762 个三角面、6,614 个顶点、零贴图、一个材质/蒙皮渲染器**。FBX 保留命名部件方便编辑，游戏使用严格按原骨骼坐标导入的合并网格；导入器检查权重归一化、面数和源/目标顶点一致性，并保留张嘴避让及吞噬腹部变形。此引擎的 Mesh 原生缓冲区不能只依赖 CopySerialized 更新，因此导入器逐项替换网格缓冲区，保留资源 GUID。

## 验收证据

| 检查 | 结果与实际覆盖范围 |
|---|---|
| 11 类动作 | 待机、走、跑、跳、轻击、重击、抓取、吞噬、受击、倒地、恢复，共 660 帧；新版与原玩偶身体骨骼位置/旋转、右手挂点差异均为 0 |
| 连续换装 | 编辑器确定性检查中，原玩偶与 DS 娘往返 20 次，无服饰或辅助骨残留，身体仍为 22 骨、物理姿态仍为 15 骨 |
| 实际物理姿态 | 420 个真实 Match tick，覆盖 Standing、Falling、Grounded、GettingUp；身体骨骼位置/旋转差异均为 0 |
| 原物理回归 | V4_BODY_VALIDATION_PASSED，含跌落、跳跃、撞墙、拖动释放、楼梯、六人堆叠和掉落保护 |
| 原规则与菜单 | 26 项规则测试、13 项菜单/NPC 测试通过 |
| 资源导入 | 所有权重归一化；导入网格与源 JSON 最大顶点差异为 0；配件低于 12,000 面预算 |
| macOS | 独立开发构建成功；实际运行截图和自动物理对局录屏已生成；运行日志未发现异常 |
| WebGL | Development 构建成功（DS_MODULAR_BUILD_OK WebGL）；本轮没有把构建成功等同于浏览器或微信真机运行验收 |

### 六角色性能

Apple M5 / Mac17,3，1280×720，目标 60 FPS，各运行约 14 秒、排除前 3 秒，关闭截图录制。两次是同一自动探针、同一场景和动作序列；这是短时本机测量，不代表全程真人对局或手机性能。

| 场景 | 平均 FPS | p95 帧时间 | 平均物理步耗时 |
|---|---:|---:|---:|
| 六个原玩偶 | 59.55 | 17.23 ms | 2.43 ms |
| 六个 DS 娘 | 59.64 | 17.18 ms | 2.62 ms |

证据：`artifacts/ds-girl-modular/perf-toy/metrics.json`、`perf-ds/metrics.json`。本次未观察到明显帧率退化，差异处于短样本波动范围。

### 预览与录屏

- `artifacts/ds-girl-modular/review/comparison.png`：新版与原玩偶并排。
- 同目录 `front.png`、`side.png`、`back.png`、`three-quarter.png`：实际引擎视图。
- 同目录 `actions-contact-sheet.jpg`：实际动作截图。
- `artifacts/ds-girl-modular/physics-motion.mp4`：真实物理 tick 的引擎近景渲染。
- `artifacts/ds-girl-modular/mac-runtime.mp4`：独立 macOS 玩家实际运行的 108 帧截图采样回放，约 14 秒；不是 60 FPS 原始录像。

上述六角色对局是同一进程内的本地自动物理测试，不是六客户端联机、真人试玩、异地联网或微信真机验收。服饰采用有限摆动与裙摆避让，不包含布料自碰撞；极端多人挤压下的全部姿态仍需真人试玩补充。手机与微信真机未验证。

## 来源与费用

造型参考由用户提供，几何与配色在 Blender 本地制作，基础身体来自本项目。未引入新的第三方模型或贴图。

Tripo 连通性检查失败后执行已批准的本地备用方案，未提交任何生成/处理任务。头发生成费用 0 点，裙摆生成费用 0 点，**本轮总消耗 0 点**。
