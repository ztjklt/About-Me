# 本地交付与验收状态

更新：2026 年 9 月 23 日，新加坡时间。引擎：团结 2022.3.62t11。

**当前交付是可运行、可继续开发的本地原型。原始 PRD 仍是目标，尚未完成全量验收。** 用户已确认先完成本地交付；本阶段不等待公网服务器、模型账号或微信 AppID。

## 2026-09-23 普通角色与 UI 最终整合

当前源码为 `codex/unity-default-menu-integration` 的 `18ca3f4`，最终同版产物为 `builds/reviewed-windowed-mac/About Me QA.app`、`builds/reviewed-windowed-web`、`builds/reviewed-windowed-linux/AboutMeServer`。三平台在同一次 `BuildDeliverables` 运行中构建成功；Mac 包为独立 QA 存储域，默认 1280×720 窗口。原试玩包及旧构建没有被替换。首次启动、返回和重开使用暖色教室四气泡首页；普通角色脚底、转身、单键道具平衡、背包/快捷栏与触控界面已整合。DS 娘和 GPT 娘未在本轮修复。

前一源码提交 `52ff497` 的 Mac 包在本机 2/4/6 客户端 WebSocket 自动对局中均完成结算；此后只修改设置页地址标签，未重跑网络对局。这是同机自动化证据。编辑器脚部、身体、移动和开场静态审计，以及桌面 1600×900 美术截图属于专项证据；最终 QA 的真实操作范围和未完成项见 [整合交付记录](TUANJIE_CHARACTER_UI_INTEGRATION.md)。以下章节记录旧 `61470c8` 基线及历史专项，不能将其中的构建路径和测试数字误作本轮最终包结果。

本轮整合代码与构建基线为 `codex/network-deployment-hardening` 的 `61470c8`：合入客户端单一 `use_item` 攻击与四入口开场、后端英文误配排除词和“纸质笔记本”名称；游戏服务、客户端与 API 采用同一份 60 项 Persona 目录和 52 项视觉清单。样例对局仍固定使用八类各一件；真实生成按有效介绍返回可变数量，原型单人上限为 12 件。最终同版构建位于本 worktree 的 `builds/mac`、`builds/web`、`builds/linux`，详细证据见 [联机安全记录](NETWORK_SAFETY.md)与[四件客户端交付](PERSONA_FOUR_CLIENT_DELIVERY.md)。

## V4 主动布娃娃专项基线

V4 专项历史输出为 `builds/v4-mac/About Me.app`、`builds/v4-web`、`builds/v4-linux`。15 刚体 / 14 关节权威物理已接入；真实摔躺、落地起身、倒地拖动、击倒与掉落去重通过当时的物理验证，网络协议升级为 3。当时的 2 / 4 / 6 人本机 WebSocket、100 ms 单程延迟及原身份重连记录保留在专项文档；本轮最终整合构建另行复测了 2 / 4 / 6 人和延迟重连。

专项六角色校园原生渲染实测（Apple M5，非手机）：约 59.1 FPS，p95 帧时间 17.40 ms；权威物理平均 2.16 ms、p95 3.15 ms。网页两角色约 60 FPS / p95 17–18 ms。这些性能数值未在本轮 60 项目录整合构建重测。内嵌 Chromium 的鼠标锁定 UnknownError 仍可出现；手机真机及 Linux 主机运行未验收。完整范围和证据见 [V4 记录](ACTIVE_RAGDOLL_V4.md)。

## V3 试玩历史记录

当时含贴墙镜头修正的独立 macOS 更新包位于 `builds/mac-next/About Me.app`；此处仅保留 V3 专项记录。本轮 `builds/mac/About Me.app` 已由最终整合构建替换。

当时已接入鼠标直接转镜头、四方向双击奔跑、22 骨柔性关节表现、真实 Tripo 基础动作、轻重击轨迹、双向抓取拉力、Q 持续挣脱及 NPC 抓取冷却；其旧测试数字见 [软物理战斗 V3](SOFT_COMBAT_V3.md)，不作为本轮整合构建的验收数字。镜头贴墙避障与近距离角色遮挡已处理。

这是受控软身体表现和服务器根部拉力，不是全身 PhysX 主动布娃娃。测试证据、原生鼠标自动化限制、尚待真实设备确认的手感与性能详见 [软物理战斗 V3](SOFT_COMBAT_V3.md)。

## 本轮菜单与单人对战

主界面现在是 About Me 四项菜单；自我介绍移入独立预设页。单人游戏使用本地 Match 加一名 NPC，可在 API 与游戏服务器均关闭时完成一局。多份介绍草稿与一套当前 Persona 分离；提供重玩、镜头灵敏度、触控显示和减少动态效果设置。详见 [菜单与 NPC 验收](MENU_SOLO_DELIVERY.md)。

**本轮调整后的规则覆盖原始 PRD 的旧时间配置：**5 秒展示，战斗第 150 / 300 秒大手，第 450 秒强制结算；取消 Final Burst。到时先比较存活者吞噬数，再比较剩余 Persona，完全相同则并列。提前只剩一名存活者仍直接获胜。客户端键鼠 F 与单个触控按钮、NPC 和自动对局统一发送 `use_item`；空手不攻击，公共道具由同键投出，旧 `light`、`charge`、`heavy` 输入被权威 Match 忽略。

## 已交付

| 产物 | 位置 | 说明 |
|---|---|---|
| macOS 游戏 | `builds/mac/About Me.app` | 开发构建，可作为客户端或无界面样例服务器 |
| 浏览器游戏 | `builds/web/index.html` | 通过本地 HTTP 服务打开，WebSocket 连接游戏服务器 |
| Linux 权威服务器 | `builds/linux/AboutMeServer` | x86_64 Dedicated Server，已编译；未在 Linux 主机运行验收 |
| 本机启动入口 | `scripts/start-local.command` | 启动样例服务器、API 和网页服务；终端 Ctrl+C 关闭本次服务 |
| 团结工程 | `client` | 可重新生成五个入口场景，提交文本资产与 .meta |
| AI / 语音服务 | `server` | Python HTTP、SQLite、真实服务调用接口及边界测试 |
| 部署与协作 | `deploy`、`docs` | Docker、HTTPS/WSS 配置、接口说明、原始 PRD |

样例模式使用服务器发布目录中的 8 个测试道具，以便覆盖八类机制。**这不是 AI 生成数量限制，也不是实时 AI 结果。** 当前共享与客户端目录有 60 个允许的模板/外观组合、52 项可加载视觉资源；真实生成按有效内容返回可变数量，原型单人最多 12 件。没有配置模型服务时明确报错。

## 验证记录

| 验证项 | 结果 | 可复现证据 |
|---|---|---|
| 团结编译和构建 | 最终同版 macOS、WebGL、Linux x86_64 成功；Linux 未运行 | `artifacts/BuildDeliverables.log`；`sh scripts/editor.sh BuildDeliverables` |
| 权威规则 | 27 项基础规则及菜单 / NPC 套件通过，覆盖快照不含 `source` | `artifacts/BuildDeliverables.log` 中的 `ABOUTME_RULE_TESTS_PASSED`；可单独运行 `sh scripts/editor.sh TestRules` |
| Python API | 32 项通过，模型成功场景使用 mock | `.venv/bin/python -m pytest server/tests -q` |
| Persona 资源和样例 | 52 项视觉校验、四件新增道具编辑器/玩家构建 `use_item` 审计通过；样例仍为 8 件，纸质笔记本名称一致 | `artifacts/ValidatePersonaVisuals.log`、`IntegratedPersonaFourClientAudit.log`、`IntegratedPersonaFourPlayerAudit.log`、`IntegratedSampleProfileAudit.log` |
| 2 客户端真实本机连接 | 单键最终同版完成连接、状态同步及结算 | `artifacts/integrated-network-2` |
| 4 客户端真实本机连接 | 单键最终同版完成连接、状态同步及结算 | `artifacts/integrated-network-4` |
| 6 客户端真实本机连接 | 单键最终同版完成连接、状态同步及结算 | `artifacts/integrated-network-6` |
| 100 ms 模拟延迟及重连 | 单键最终同版，独立代理施加单程 100 ms，房主原身份重连并结算 | `artifacts/integrated-network-delay-reconnect` |
| 全断线房间回收 | 测试宽限 3 秒后释放并可再次建房；正式宽限配置为 120 秒 | `artifacts/room-cleanup`；`python3 scripts/test-room-cleanup.py` |
| Persona 公开边界与调侃 | 新构建 Unity WebSocket 快照仅五个公开资产字段；API 仅向 mock 模型传名称/模板/外观，额外 `source` 请求获 422 | `.venv/bin/python scripts/test-roast-contract.py --snapshot artifacts/integrated-network-2/room.txt.state.json` |
| 单键攻击权威规则 | 26 项软战斗测试通过，含旧 `light/charge/heavy` 无效、空手不攻击及公共道具同键投出 | `artifacts/IntegratedSoftCombat.log`；`AboutMe.Editor.SoftCombatTests.Run` |
| 四入口开场动效 | 静态人物与周边图层审计、减少动态效果的菜单规则测试通过；最终整合构建尚未逐项手动点击 | `artifacts/IntegratedStartMenuStaticAudit.log`、`BuildDeliverables.log`；[客户端同代码候选截图](start-menu-bubbles/home-1600x900.png) |
| 本机反向代理 | Caddy 2.11.4 与 API 在本机运行，两个 TCP 来源额度分离、伪造头不能拆分额度、`/internal/*` 返回 404 | `.venv/bin/python scripts/test-proxy-quota.py`；不代表 Docker/HTTPS 公网验收 |
| WebGL 与 macOS 协议互通 | 旧构建曾通过浏览器与桌面客户端互通；最终整合 WebGL 本轮只验证构建 | [旧版记录](ACTIVE_RAGDOLL_V4.md)；最终版仍待浏览器交互复核 |
| 本地启动脚本、界面及响应式布局 | 旧构建曾通过本机入口和多尺寸截图检查；最终整合 UI 本轮未逐尺寸重测 | `artifacts/local`、`artifacts/ui-*.png` 为旧证据；微信真机仍待验收 |

自动对局使用明确的测试客户端与加速配置，真实发送操作意图、接收服务器快照并等待 Finished；没有把模拟状态直接写入结算。它们不是异地真人测试。旧版曾以标准配置运行完整 450 秒战斗计时，记录两次大手和到时结算（`artifacts/menu-audit/schedule.log`）；这是两个静止角色的历史计时验收，不能当作本轮真人手感测试。API 中外部服务成功场景使用 mock；不计作真实 AI、语音或微信验收。本机 Python 3.14 本轮有 16 条上游弃用警告，不影响 32 项通过；容器固定 Python 3.12。

规则测试覆盖可变资产数量、地面与持有资产计数、库存隐私、争抢唯一性、交互中断、最后吞噬与单次结算、回收身份、优先装备掉落、坠落、不可达救回、重连、非法输入、Reveal / 到时结算、抓取不直接转移资产。

## 已实现的原型功能

- 文字介绍与账号资产接口、整体重生成校验、长期保存、完整背包与五格快捷栏；12 类受控机制、60 项允许组合，其中四件新增 CC0 道具复用现有模板效果，样例只选八类各一件。
- Persona 仅接受介绍原文中可定位的事实，同一事实只生成一个；学校信息统一为带校名的校徽圆盘，人物配色不参与 Persona 推断。介绍限制为 1200 字并提供明确计数与超时提示。
- 联机资产公开投影只含 `assetId`、`name`、`templateId`、`visualId`、`color`；`source` 留在本人鉴权资料与服务端。调侃请求只传公开道具字段，API 做输入与输出安全检查、会话与 AI 额度限制；本轮为原型边界，仍需正式内容审核。
- 移动、跳跃、第三人称镜头、单键使用已装备 Persona 或投出公共道具、空手抓人、拉扯手持 Persona、削韧、击飞与简易布娃娃表现。
- 回收 / 吞噬计时与打断，地面资产保留、边界救回、坠落复活与掉落、只在吞噬完成扣除生命、淘汰观战、幂等结算。
- 熟人房间、Ready、开局代表 Persona 展示、周期大手、到时排名与并列获胜、断线角色保留和身份重连；开局后全员断线的房间在 120 秒宽限期满后释放容量。
- 房间支持 2–6 人；达到 2 人后房主可直接开局，Ready 只显示玩家状态，不构成开局门槛。
- 桌面 / 浏览器 / 微信平台桥接代码、录音上传、微信身份和房间二维码接口；触控控件与键鼠共用操作协议。
- 完整玩具贴纸派对 UI：统一主题、程序化九宫格和图标图集、按钮状态、模态遮罩、轻量动效、安全区、六人卡片、韧性与交互进度、Persona 卡片背包和吞噬清单结算。
- 自我介绍预设页区分真实 AI 状态与固定本地样例；原生、WebGL 和微信输入分别启用 IME / 浏览器或平台键盘桥接，支持中文组合输入。

## 尚未完成与限制

| 项目 | 当前限制 / 后续验收 |
|---|---|
| 真实 AI 和语音 | 旧版曾在本机用真实中文介绍验证 DeepSeek 文本生成；本整合 worktree 没有配置供应商凭据，本轮 32 项 API 使用构造输出或 mock，语音识别仍未配置，真实录音转写待验证 |
| 微信小游戏 | 官方 WXSDK 已固定版本，桥接和构建入口已接入；缺少 AppID / CDN / 域名 / 真机，未导出可用体验版、未验证微信 WebSocket 与录音权限 |
| 公网与 Linux | Linux x86_64 仅交叉构建，未在目标 Linux 主机运行；未部署 Docker/HTTPS/WSS，未做局域网双设备或两地 2/4/6 人互联网验收，也未测真实网络扰动。Caddy→API 的双来源额度仅在本机 HTTP 运行验证 |
| 美术与动画 | UI 已完成玩具贴纸派对升级；校园与角色资产见专项交付文档。大手已使用预警、平滑进场、接触停留和退场；完整音效、配乐及多人手感仍需打磨 |
| 战斗手感 | 命中与移动采用服务器简化几何；前后摇、强碰撞掉落、抓取稳定性、镜头遮挡、模板差异和数值平衡仍需迭代，不等同完整物理沙盒 |
| AI 外观表达 | 已限制模型、颜色和模板目录；按语义生成尺寸 / 装饰组合的丰富表达尚未完成 |
| 单局节奏 | 新版 150 / 300 秒大手与 450 秒到时结算已经标准计时验收；真人对战平衡仍需迭代 |
| 性能与包体 | 当前为开发构建，WebGL 目录约 87 MB；本轮整合未重测 FPS、内存或微信包体，不沿用旧专项数值宣称当前桌面 60 FPS / 手机稳定 30 FPS |
| 容错与运营 | 游戏服务器重启不恢复进行中比赛；无生产级内容审核、流量保护、完整账号恢复或运营后台 |
| GitHub | 本轮整合分支已在本地提交，尚未推送该分支到远程仓库 |

后续应优先完成真人本地手感迭代与正式美术表现，再接入真实 AI / ASR、公网部署和微信真机验收。不要以样例内容、同机测试或编译成功替代对应外部功能的验收。
