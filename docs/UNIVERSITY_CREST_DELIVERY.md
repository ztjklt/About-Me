# 大学校徽飞盘交付

已完成自我介绍中的大学名称提取、多校去重、DeepSeek 官网检索与校徽视觉核对、单个彩色校徽完整裁切、飞盘双面贴图、持久保存和投掷/掉落同步。只接受官网校徽，不采用校园照片，也不重绘学校标志。找不到可靠校徽时显示校名占位并提示重试，不造假校徽；同一介绍重试仅补校徽，不刷新道具身份。

## 最新版入口

双击 `scripts/start-university-crests.command`。默认 1280×720 窗口，强制关闭全屏。此入口启动同版 `builds/university-crests-service`、本机游戏权威服务和 `builds/university-crests-mac/About Me.app`；退出客户端后关闭本次启动的服务。复用服务前检查最新版标记，端口占用不会终止其他服务。

新版源自 `About Me DS Local Current` 的 `e5470e2`：四气泡首页、最新选择页背景、衣橱与 DS/Claude 角色保持最新版。实现分支 `codex/persona-university-crests-latest-ui`，工作区 `/Users/jitian/.codex/worktrees/persona-university-crests-latest-ui/About Me`。原工作树已有未提交内容保留。

## 验收范围

- 服务端 65 项自动测试通过，涵盖官网 URL、非校徽拒绝、重复大学、多大学、图片缓存、资料保存、内部身份验证，以及同文仅重试贴图。
- 最新 Unity 菜单/玩法和双面贴图检查通过（构建日志 `artifacts/UniversityCrestBuilds.log`）。
- 真实 DeepSeek + 中山大学、北京大学官网端到端验证：`artifacts/university-crest-final/report.json`，生成图片和持久化资料均在同目录。清华大学真实校徽验证结果仍在 `artifacts/university-crest-tsinghua-final`。
- Mac 原生运行验证真实 HTTP 贴图、逐像素一致、同外观大学切换与临时隐藏恢复：`artifacts/university-crest-runtime/runtime-report.json`。
- 此证据为本机 API/原生客户端和构建验收，不代表微信真机或公网多人验收。企业 logo 留给后续，当前优先实现大学。

## 配置与复核

使用已有 `server/.env` 的 DeepSeek 凭据。可配置 `UNIVERSITY_CREST_MODEL`、`UNIVERSITY_CREST_CACHE` 与 `UNIVERSITY_CREST_TIMEOUT`。图片使用内容摘要缓存；新学校由 DeepSeek 通过受控官网抓取工具动态检索，不限于预置学校。官网阻拦、网络失败或模型不能确认时会返回未就绪状态。

在实现工作区运行 `.venv/bin/python -m pytest -q server/tests`；真实调用验证入口为 `scripts/verify-university-crests.py`。构建入口 `AboutMe.Editor.UniversityCrestTests.BuildDeliverables`，设置 `ABOUTME_RELEASE_BUILD=1`、`ABOUTME_MAC_OUTPUT` 与 `ABOUTME_WEB_OUTPUT` 后以团结引擎批处理执行。

## 飞盘铺满与校徽主色（2026-10-03）

校徽覆盖整张飞盘正反面，自动排除透明画布与外围白色留边，保持原标志比例及内部白色细节。盘体和侧边采用真实校徽的主要有色部分，排除白色背景：中山大学绿色、北京大学红色。已有贴图和资料无需重新生成即可显示新效果。新生成 Persona 的主色同时写入资料并随既有协议同步。

窗口模式原生验证和截图在 `artifacts/university-crest-full-disc-runtime`；报告包含 `fullDiscCoverage` 和 `bodyUsesEmblemColor`。65 项服务测试与 Unity 满盘贴图、主色和最新版菜单/玩法检查通过。
