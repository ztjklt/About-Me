# 当前 DS 娘与真实 AI 整合候选

此候选的唯一源码分支为 `codex/ds-local-current`。最终包应从 `git rev-parse HEAD` 得到的同一提交构建；在记录 SHA、构建时间与包散列后，才可把 Mac、Web、Linux Game Server 和后续微信导出称为同版产物。旧包、旧 API 或旧公网对局的成功记录不能代替这一版验收。

| 功能 | 当前源码位置 | 验收所需证据 |
| --- | --- | --- |
| 最新四气泡首页、战斗音乐、校徽飞盘与 DeepSeek Vision 完整校徽 | `GameUIVisual.cs`、`BattleMusic.cs`、`GameModel.cs`、`server/app.py` | 同版客户端画面、校徽服务请求和实战投出 |
| DS 娘 | `GameUIAppearance.cs`、`CampusDoll.cs`、`server/easter_egg.py` | 真实资料免费解锁，衣橱显式选装并保存，重进仍在；未触发 Persona 模型 |
| 真实 Persona 与身份 | `server/app.py`、`GameUI.cs`、`GameNetwork.cs` | 模型生成 200、重新读取相同资料、Game `/internal/verify` 使用同一身份 |
| 八件 Persona 实体投掷 | `GameModel.cs`、`BodyCombat.cs`、双份目录 | 命中归零、起身、掉落、回收与吞噬；校徽飞盘独立效果仍正确 |
| 权威吞噬调侃 | `GameNetwork.cs`、`server/app.py` | 同版两个真实身份客户端看到同一 AI 句子；Game 物理不等待模型，样例房不触发付费调用 |
| 晚到事件和网页输入 | `GameUI.cs`、`GameUIVisual.cs`、`IMEInputBridge.cs` | 结算后调侃可见；网页房间码输入与加入 |

本机入口与 API 生命周期见 [本机 Persona API 启停](LOCAL_AI_API.md)。Mac 包使用 `ABOUTME_QA_ISOLATED=1` 和 `ABOUTME_MAC_OUTPUT` 构建到 `builds/ds-local-current-qa/About Me Integration QA.app`，默认 1280×720 窗口。Linux Game Server 与 Web 包可分别通过 `BuildServer`、`BuildWeb` 从同一提交构建。`BuildWeChat` 需要微信 AppID、CDN 地址和已安装的指定 WXSDK；没有微信真机结果时记录为未验收。
