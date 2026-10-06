# 完整 R15：GitHub 与 Railway 公网交付

发布目标是把本机完整 R15（近战两击击飞掉落、重击/投掷/范围/攻击性接触场地强化）连同 R13/R14 的 AI 词条、待机嘴型和五至七字调侃一起发布。

- 公网入口：https://web-production-fb9b1.up.railway.app/
- GitHub：https://github.com/ztjklt/About-Me；`main` 与 `codex/combat-impact-r15` 已推送完整版本。
- 公网发布源码：`f65ac1fc3863fd13c6b62b296ccc0710cd07f50b`。此提交仅在原 R15 上修正 API 发布身份；后续交付文档提交不改变运行源码。
- Mac/Web/Linux 客户端与权威运行代码：`7cf19de1e40ac1fc37b87f1fea2c6274b1a24721`，发布提交与其 `client/Assets`、`shared` 完全一致。
- Web 资源指纹：`e0a8408468297d00`；Linux 专用服务器归档 SHA256：`ecd9539953ff4175c89b4c6bf42bef8fb5fb631491114dafdcc95a77ef2be075`。
- 本机最新入口仍是 `scripts/start-latest.command`，完整本机构建/规则/物理证据见 [R15 交付](COMBAT_R15_DELIVERY.md)。

## 发布包与服务

冻结源码与专用上传目录在发布 worktree 的 `artifacts/railway-r15/`。API 只带冻结的 `server/`、`shared/` 和部署文件；Game 带本次 Release Linux Dedicated Server；Web 带本次完整 R15 WebGL 构建并注入同源 HTTPS API 与 `wss://game-production-8d73.up.railway.app`。专用上传必须使用 `--path-as-root --no-gitignore`，避免仓库的构建忽略规则排除发布文件。

本次仅修改三服务的发布 ID 与提交环境字段；原 API 持久卷 `/data`、模型配置和服务密钥保留。没有上传本机数据库或 `.env`。服务器内部 HTTP 仅由 Dedicated Server 构建标志启用；客户端策略保持原值。

| 服务 | 最终部署 ID | 状态 |
|---|---|---|
| web | `c5d18420-f617-47ae-9888-c2bc2ff1575d` | SUCCESS |
| api | `be00eab0-f08a-44c3-8750-84c94dd00eb4` | SUCCESS |
| game | `4085be13-5a5a-4ff2-8d94-f46a30f8c441` | SUCCESS |

## 已完成验证

- 532 项后端回归通过；Linux Release 构建成功，Railway Linux 权威实际启动并监听 7777，启动日志身份为 R15。
- 公网 `/version.json` 与本地发布清单一致；四份 WebGL 资源逐份下载并匹配 SHA256，首页显示“最新版 R15”，使用指纹参数；首页和版本端点 `no-store`。
- 公网 API 健康报告 R15、协议 5、277 道具、96 机制及准备调侃能力；`/api/internal/verify` 保持 404。
- 两个实际 R15 Mac 自动客户端通过生产 WSS 入房并进入 Battle；双方接收事件，权威快照含两人各 15 根骨骼，主机断线后以原身份恢复，恢复后继续收到时间超过 20 秒的战斗快照。结果记录在 `artifacts/railway-r15/public-checks.json`。此项为单一出发网络的联网/重连检查，未运行至整局结束。

以上为自动回归、公网 HTTPS 文件校验及 Linux 运行证据，不代表真人手感、微信真机或异地两种网络验收。二进制与验证日志保留为本机发布产物，不加入 Git。
