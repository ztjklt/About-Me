# 数据与协作约定

## 权威状态

`Match` 是唯一比赛状态来源。服务器固定 50 Hz 推进规则，20 Hz 发送接收者专属快照。客户端上传带递增序号的输入，服务器丢弃重复、过期和非有限数值输入，并按服务端时间计算蓄力和交互完成时间。

Persona 的长期 `assetId` 与本局 `id` 分离。状态为 Inventory、Equipped、Ground、Carried、Consumed。只有 Consumed 不计入原主人剩余数量；吃掉不会修改长期角色。玩家不能装备别人的 Persona，也不能通过客户端上报胜负或直接变更道具归属。

公开快照只包含玩家外观、当前装备、剩余数量、地面道具和效果。背包内容只发本人。大库存使用分片字节消息，避免 Mirror 字符串 64 KB 限制；底层 WebSocket 保证顺序。多人抢同一物品按服务器接收顺序裁定。

场景使用共享的校园地面、双侧楼梯和二层走廊几何。玩家移动与碰撞由规则层处理，视觉角色使用独立 Rigidbody/CharacterJoint 布娃娃。视觉骨骼不承担命中或胜负判定。

## API

| 路径 | 调用方 | 用途 |
|---|---|---|
| POST `/sessions` | 桌面客户端 | 建立设备角色凭据 |
| POST `/sessions/wechat` | 微信客户端 | code 换取绑定微信身份的角色凭据 |
| GET `/profiles/me` | Bearer 客户端 | 读取长期角色 |
| POST `/personas/generate` | Bearer 客户端 | 修改介绍后整体生成并保存 Persona |
| POST `/speech/transcribe` | Bearer 客户端 | 上传录音，返回待确认文字 |
| GET `/catalog` | 客户端 | 已发布模板与外观 |
| GET `/rooms/{code}/qrcode` | Bearer 客户端 | 生成微信房间体验码 |
| POST `/internal/verify` | X-Game-Key 服务器 | 校验客户端令牌、获取可信角色 |
| POST `/internal/results` | X-Game-Key 服务器 | 按 matchId 幂等保存结算 |

AI 返回 `personas[]`，每项包含 `name`、`source`、`templateId`、`visualId`、`color`；服务端校验并分配 assetId。重复 source 去重，不设置固定 Persona 数量。介绍最长 1200 字符、录音最大 10 MB 是输入资源边界。角色更新使用版本比较，防止并行生成覆盖较新的结果。

## 同伴加入

优先可独立接手：角色/道具美术、动作与音效、微信真机适配、AI 服务供应商适配。不要同时编辑主场景或修改相同规则文件。

提交 `Assets`、`.meta`、`Packages`、`ProjectSettings`，不要提交 `Library`、构建产物或 `.env`。原始 PRD 保存在 `docs/PRD.md`。新增模板先更新共享目录和服务器校验，再实现重击行为及客户端展示；不要让 AI 输出脚本。

提交前运行规则测试、API 测试和相关平台构建。改变网络协议时，服务器和客户端必须一起更新。运行脚本、测试日志和截图位于 `artifacts`，用于验收而非源代码。

## 单人、计时与结果（2026-09-23）

`IMatchSession` 提供 Current、Submit、Close。`LocalMatchSession` 在本机推进相同的 Match，不启动 Mirror 或 API；NPC 只接收自己的快照，走共享校园几何产生的路径，提交普通输入意图。交互可使用已有的 itemId 指定目标，仍需通过地面状态和距离验证。

标准 Reveal 5 秒；Battle 的 150、300 秒触发大手，450 秒先于该帧交互强制结算，停止计时并取消未完成交互。快照新增 endReason、winners、rankings、simulationTime、handStartedAt 和 handEventId；大手视觉按事件时间连续插值，伤害仍在权威层的接触时刻判定。

结果保留单人 winner 兼容字段，并新增 winners[]（允许并列）、rankings[] 和 endReason。API 按 matchId 保留首次写入，旧格式仍可接收。本地单人结果保存在 PlayerPrefs 的 lastSoloResult，不上传服务端。此版本的多人客户端和服务器应一起更新。

Mirror v96.9.23 本地补丁：ServerHandshake.CreateHash 对共享 SHA1 实例加锁，防止多个接收线程并发握手时产生错误 Accept Key。测试包含 512 次并发哈希校验和 2 / 4 / 6 客户端连接。
