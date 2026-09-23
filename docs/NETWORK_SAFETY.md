# 联机公开数据与服务端流量边界（本地原型）

## 数据路径

- 本人鉴权资料和游戏服务器内部保留 Persona `source`，用于生成来源核对。`Match.View` 对 Reveal、Ground、Carried 和本人 Inventory/Equipped 都生成独立的公开道具副本，资产字段只显式复制 `assetId`、`name`、`templateId`、`visualId`、`color`；不会修改权威状态中的原件。其他玩家无法通过快照拿到介绍原文。
- 客户端调侃请求只发送公开道具名称与已发布的模板、外观标识。`/roast` 禁止额外字段，拒绝包含明显联系方式、账号、住址、证件号码和攻击性词句的名称；不会把 `source` 转发给模型。未配置模型、模型调用失败时，客户端仅显示固定的安全文案。游戏服务器取历史档案时也复核公开名称，不合规的旧资料不能加入多人房间。
- Persona 生成入口在服务端校验来源与公开名称，规则见 [Persona 生成与公开边界](PERSONA_GENERATION_BOUNDARY.md)。调侃响应由服务端检查长度、单行格式、长号码、网址、联系方式及明确辱骂/色情/仇恨词；未通过时只返回固定安全文案，不展示原响应。

## 房间与额度

- Lobby 最后一人断线时立即删除房间。已开始的房间在全部连接断开后保留 120 秒供原身份重连，到期释放房间与 32 房容量；`--accelerated` 自动验收模式使用 3 秒宽限。
- SQLite `request_limits` 表跨同一数据库的 API worker 原子计数。`/sessions` 和 `/sessions/wechat`：同一网络来源每分钟 10 次，全局每分钟 120 次。AI 接口（生成、调侃、语音转写、图片识别、外观推荐）：同一网络来源每分钟 6 次，全局每分钟 30 次，且全局每天 500 次；超额返回 429。调侃另保留每身份 2 秒冷却。创建新身份仍共享网络来源和全局额度。
- 默认网络来源取 API 实际连接的 peer，不信任客户端自报的转发头。反向代理部署在 API 与 Caddy 环境中使用相同、未提交的 `ABOUTME_PROXY_KEY`；Caddy 覆盖写入 `X-AboutMe-Client-IP: {remote_host}` 和 `X-AboutMe-Proxy-Key: {$ABOUTME_PROXY_KEY}`。API 只在密钥匹配且客户端 IP 是单个合法 IPv4/IPv6 地址时使用该来源；其他请求仍按连接 peer 计数。Uvicorn 禁用默认代理头处理，API 容器端口不直接对外。全局额度仍生效；多 API 实例须共享同一数据库或改用集中式限流存储。

## 本轮可复现的本地证据（2026-09-23）

- `.venv/bin/python -m pytest server/tests -q`：31 项通过；覆盖 API 鉴权、生成边界、调侃输入/输出审核、可信代理来源、跨身份与跨来源额度。外部模型响应使用 mock。
- `sh scripts/editor.sh TestRules`：27 项规则及菜单/NPC 套件通过；Reveal、Ground、Carried 与本人 Inventory 的序列化 JSON 不含 `source` 字段，权威原件仍保留来源。
- `sh scripts/editor.sh BuildMac`、`BuildServer`、`BuildWeb`：macOS 客户端、Linux x86_64 专用服务端和浏览器包成功生成。Linux 文件尚未在 Linux 主机运行，浏览器包尚未做本轮人工交互验收。
- `python3 scripts/test-network.py --players 2 --port 7780 --output artifacts/network-integrated-2` 与 6 人版本：本机真实 WebSocket 自动对局通过；探针检查公开快照结构。
- `python3 scripts/test-network.py --players 2 --port 7790 --output artifacts/network-integrated-delay-reconnect --delay-ms 100 --reconnect`：独立代理施加单程 100 ms 延迟，房主断线后用原身份重连通过。
- `python3 scripts/test-room-cleanup.py`：本机真实 WebSocket 房间开局、全部断线、3 秒测试宽限到期释放容量、再次建房通过；正式宽限为 120 秒。
- `.venv/bin/python scripts/test-roast-contract.py`：读取这轮 Unity WebSocket 客户端保存的真实快照，核对 Persona 只有五个公开字段，再经 API 把公开道具字段发送到 mock 模型；模型只收到名称、模板和外观 ID，额外 `source` 请求获 422。此项是本地跨端契约验收，不是交互 UI 或真实外部模型测试。
- `API_DOMAIN=api.example.com GAME_DOMAIN=game.example.com PLAY_DOMAIN=play.example.com ABOUTME_PROXY_KEY=local-validation-only caddy validate --config deploy/Caddyfile --adapter caddyfile`：用测试域名和测试密钥解析生产 Caddyfile，配置有效。
- `.venv/bin/python scripts/test-proxy-quota.py`：本机运行 Caddy 2.11.4 与 Uvicorn，复用生产 API 反代块，以回环和本机局域网 IP 两个真实 TCP 来源各创建 10 个会话；两组额度分开，第 11 次分别返回 429。客户端伪造来源和代理密钥仍返回 429，直连 API 使用错误密钥不能绕过额度，`/internal/*` 经代理返回 404。脚本只测试本机 HTTP 反代；部署镜像固定 Caddy 2.10.0，尚未在 Docker/HTTPS/公网环境运行。

上述证据不覆盖异地公网、真人、微信真机或生产级语义审核。关键词规则可能漏掉隐晦个人信息和变体措辞，上线前仍需审核与滥用对抗测试。
