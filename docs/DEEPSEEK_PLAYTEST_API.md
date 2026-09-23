# DeepSeek 公网试玩 API 契约

## 配置与部署

API 使用单副本 SQLite。容器监听 `0.0.0.0:8081`；设置 `ABOUTME_DB=/data/aboutme.sqlite3`，并保证运行用户能写挂载卷。浏览器通过 Web 同源 HTTPS `/api/*` 代理访问公开接口；代理应剥离 `/api` 前缀并拒绝 `/api/internal/*`。Game 服务经私网 `http://api.railway.internal:8081` 调内部接口。`GAME_SERVER_KEY` 只配置在 Game 与 API 服务端。

文本生成与调侃共用服务端环境变量 `LLM_BASE_URL`、`LLM_MODEL`、`LLM_API_KEY`；本轮实际验证使用 DeepSeek 官方主机及模型 `deepseek-v4-flash-vision-exp`。密钥不进客户端、镜像、仓库或日志。`DEEPSEEK_API_KEY` 是单独的图片识别配置，不是文本生成/调侃的必需变量。`GET /health` 返回非秘密的 `aiConfigured`、`aiProvider`、`aiModel`。缺少文本模型配置时生成和调侃返回 503。

公开试玩建议将 `ABOUTME_SESSION_GLOBAL_PER_MINUTE=60`、`ABOUTME_SESSION_PEER_PER_MINUTE=10`、`ABOUTME_AI_GLOBAL_PER_MINUTE=12`、`ABOUTME_AI_PEER_PER_MINUTE=6`、`ABOUTME_AI_DAILY_LIMIT=30` 作为初始预算；实际值由部署者按试玩人数调整。超额返回 429，非法额度配置返回 503。内部权威调侃计入 AI 全局分钟和每日额度，不按 Game 私网的单一 peer 限制；同事件缓存重读不重复扣额。来源识别和代理密钥见 [联机安全边界](NETWORK_SAFETY.md)。CORS 只允许配置的浏览器来源，不承担访问准入。

## 客户端公开接口

| 路径 | 鉴权 | 输入 | 成功 | 失败 |
| --- | --- | --- | --- | --- |
| `POST /sessions` | 无 | `{nickname}` | `{token,profile}` | 422 / 429 |
| `POST /personas/generate` | Bearer token | `{introduction,nickname,color,outfit}` | 持久化 Profile，`personas[]` 为经模型调用及服务端校验的结果 | 401 / 409 / 422 / 429 / 502 / 503 / 504 |
| `GET /profiles/me` | Bearer token | 无 | 本人 Profile，含 `source` | 401 |
| `GET /health` | 无 | 无 | `aiConfigured`、`aiProvider`、`aiModel` 等非秘密状态 | 服务不可用 |

生成 200 必须经过真实文本模型响应和来源、目录、内容边界校验。学校单事实也走文本模型，再由服务端统一映射为 `Disc/school-disc`，并经 DeepSeek Vision 核对完整校徽与裁切后保存。本地样例按钮独立于此 API，不把样例结果标成真实 AI。生成仅保存通过校验的 Persona；失败保留旧资料。游戏服务器通过 `POST /internal/verify`（`X-Game-Key`、`{token}`）取得可信 Profile，按真实身份入房。

## 权威吞噬调侃

Game 服务对真实场次的每个新 `EatRecord`，以 `MatchId` 和 `instanceId` 为事件键，异步调用 `POST /internal/roast`。请求头 `X-Game-Key`；正文只含 `match_id`、`event_id`、`item_name`、`template_id`、`visual_id`，前两者为 32 位十六进制字符串。服务端拒绝额外字段、私密 `source` 与不合规名称。成功 200 返回 `{event_id,roast,aiSource:"provider"}`；相同事件及道具重复请求返回同一句且不重复调用模型。正在生成或同键不同道具返回 409；不合规输入 422，限额 429，缺配置 503，超时 504，模型或内容审核失败 502。失败不缓存假短句。

Game 只在成功且房间/MatchId 仍有效时，将短句写入 `room.Say`；现有 `Snapshot.events` 向全房同步同一句。客户端不直接为多人事件调用 `/roast`，也不先显示固定假调侃。Game 的 50 Hz 权威物理不等待模型。样例身份场次跳过付费调侃。既有 Bearer `POST /roast` 保留兼容，但不用于多人广播。

## 验证范围

`server/tests` 覆盖模型缺配置、审核错误、事件幂等、并发处理中拒绝、额度及资料持久化的本地 mock/构造场景。`scripts/probe-real-ai.py` 使用虚构介绍、本机未提交配置和临时数据库，实际调用一次 DeepSeek 生成与一次调侃；只输出 provider/model、数量、持久化及缓存检查等元数据，不输出介绍、Persona、短句或密钥。该探针不证明 Railway、WebGL、真人或公网多人链路；这些由部署和 QA 继续验收。
