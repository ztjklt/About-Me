# 完整R5：文字 Persona 与 AI 角色整合

状态：完整Mac/Web Release已构建，本机桌面行为验收通过，独立完整R5发布。默认入口更新只影响下次启动，当前R4窗口与数据保留。

## 继承与身份

完整 R4 来源 `baef9a5b7b7a87b10318a81812e5aeeceaae6259f187fa7380d0680602c58c29`，全部 3923 源文件逐项核对，隔离冻结提交 `78208762df19afa2bea556858690ec6b141b4edd`。主责分支 `codex/trait-persona-r5`，工作区 `/Users/jitian/.codex/worktrees/playtest-bug-fixes/About Me`。保留 R4 紧凑介绍、横向结果、每题自填、角色预览、原输入保护、实际设置与中英功能；274 实体目录、96 条机制保持。文字复用六条已有规则，不声称新增六个独立机制。

## 本轮行为

- 性格、表情和16个可选MBTI类型成为实际 `SpecialAbility/trait-word` 资产；单玩家总计不超过12件，类型不增加战力。能力为喷火、泡泡托举、护盾、冷静罩、牵引或弹床，可单件显式改选；选择后仍须 Generate 成功保存。
- 字形源为现有字体 CFF 轮廓，含正面、背面、外缘与孔洞侧壁，真实0.16厚度，紧包围触发式 BoxCollider。战斗碰撞仍走原权威范围/遮挡/刚体路径；展示碰撞体不会自行推动玩家。缺字、非法字符或超长词明确失败；内部空格为advance，空白不假造字形。
- 原持有者 F 使用、打落、抢夺、回收、吞噬沿用实际规则。仅文字新增 R 借用/T 放下，E 显式吞噬/原主回收，触控有分开的按钮。借用保持实例 ID、ownerId、生命归属和冷却，不增加借方生命，不会因仅借用原主最后一件而淘汰原主；失去装备立即取消依赖持有的持续能力。普通道具 E 的规则保持。
- API 可选 `wordAbilities:[{text,category,abilityId}]`，旧请求兼容；服务校验类别、可显示字符与六能力白名单，不运行 AI 战斗代码。同一句不同事实分别保留，同义与友好微笑复合去重。未知自填未确认显示“请选择能力”，成功 assetId 在改选/补旧缓存中保留。公开快照不广播介绍、source或生成中间项。
- 游戏匹配协议4，旧客户端/权威组合拒绝；API环境地址在 Awake 首请求前应用。

## 完整整合与正式成品

八项来源按各自 Delivery.zip、manifest、限定补丁与新增路径白名单语义合入；未整树覆盖任何其他工作区。GPT 使用最新灰紫眉修订。保留普通玩偶、DS娘、Claude娘，衣橱共11个真模型选项；10个AI偏好只提供资格，必须主动选择保存才换模型。API、权威与客户端共用 `shared/ai-skins.json` 白名单，保存与重连同步；未知AI不假造模型。每题自填、MBTI和能力问答保留，AI可选备注中英文显示。

来源精确包/文件哈希见 `artifacts/ai-girls-r5/asset-origins.json` 与 [AI整合记录](AI_GIRLS_R5_INTEGRATION.md)。Gemini/Grok原交付未提供完整源码树hash，记录其实际zip/manifest/白名单身份，不推定不存在的完整源码身份。

正式独立包：`builds/playtest-trait-r5-mac/About Me.app`、`builds/playtest-trait-r5-web`、`builds/playtest-trait-r5-service`。运行代码冻结提交 `77ae8388da17366eadedc1be2a1a66d47a744cb8`，后续发布脚本/文档提交未改生产运行代码；精确完整提交、源码快照、dirty范围及所有Mac/Web/API文件hash，以 `builds/playtest-trait-r5-mac/release-manifest.json` 为准。Mac最终程序集SHA256 `5ffb127d98c5aa7fde93f229faab420d6af821e0172fa7ebdf69e87405061ec1`。Web采用相同运行源码IL2CPP构建，平台二进制不等同Mac托管程序集。

## 实际验收范围

结构化报告 [acceptance-report.json](../artifacts/trait-persona-r5/acceptance-report.json)；保留首次失败报告与修正依据。

- 463项API集成/单元检查全部通过，mock provider范围；并有独立真实配置LLM代表输入，HTTP实际返回热情、友好微笑、ENFP、篮球4件，原介绍与UI一致，见 `real-ai/result.json`。两本机联机账号也由真实provider生成六性格，后续复用真实鉴权token，不替代成fixture。
- 18项实际Unity Match权威规则检查：全部性格/表情/16MBTI、六能力/12上限、墙遮挡、喷火/泡泡、借用/冷却/主人最后一件/借方淘汰/回收/吞噬/普通实体规则与隐私协议。受控平地资料、50Hz，不启用BodyWorld。
- 最终Mac运行20项文字/能力/厚度/持握切换/原稿/编辑提示清理检查；44项介绍交互、308项原R4设置含274中英模型、29项正常衣橱真模型切换/API Save/关闭重开/中英/最后选项。设置与GUI审计都备份并还原用户偏好。最后新增审计字段不改生产介绍/衣橱/设置代码。
- 最终同包两本机真实Unity客户端：409/473个真实15骨physicsFrames、316对同serverTick装备词/能力/color、动作、remaining与cooldown完全一致；双方正式喷火/泡泡、实际双向借用原主数量保持6且冷却expiry差约0，协议4、隐私、主机重连保持GPT/Gemini与10资格。`final-network/20261005-033655/result.json`记录最终binary/hash。真实ToyActor导入sharedMesh/骨绑定、持有厚字网格/材质与重连模型实例通过；此处headless实例检查不是peer截图像素渲染。正常衣橱另有真像素截图。
- 实际独立API进程重启后的GET保持双方资产ID、能力、资料revision、主动外观与资格，见 `final-network/20261005-032747/result.json`；正式配套快照启动与最后基线持久化结果继续写入结构化报告。
- 六角色72正常厚字，六种正式能力实际释放，215帧中208帧存在效果，峰值6并发效果；35预热+180采样。并行Web编译样本均值34.07ms/p95 34.31ms保留于 `six-player-performance-during-web-build.json`；较少负载重复值见 `runtime-audit/six-player-performance.json`。构造Match单Mac渲染，不等于6客户端/手机/公网60FPS。
- Web同源Release编译通过，Safari实际加载并渲染完整菜单，QA指定API8092/权威7792并真实消费API健康请求；网页模板localhost默认发布API8091/权威7791，显式URL参数优先，不依赖原生环境变量。CUA原生坐标反复 `windowNotFoundAtPosition` 且IAB不可用，网页保存/生成/房间连接未验收，不把Web编译称为全功能验收。正式服务 `release_entry /local-release-version` 与启动检查核对源码/树/PID身份。

## 成品截图

- 中文真实介绍与4结果、厚字正面：[介绍](../artifacts/trait-persona-r5/runtime-audit/introduction-words-zh.png)。
- MBTI能力中英：[中文](../artifacts/trait-persona-r5/runtime-audit/mbti-ability-zh.png)、[英文](../artifacts/trait-persona-r5/runtime-audit/mbti-ability-english.png)、[AI可选备注](../artifacts/trait-persona-r5/runtime-audit/ai-note-english.png)。
- [握持](../artifacts/trait-persona-r5/runtime-audit/battle-held-word.png)、[喷火](../artifacts/trait-persona-r5/runtime-audit/battle-flame-word.png)、[透明笑脸泡泡](../artifacts/trait-persona-r5/runtime-audit/battle-bubble-word.png)、[掉落](../artifacts/trait-persona-r5/runtime-audit/battle-dropped-word.png)：构造Match使用真实AI返回资产，非真人网络对战。
- 正常衣橱：[GPT中文](../artifacts/trait-persona-r5/final-skins-ui/wardrobe-gpt-zh.png)、[GPT英文](../artifacts/trait-persona-r5/final-skins-ui/wardrobe-gpt-english.png)、[最后GLM选项](../artifacts/trait-persona-r5/final-skins-ui/wardrobe-last-options-english.png)。真实独立QA资料/API保存流程，不假称用户本人资产。

## 数据与发布

稳定默认入口 `scripts/start-latest.command` → `start-playtest-trait-r5.command`；先逐文件hash、全部已交付功能门槛、API与authority实际PID/路径/服务身份检查，未知8091/7791占用拒绝复用。`--verify-only`不启动，`--check`只启动核验配套服务并清理本次进程。

首次启动使用SQLite在线一致备份将R4正在写的资料库备份到新R5数据目录；后续两份DB独立，不持续跨端口同步。旧库/旧R4进程保留，账号token仍以实际API域保存规则为准；UI草稿保护不是跨端口自动同步承诺。包没有凭据、QA token或数据库；`.env`仅在本机原目录读取。验收使用独立 `/tmp` QA DB，发布运行使用 `artifacts/playtest-trait-r5/profiles.sqlite3`。

当前未验收：真人触屏/微信手机/公网，多人像素截图与Web保存/房间；最复杂72个12字厚词可能约80.93MiB mesh，手机性能须后续验收。字体缓存有界；不声称稳定60FPS。仅清理自身验收进程，不自动打开或打断用户当前R4。
