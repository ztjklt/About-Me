# 大学校徽 Persona 与关键词结果交付

已完成「自我介绍 → DeepSeek 提取词条 → 真实联网搜索 → DeepSeek 校徽核对 → 飞盘生成 → 绿红结果 → 保存 → 失败重试 → 游戏内贴图」的本机流程。只使用学校真实校徽、徽章或 logo；校园、人像、建筑、其他机构标志不能通过视觉校验。不重绘学校标志。

## 最新版入口

双击 `scripts/start-university-crests.command`。使用最新版四气泡首页与 Persona 编辑页，默认 1280×720 窗口，关闭全屏。启动匹配的 `builds/university-crests-service`、本机权威服务和 `builds/university-crests-mac/About Me.app`，退出时清理本次启动的进程。复用 API 必须通过关键词结果、联网检索及最新版标记检查；不终止其他占用端口的服务。

源码以 `About Me DS Local Current` 的 `e5470e2` 为基线，当前功能已合入该项目。实现分支 `codex/persona-university-crests-latest-ui`；工作区 `/Users/jitian/.codex/worktrees/persona-university-crests-latest-ui/About Me`。既有其他改动保留。

## 可观察行为

- 点击生成后，下方显示原文关键词。提取和检索期间显示处理状态；真正获得可用道具后整块绿色，未生成则整块红色并显示原因。
- 不支持的兴趣仍保留红色；多事实共用一句来源不会借另一件道具的成功一起变绿。
- 学校搜索不限制预置校名、知名度、国家或 edu 域名。学校简称由 DeepSeek 识别；完整校名若已在关键词中，即使模型漏写 universities 数组也会进入检索。
- 搜索工具接入公开网页、多语言大学索引及百科文章索引。处理 HTTP 200 内的 maxlag 错误，遵守重试等待，并切换独立索引；使用公开 API 提供的标准缩略图，避免原图下载限流；下载有瞬时网络重试。可选 Tavily 搜索 API 供部署配置，不是本机使用前提。
- 真正经过 DeepSeek 图片校验、裁切和安全转码的 PNG 才成为可用飞盘。支持 SVG。没有找到可靠校徽则学校词条红色，不发出假校徽或无校徽占位飞盘。
- 校徽铺满飞盘双面，保留图案比例与内部文字。盘体、侧边使用校徽主要颜色；透明与白色画布不参与主色选择。
- 绿红结果与失败学校候选均私有保存，包含全红结果。全红时不能拿未生成的道具进入游戏。
- 相同介绍再次生成仅补失败校徽：保留既有道具身份和资料版本，成功项目使用缓存。显式重试会清除失败校徽及空搜索的短期缓存。全部成功时读取已有结果。
- 生成请求整体失败时保留已提取词条，标红说明结果未保存；不能把未保存的临时结果当作可用道具。关键词来源和失败候选不进入公开对局快照。

## 证据与实际覆盖

服务端 81 项自动检查覆盖词条真实性、实际道具匹配、进度鉴权、多校、原文重复、全红保存、红转绿重试、网络/索引回退、非校徽拒绝、安全 SVG 与缓存。最新版 Unity 15 项菜单、31 项玩法以及双面贴图/主色检查通过；另验证了全红结果的实际 PlayerPrefs 缓存并恢复原有测试前设置。

真实 DeepSeek 与真实公开搜索证据：`artifacts/persona-keywords-live-release/report.json`。湖北文理学院与 University of Otago 都未预置，均得到可核对校徽；首次约 2 秒显示关键词，完整生成约 23 秒。篮球绿色、小提琴因无已上线道具红色。UI 显示真实保存结果的窗口截图在该目录 `latest-ui-real-results.png`。

原生窗口验证 `artifacts/persona-keywords-runtime-audit/runtime-report.json` 检查两所学校 HTTP 贴图加载、原图像素一致、飞盘主色和覆盖、同外观换学校与隐藏后恢复。Mac 与 WebGL 已更新构建。后续闭环复核记录在 `artifacts/persona-keywords-cycle-audit-verified`；具体结论见 `docs/PERSONA_CYCLE_AUDIT.md`。

以上为本机真实模型/API、自动检查与原生客户端证据。微信真机、公网服务、真人多人以及公司 logo 未纳入本次验收。任意学校都会进入检索流程，但公开来源可能暂时失败、没有合适图片或无法确认归属，不能承诺所有学校都秒出；这种情况会显式显示红色且允许重试。

## 配置与复核

读取已有 `server/.env` 的 DeepSeek 配置。可设置 `UNIVERSITY_CREST_MODEL`、`UNIVERSITY_CREST_CACHE`、`UNIVERSITY_CREST_TIMEOUT`（默认 100 秒）和可选 `TAVILY_API_KEY`。图片成功缓存使用内容摘要与来源记录；搜索成功缓存 24 小时、空结果 60 秒，失败校徽默认 5 分钟，但用户显式重试不受失败缓存阻挡。

安装服务依赖包括 CairoSVG，系统需提供 Cairo 库；当前本机已可正常转换。新部署需按 `server/requirements.txt` 安装依赖。

在实现工作区运行 `.venv/bin/python -m pytest -q server/tests`。真实模型/搜索复核：`scripts/verify-persona-keywords.py --env <DeepSeek环境文件> --output <独立证据目录>`，验证进度、资料保存、内部验证及重复生成的身份稳定。匹配交付包需在 `builds/university-crests-service` 内运行服务测试，避免旧项目模块遮蔽新版模块。

构建入口 `AboutMe.Editor.UniversityCrestTests.BuildDeliverables`，设置 `ABOUTME_RELEASE_BUILD=1`、`ABOUTME_MAC_OUTPUT` 和 `ABOUTME_WEB_OUTPUT` 后团结引擎批处理执行。日志应使用绝对路径。
