# 联机公开数据与服务端流量边界（本地原型）

## 数据路径

- 本人鉴权资料和游戏服务器内部保留 Persona `source`，用于生成来源核对。`Match.View` 对 Reveal、Ground、Carried 和本人 Inventory/Equipped 都生成独立的公开道具副本，资产字段只显式复制 `assetId`、`name`、`templateId`、`visualId`、`color`；不会修改权威状态中的原件。其他玩家无法通过快照拿到介绍原文。
- 客户端调侃请求只发送公开道具名称与已发布的模板、外观标识。`/roast` 禁止额外字段，拒绝包含明显联系方式、账号、住址、证件号码和攻击性词句的名称；不会把 `source` 转发给模型。未配置模型、模型调用失败时，客户端仅显示固定的安全文案。游戏服务器取历史档案时也复核公开名称，不合规的旧资料不能加入多人房间。
- Persona 生成入口在服务端校验来源与公开名称，规则见 [Persona 生成与公开边界](PERSONA_GENERATION_BOUNDARY.md)。调侃响应由服务端检查长度、单行格式、长号码、网址、联系方式及明确辱骂/色情/仇恨词；未通过时只返回固定安全文案，不展示原响应。

## 房间与额度

- Lobby 最后一人断线时立即删除房间。已开始的房间在全部连接断开后保留 120 秒供原身份重连，到期释放房间与 32 房容量；`--accelerated` 自动验收模式使用 3 秒宽限。
- SQLite `request_limits` 表跨同一数据库的 API worker 原子计数。`/sessions` 和 `/sessions/wechat`：同一网络来源每分钟 10 次，全局每分钟 120 次。AI 接口（生成、调侃、语音转写、图片识别、外观推荐）：同一网络来源每分钟 6 次，全局每分钟 30 次，且全局每天 500 次；超额返回 429。调侃另保留每身份 2 秒冷却。创建新身份仍共享网络来源和全局额度。
- 默认网络来源取 API 实际连接的 peer，不信任客户端自报的转发头。反向代理部署在 API 与 Caddy 环境中使用相同、未提交的 `ABOUTME_PROXY_KEY`；Caddy 覆盖写入 `X-AboutMe-Client-IP: {remote_host}` 和 `X-AboutMe-Proxy-Key: {$ABOUTME_PROXY_KEY}`。API 只在密钥匹配且客户端 IP 是单个合法 IPv4/IPv6 地址时使用该来源；其他请求仍按连接 peer 计数。Uvicorn 禁用默认代理头处理，API 容器端口不直接对外。全局额度仍生效；多 API 实例须共享同一数据库或改用集中式限流存储。

## 单一「使用道具」最终整合证据（2026-09-23，代码基线 `61470c8`）

- `sh scripts/editor.sh BuildDeliverables`：同版 macOS 开发客户端、WebGL、Linux x86_64 专用服务端构建通过；Linux 二进制只完成交叉构建，尚未在 Linux 主机运行。
- `.venv/bin/python -m pytest server/tests -q`：32 项通过，含英文 `gridiron football`、`notebook computer` 误配排除；模型成功场景使用 mock。共享与客户端目录逐项相同，共 60 个允许组合、52 项视觉资源，“纸质笔记本”名称一致；视觉校验 52 项、样例固定 8 项通过。
- `python3 scripts/test-network.py`：在 `builds/mac/About Me.app` 新构建上分别用 2/4/6 个真实本机 WebSocket 客户端运行，`artifacts/integrated-network-2`、`artifacts/integrated-network-4`、`artifacts/integrated-network-6` 的所有客户端均 `PROBE_PASS`，完成同步与结算；探针发送 `use_item` 并拒绝带 `source` 的快照。
- `python3 scripts/test-network.py --players 2 --port 7853 --output artifacts/integrated-network-delay-reconnect --delay-ms 100 --reconnect`：独立代理施加单程 100 ms 延迟，房主断线后用原身份重连并结算。
- `python3 scripts/test-room-cleanup.py`：新构建开局后全员断线，3 秒测试宽限到期释放容量并再次建房；正式宽限为 120 秒。
- `.venv/bin/python scripts/test-roast-contract.py --snapshot artifacts/integrated-network-2/room.txt.state.json`：读取新构建真实 WebSocket 快照，仅五个公开资产字段；API 向 mock 模型仅转发名称/模板/外观三个字段，含 `source` 的请求返回 422。
- `artifacts/IntegratedSoftCombat.log`：26 项权威战斗专项通过，包含旧 `light`、`charge`、`heavy` 输入无效、空手无攻击、公共道具同键投出。`artifacts/IntegratedPersonaFourClientAudit.log` 与 `IntegratedPersonaFourPlayerAudit.log`：四件新增道具装备、掉落、回收、吞噬和 `use_item` 效果均通过。`artifacts/IntegratedStartMenuStaticAudit.log`：静态人物与周边动效审计通过。
- `.venv/bin/python scripts/test-proxy-quota.py`：本机 Caddy 2.11.4 + Uvicorn HTTP 双 TCP 来源额度隔离、伪造头无效、`/internal/*` 404 再次通过；未在 Docker/HTTPS/公网运行。

以上是本机自动对局与离线规则证据；WebGL 包未做本轮浏览器交互，四入口未在本整合构建逐项点击，手机、真人与异地公网尚未验收。

## 首轮联机安全基线证据（`8e12edf`，保留作历史比较）

- `.venv/bin/python -m pytest server/tests -q`：32 项通过；覆盖 API 鉴权、60 项 Persona 目录的生成边界、调侃输入/输出审核、可信代理来源、跨身份与跨来源额度。外部模型响应使用 mock。
- `sh scripts/editor.sh TestRules`：27 项规则及菜单/NPC 套件通过；Reveal、Ground、Carried 与本人 Inventory 的序列化 JSON 不含 `source` 字段，权威原件仍保留来源。
- `sh scripts/editor.sh BuildDeliverables`：包含 60 项 Persona 目录、52 件可加载视觉资源及衣橱动效的 macOS 客户端、Linux x86_64 专用服务端和浏览器包成功生成。Linux 文件尚未在 Linux 主机运行，浏览器包尚未做本轮人工交互验收。
- `python3 scripts/test-network.py --players 2 --port 7820 --output artifacts/final-network-2`、4 人版本（端口 7840、`artifacts/final-network-4`）与 6 人版本（端口 7825、`artifacts/final-network-6`）：当时同版构建的本机真实 WebSocket 自动对局通过；探针检查公开快照结构。脚本等待服务端实际监听，避免冷启动竞态。
- `python3 scripts/test-network.py --players 2 --port 7830 --output artifacts/final-network-delay-reconnect --delay-ms 100 --reconnect`：当时同版构建经独立代理施加单程 100 ms 延迟，房主断线后用原身份重连通过。
- `python3 scripts/test-room-cleanup.py`：本机真实 WebSocket 房间开局、全部断线、3 秒测试宽限到期释放容量、再次建房通过；正式宽限为 120 秒。
- `.venv/bin/python scripts/test-roast-contract.py`：读取这轮 Unity WebSocket 客户端保存的真实快照，核对 Persona 只有五个公开字段，再经 API 把公开道具字段发送到 mock 模型；模型只收到名称、模板和外观 ID，额外 `source` 请求获 422。此项是本地跨端契约验收，不是交互 UI 或真实外部模型测试。
- 团结 `AboutMe.Editor.PersonaFourClientAudit.Run` 与 macOS 开发构建 `--persona-four-audit`：四件新增道具逐件检查装备、掉落、回收、吞噬和受控模板重击；审计按真实 `charge`→`heavy` 输入执行，样例仍为八件。此项是离线权威规则/资源验收。
- `API_DOMAIN=api.example.com GAME_DOMAIN=game.example.com PLAY_DOMAIN=play.example.com ABOUTME_PROXY_KEY=local-validation-only caddy validate --config deploy/Caddyfile --adapter caddyfile`：用测试域名和测试密钥解析生产 Caddyfile，配置有效。
- `.venv/bin/python scripts/test-proxy-quota.py`：本机运行 Caddy 2.11.4 与 Uvicorn，复用生产 API 反代块，以回环和本机局域网 IP 两个真实 TCP 来源各创建 10 个会话；两组额度分开，第 11 次分别返回 429。客户端伪造来源和代理密钥仍返回 429，直连 API 使用错误密钥不能绕过额度，`/internal/*` 经代理返回 404。脚本只测试本机 HTTP 反代；部署镜像固定 Caddy 2.10.0，尚未在 Docker/HTTPS/公网环境运行。

上述证据不覆盖异地公网、真人、微信真机或生产级语义审核。关键词规则可能漏掉隐晦个人信息和变体措辞，上线前仍需审核与滥用对抗测试。
