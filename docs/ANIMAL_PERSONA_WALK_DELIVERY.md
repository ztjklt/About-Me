# 小动物骨骼走动 R7 候选交付

四种动物已完成实际骨骼绑定、蒙皮和循环迈步动画，并进入同源 Mac / Web Release 候选。工作区 `/Users/jitian/.codex/worktrees/animal-persona/About Me`，分支 `codex/animal-persona`。根目录默认仍为完整 R5；正式整合与切换入口由产品负责人指定。

## 成品与行为

![实际 Mac 客户端录制的四种骨骼走动](../artifacts/animal-persona-animation/walking.mp4)

视频从左至右是仓鼠、兔子、狗和猫，1600×650、30fps、4 秒，来自实际发布客户端连续 120 帧，未使用生成概念帧。原始帧在 `artifacts/animal-persona-animation/runtime-audit/walk-frames/`。

- 猫、狗采用 Tripo 四足行走骨骼和动画，各 21 骨。兔子保留 Tripo 的 21 骨蒙皮，在本地制作直立迈步；仓鼠保留原网格、UV 和贴图，本地绑定 7 骨并制作直立迈步。兔子和仓鼠是玩具式直立走法，尚无四足动作或脚部 IK。
- 追击时驱动实际蒙皮四肢，跟随权威位置和朝向；持握、抛物飞行与落地恢复静止姿态。动画采用运行时可采样的 Legacy 骨骼片段，关闭自动播放和根运动；动画不决定目标、碰撞、伤害或生命归属。
- 原有 R6 行为保留：随机奔向就近有效玩家、一次命中击飞，原 Persona 实例落地，玩家按现有规则拾取／回收／吞噬，投出的道具不自动回手。协议仍为 5。
- 四件各低于 6000 三角面；真实静止网格最大尺寸 0.78 米、脚底对齐地面。运行资产 `client/Assets/AboutMe/Art/TripoAnimals/*/Animation/walk.fbx` 和 PersonaProps 四个预制件；本地可编辑工程在 `art-source/animal-persona-animation/`。

## 授权与实付费用

用户批准本轮追加 105 credits。实际消费 **95**，余额从 24,715 到 **24,620**，frozen=0。猫 35、狗 35、兔子 25、仓鼠 0。兔子付费动画任务失败，10 credits 已退款；未付费重试，兔子与仓鼠的本地制作不消耗 Tripo credits。前轮四个模型花费 200，本任务模型与动画累计 295。

| 动物 / 工序 | 任务 ID | 实付 credits |
|---|---|---:|
| 猫绑定 | `0338fbbb-3991-4f72-a247-1322554861de` | 25 |
| 猫四足 walk | `9395a1ef-bb19-4b2d-93e2-11263398f4f1` | 10 |
| 狗绑定 | `02336d6f-1db7-4be2-acd6-b4162dc16438` | 25 |
| 狗四足 walk | `e90edd99-1683-4646-8ebd-b956e8c93056` | 10 |
| 兔子绑定 | `bef66302-0a23-4ddb-bd89-b45b5cfef86c` | 25 |
| 兔子 walk，失败并退款 | `7bc88629-89ae-4d3b-9a08-6a55360df1b6` | 0（退款 10） |
| 兔子本地动画 / 仓鼠本地绑定与动画 | 本地 Blender 5.1.2 | 0 |

Rig API 模型 `v2.5-20260210`，猫狗动画 `preset:quadruped:walk`，in-place。逐任务来源、实付、退款和最终 FBX 哈希在 `docs/ANIMAL_PERSONA_ANIMATION_RESULTS.json`、`artifacts/animal-persona-animation/animation-deliveries.json` 与各动画 `provenance.json`；不包含密钥或签名下载 URL。免费检查、查询、下载未计费。

## 验收证据与实际覆盖

1. 编辑器骨骼专项 **28 项通过**：四件实际动画、骨数、权重归一、真实网格尺寸和脚底对齐、四肢变形、变形幅度上限、导入容器保持、循环接缝及停止后恢复。结果 `artifacts/animal-persona-animation/unity-animation-checks.txt`，运行日志 `unity-animation-final.log`。
2. 权威动物／投掷规则和原软战斗、爆风、控制、防守回归 **113 项通过**，日志 `rules-final.log`；此层是规则夹具。Mac Release 原规则 **31 项**及菜单／NPC **15 项**通过。
3. 实际 Mac 发布客户端专项 **26 项通过**：四种网格、贴图、真实骨骼和发布环境中的蒙皮变形、四种实际连续动画渲染、中英选择、持握、BodyWorld 追击击飞落地、同一实例生命归属、四个角色的 15 刚体快照与协议 5。结果和截图 `runtime-audit/passed.txt` 及同目录。这是构造对局自动验收，非真人试玩。
4. 最终 Mac 包的两个实际客户端通过本机 WebSocket 自动对局，观察到 **476 / 477** 个物理帧、两个外观、吞噬与结算，记录 `network-2-final/`。此为通用多人回归，未专门验收双客户端动物命中同步。
5. Mac / Web 成对构建前后 **4360** 个 Assets / Packages / ProjectSettings 文件哈希一致。构建和完整打包文件哈希、API 发布身份、实际 PID、路径、277 件目录及 96 机制均通过核对；`paired-build-check.txt` 与 `service-check.txt`。验收服务已清理。

本轮服务端源码和目录没有改动，API 的 470 项与真实 AI 动物匹配沿用 R6 证据，本轮只复核配套服务身份和健康目录。Web 编译成功尚未做浏览器运行；微信真机、移动性能、公网、真人手感和专门的双客户端动物命中同步待后续测试。

## 构建身份与交接

- 候选 `playtest-animal-walk-r7`：`builds/playtest-animal-walk-r7-mac/About Me.app`、`builds/playtest-animal-walk-r7-web/`、`builds/playtest-animal-walk-r7-service/`。
- 构建客户端源码提交 `91acb2332e08f5a8d6d3db93d5c6243f5715f74a`；打包脚本源码提交 `de8a1f472e19f768612da6cacfbcd94e59065a60`。客户端源码在后者与构建提交完全一致，未提交辅助项仅 `.venv` 本机环境软链接和免费检查 `tripo-out/`，未纳入源码快照。
- Mac 构建 UTC `2026-10-05T13:08:37.006430+00:00`；Web `2026-10-05T13:10:36.199757+00:00`。
- 完整冻结快照 `1f05bbda299f3a492e868b8079ae264323112ca9f6594f86218ab16435ccf352`，位于 `artifacts/animal-persona-animation/source-freeze/`；精确清单及构建哈希在 Mac 目录的 `release-manifest.json`。随包附 R6 继承 manifest，候选验证无需安装 R6 旧包。
- 候选入口 `scripts/start-playtest-animal-walk-r7.command`，API **8095** / 权威 **7795**，同包协议 5。支持 `--verify-only` 及 `--check`，拒绝复用未知端口服务。首次启动可一致备份已有 R6 或 R5 SQLite 资料到独立 R7 数据目录；密钥仍由既有 `server/.env` 管理。
- 复现构建需在冻结源码中保持场景不变，设置 `ABOUTME_EXISTING_SCENES=1`、`ABOUTME_RELEASE_BUILD=1`，分别执行团结 `AboutMe.Editor.ProjectTools.BuildMac` / `BuildWeb` 并将平台输出指向 R7 目录。

交由产品负责人验收成品并指定专业角色整合；正式交付须同时发布 Mac、Web、配套服务、新脚本与默认入口记录。未覆盖或提交根目录、其他角色工作区的未提交工作。当前没有阻塞候选交付的问题；兔子与仓鼠的自然四足运动、真人手感和跨设备性能仍需专项迭代。
