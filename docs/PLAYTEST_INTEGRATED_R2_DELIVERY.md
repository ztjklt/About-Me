# 完整试玩整合 R2 交付

完成：同一个 Release Mac 包同时保留红绿结果横向列表、10 题可选问答、直接输入与完整原稿保留、最新玩家设置和中英文切换，以及 274 道具／96 机制。修复了先前专项发布覆盖已交付 UI 的整合遗漏。

- 稳定入口：`scripts/start-latest.command` → `scripts/start-playtest-integrated-r2.command`。
- 发布客户端／同二进制权威服务器：`builds/playtest-integrated-r2-mac/About Me.app`。
- 匹配 API：`builds/playtest-integrated-r2-service`；独立端口 API 8087／权威 7787。未复制凭据或旧数据库；运行读取现有配置，新数据独立保存。
- 客户端源码：分支 `codex/playtest-ui-settings-integrated`，提交 `bbfc24f3316e5cd482c5dbd94e666bf312db9d36`，独立 worktree `/Users/jitian/.codex/worktrees/playtest-bug-fixes/About Me`。
- 继承已交付设置／语言源码快照 `d65bd4d44b678cbda5de357195af1daeb2d04832819611eec3f6a717eaf2b07a`。早先记录中的 4c6b79f 标识已校正为 d65bd4d（sourceFiles 排序 JSON 的摘要）；3895 个文件哈希完全相同，构建内容和时间未变。整合前逐项核对并冻结副本，不修改 language 工作区；排除它后来新增的 6 个未交付文件改动。
- 最终源码快照 `f51a4e750d0dcc1a8506d6f85d22d0cfc546831eea9a097fd59624b14b66a7bd`；逐文件哈希与原始来源见 `artifacts/playtest-integrated-r2/source-snapshot.json`。
- Release 程序集 SHA256：`facd47af1e833c560edac95c1687ed48c760cb695ecb7062372c2638e63e45c0`。
- API 文件树 SHA256：`254616819c746b3f944446c8f6c888d419681cb33bbd7f2b0ece51caa89f898e`。
- 构建时间与全发布包哈希见 `builds/playtest-integrated-r2-mac/release-manifest.json`。

## 发布门槛与验收

入口先核对清单与全客户端／API 文件哈希，检查程序集包含横滚、问答、语言／设置能力，核对274目录。端口占用时拒绝复用未知进程。启动后必须核对 API 发布标识、源码快照、文件树、274目录／96机制、监听 PID 和路径；权威服务核对同发布 binary 的真实 PID／路径。未来默认发布必须保留清单 `requiredFeatures` 的这些先前交付功能，不能以构建时间或专项包名替代完整整合验收。

实际 `--check` 启动身份检查通过，已清理本轮验收 API／权威进程，无验收 GUI 或端口残留。发布前重新核对了稳定入口与继承源码身份。

真实引擎 Development 客户端 27 项局部检查通过：21 卡混合红绿结果与末项、拖动／滚轮／横向触控板事件、生成刷新保留横滚位置；问答跳过／多选、编辑与去重追加、1200字超限完整保留；语言重绘保留原稿和可编辑草稿，英文草稿与新版英文设置，274目录保留。新增问答译文，沿用已交付输入组件对玩家文本的保护。正式发布为 Release；没有重做289设置项或完整玩法回归，也没有新增Web构建。

实际运行截图（Development，UI验收样例，不代表本轮真实AI生成）：

- `artifacts/playtest-integrated-r2/evidence/results-last.png`。
- `artifacts/playtest-integrated-r2/evidence/questionnaire.png`。
- `artifacts/playtest-integrated-r2/evidence/questionnaire-english.png`。

本轮主要桌面行为已由产品负责人验收。英文问答截图中的一条动态草稿反馈仍显示中文，记录为低优先级文案待补；不影响中文试玩。触屏只有指针事件证据，真人触屏、微信真机、公网与真人多人体验未验收。
