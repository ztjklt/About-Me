# 小动物与投掷后掉落规则

状态：玩法源码已实现、权威规则检查通过；真实小动物模型待 Tripo 扣费确认，尚未整合到试玩构建，不是完整成品交付。

主责：本轮小动物 Persona 玩法／资产接入。工作区 `/Users/jitian/.codex/worktrees/animal-persona/About Me`，分支 `codex/animal-persona`。基线为完整 R5 发布源码 `be0350ace0a582973120ad8429d20c8838c15a28`；没有复制或覆盖根目录及其他角色的未提交改动。

## 可观察规则

- 预留小猫、小狗、兔子、仓鼠四个受控视觉 ID。现阶段仅已有猫是发布目录中的资产；新模型到位后才发布其目录和选择界面，不用占位几何冒充真实建模。
- 动物投出后短暂抛物飞行，再从 12 米内、无遮挡、非原投掷者、在线且可命中的玩家中随机选取目标。候选为最近三人，且距最近者的距离差不超过 2.5 米；选择后保持目标，目标失效才重选。追击速度 7 米/秒，总飞行追击最多 4 秒。一次命中走原权威 Hit 和真实刚体接口，击飞后原实例落地，不持续攻击。
- 无人、路径受阻、超时、原投掷者淘汰时原动物实例落地；受到阻弹防守可停下或转向。角色起身保护与遮挡保持。
- Projectile、Disc、Ricochet、Deployable、Summon 五种投掷模板均移走原装备实例。普通飞盘不再飞回原主；击中或落地后只有一件原 Persona，没有新建或消耗生命。必须主动回收才能再次使用。
- 接触爆风、黏附爆风、吸入瓶、倒计时橡果、可撕投网、公共物滚动球也使用投出的原实例。后续爆开、拆除、失效或命中后落地；既有持续效果按原规则结束。泡泡、喷火等手持器具产生的效果和非投掷部署继续使用原机制。
- instanceId、assetId、ownerId 与生命归属不变。原主 E 回收，其他玩家 E 拾取并按原流程吞噬；使用一次不等于永久删除这件生命资产。
- 客户端和权威规则协议升至 5，禁止和已发布 R5 协议 4 混连。API Persona schema 保持兼容，新增资产必须同步 shared/catalog.json 与 Resources 副本。

## 验证

团结引擎 2022.3.62t11 实际编译与执行：新增 20 项权威数据规则通过；既有软战斗 34、爆风 19、控制 21、防守 19，共 93 项回归通过。合计 113 项。平地／墙体夹具，50Hz，未启用 BodyWorld；这是权威规则检查，不是实际双客户端、真人手感或手机验收。

日志位于本工作区 `artifacts/animal-persona/authority-final.log` 和 `regression-final.log`。保留首次失败日志：首次新增测试的回收断言未考虑原机制会自动装备找回物品，已修正为允许原实例 Inventory/Equipped；旧橡果测试要求空手玩家显示手持冷却，已改为验证实际原实例被投出，仍完整验证倒计时、公共抓取和扔回。

复验入口：团结 batchmode 执行 `AboutMe.Editor.AnimalPersonaTests.Run` 与 `AboutMe.Editor.AnimalPersonaTests.Regression`。

## 模型生成计划与后续

精确输入和免费 dry-run 结果：`docs/ANIMAL_PERSONA_MODEL_PLAN.json`。四件依次为橘猫、柯基、白兔、金丝熊。每件 Tripo CLI `tripo-p1`，API wire model `P1-20260311`，6,000 面上限、标准贴图；每件两次 API 调用：`/v3/generation/text-to-model` 约 40 credits，`/v3/models/convert` FBX、1024 贴图约 10 credits。四件预计 200 credits，失败任务退还费用；实付以任务结果为准。GLB、FBX、preview.png、task.json 保存在 `artifacts/animal-persona/tripo-out/`，审核后导入 Unity 并保留生成来源。此计划不包含付费骨骼动画。

Tripo 连接及余额查询通过：24,915 credits，0 frozen。尚未提交付费生成，实际费用 0。网络需沿用本机系统代理 `127.0.0.1:7897`；不记录密钥。

待本聊天用户确认生成预算后继续生成和逐个目视审核模型、接入持握／投出／落地资源、增加动物选择问卷和中英目录、构建并验证匹配版本。正式整合由产品负责人指定责任人；完成新整合包后同步默认入口、README 与发布身份记录。目前默认最新试玩仍为原完整 R5，不称当前源码已进入试玩包。真人、微信、移动设备、公网和骨骼奔跑动画均未验收。
