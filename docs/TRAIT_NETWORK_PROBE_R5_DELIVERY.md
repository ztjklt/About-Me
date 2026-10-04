# R5 文字 Persona 联机验证入口

目标已完成：为正式 Release 增加显式 `--word-probe` 分支和独立真实 API 联机 driver。基线为 `083205c`，分支 `codex/trait-network-probe-r5`。仅修改 `Runtime/NetworkProbe.cs`，新增 `scripts/test-trait-network-r5.py`，不修改普通规则、公开协议、UI 或生成服务。

同一正式构建可运行两台本机客户端进程。默认通过真实 `/sessions` 与 `/personas/generate` 创建两个 QA 账号，不生成样例身份或替代模型；真实 AI 不可用时明确失败。服务端目录生成六类文字能力，driver 再通过真实 API 改选「理性」为护盾，要求原资产 ID 保留且保存 revision 增加。客户端分别优先装备热情喷火和温柔泡泡，以实际权威 effects 验证能力调度。

结构化结果包含 binary SHA-256、API 来源、两账号公有 playerId/资产 ID/三字段、房间及 matchId、protocol 4、权威 effect 能力、真实 15 骨 body poses 帧数、断线恢复身份、生命周期状态变化和观测到的 owner remaining/cooldown。两个客户端都必须看到实际生成资产，字段必须与 API 和彼此一致。公有快照与 `/internal/verify` 均检查隐私字段。自动尝试借用和释放地面文字，但未触发的借用或冷却继承不计作已验证，确定性的生命周期验收由主责正式 Match 审计覆盖。

## 主责整合接口

统一 Release 构建后运行 driver，传入 `--binary`、`--api`、`--output` 和空闲 `--port`。`GAME_SERVER_KEY` 从环境或私有 `--env-file` 读取。默认仅启动并结束 driver 自己的独立 authority/client；不复用占用端口，不停止用户游戏。已由主责确认的同版 authority 可显式传 `--authority-running`。

可传 `--qa-token-output` 指定证据目录外的私有文件，以 0600 权限保存两个 QA token，交给主责独立 API 重启验收。主责完成其独立 API 重启后，使用该私有 `--token-file` 和先前 `--verify-after-restart result.json` 再运行 driver；该模式仅发真实 authenticated GET，要求资产 ID、能力和保存 revision 完全一致，不启动游戏。凭据不进入证据或提交。driver 不自行控制 API 进程，避免误停用户服务。

## 当前验收证据和边界

- Python 语法编译通过；命令行入口正常。
- NetworkProbe C# 已用团结自带 Roslyn 对主责当前真实 `Assembly-CSharp.dll`、`Mirror.dll` 和团结 UnityEngine / netstandard 2.1 引用编译通过，未报编译错误。
- `git diff --check` 通过。
- 本子任务尚未运行最终 Release 双客户端，主责统一构建后执行。上述编译不能代替真实网络、公网、微信真机或真人验收。

未解决项：最终同源码 Release 双客户端与独立 API 重启实测由主责接手，结果文件会明确标记成功或失败；不声称已完成这些证据。
