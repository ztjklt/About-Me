# 本机试玩 UI R1 交付

本轮目标完成：红绿 Persona 结果区支持两行横向滚动，不截断结果；新增可选的 10 题多选模式，原直接输入和现有 AI 生成流程保留。问答草稿可编辑，只有明确点击「追加到自我介绍」才加入原稿，重复句子去重；切换、关闭和追加前不改原稿或当前 Persona。沿用已有 1200 字输入限制，超限追加显式拒绝，完整原稿和完整草稿都保留。

## 版本与入口

- 稳定最新入口：`scripts/start-latest.command` → `scripts/start-playtest-ui-r1.command`。
- 发布包：`builds/playtest-ui-r1-mac/About Me.app`；Release，独立应用标识 `com.aboutme.party.integrationqa`，可与原试玩并存。
- 客户端源码分支：`codex/playtest-bug-fixes`；完整源码提交：`205fcd22fcb4d81909f575d5327a48c0fc8abfa9`。
- 独立源码工作区：`/Users/jitian/.codex/worktrees/playtest-bug-fixes/About Me`；继承整合基线 `049312fe7917d82198cb94e305ad1a3415a52810`，未复制其他角色未提交源码。
- 修复提交：`27a1dcc` 横向结果布局；`e8d1ec1` 鼠标轮与触控板映射；`5e70068` 可选问答；`205fcd2` 必要交互验收及入口文字可见性修正。
- 同一发布 binary 同时用于客户端和权威服务器；API 复制经核对的 `049312f` 交付源码到 `builds/playtest-ui-r1-service`，271 件道具、90 项机制。新增仅本机 `/local-release-version` 诊断入口，不改变 Persona 生成或对局 API 协议。
- 独立端口：API `8086`，权威 WebSocket `7786`；占用时拒绝复用，启动后核对监听 PID、真实进程路径、API 源码树哈希及版本标识，再打开客户端。
- 不复制凭据、旧数据库或旧缓存；启动时读取现有 `server/.env`，新数据使用 `artifacts/playtest-ui-r1`。不同 API 地址的账号/资产数据独立。
- 原 `university-crests` 包和运行进程保持；本轮发布未自动启动新版服务或切换试玩窗口。

## 可核对身份

发布清单：`builds/playtest-ui-r1-mac/release-manifest.json`，副本与结果证据位于 `artifacts/playtest-ui-r1`。

- Release Assembly-CSharp SHA256：`3c4720be4bf113864ad3a768b924d9c99630adf451f3d87c672460ee68da2a70`。
- Release 客户端文件树 SHA256：`3c56b367a5eac9a60cc4a3a1f9002c20cbdcbeb3cc73a292fe746bfb06c96125`。
- API 文件树（含本机版本诊断）SHA256：`aaf060ac31797e763cdf5e4e2f4090ab7937d70073e45e4e4a5571c964e32f3d`。
- 构建源文件快照 SHA256：`a13f83f0d31422817b8b4067639ee483122778750ae09d909af670b93adbdeb0`。

`artifacts/playtest-ui-r1/source-snapshot.json` 记录构建源码逐文件哈希。未提交差异只来自该隔离工作区构建流程生成的场景、资源导入与设置，不包含其他角色开发改动。引擎 `2022.3.62t11`，`ABOUTME_RELEASE_BUILD=1`、`ABOUTME_QA_ISOLATED=1`；构建与打包时间分别记录在清单中。`scripts/start-latest.command --verify-only` 已通过全发布包哈希校验及脚本语法验证，不启动进程。

## 桌面证据范围

Release Mac 构建成功，31 项规则和 15 项菜单检查通过。同源码 Development 客户端在 1280×720 和 960×540 实际运行，各通过 22 项 UI 验收：21 张红绿混合卡保留、最后一张完整可见、鼠标轮/横向触控板事件、指针拖动路由、生成进度刷新保留滚动位置、滚动条范围；10 题/40 选项均匹配实际目录、全部跳过/多选、原稿逐字保留、现有 Persona 不替换、编辑草稿追加、重复去重和超限原稿/草稿保留、原输入和生成按钮仍可用。问答操作不触发原生成按钮，AI 仍由该按钮明确触发。

最终截图来自同源码 Development 实际客户端，画面带 Development 标记；正式发布的是 Release 包。图中多卡结果为明确的 UI 验收样例，不是本轮真实 AI 生成成果。

- 最后一张结果：`artifacts/playtest-ui-r1/evidence-1280/results-last.png`。
- 问答面板：`artifacts/playtest-ui-r1/evidence-1280/questionnaire.png`。
- 原输入保留：`artifacts/playtest-ui-r1/evidence-1280/original-input-preserved.png`。
- 小窗口同类截图与最终通过项：`artifacts/playtest-ui-r1/evidence-960`。

本轮桌面证据已由产品负责人验收。触屏仅验证真实引擎指针事件；真人触屏、手机/微信真机、公网和新版独立端口首次真人联机尚未验收。未新增 Web 构建，也未重做完整玩法回归。
