# 完整 R10：统一 AI 选择题与喷火增强

目标完成。默认 `scripts/start-latest.command` → `scripts/start-playtest-persona-r10.command`。本次以完整 R9 继承，保留20件Persona、四动物骨骼行走、277道具／96机制、协议5、1–6人机／默认3个以及此前设置、衣橱和中英文功能。

## 可观察变化

- 所有16种MBTI均有对应文字Persona和默认能力；共用6种已实现机制。INTP默认好奇牵引，牵引地面道具；每件文字道具可在已有六种能力中手动改选。当前性格词目录18个，问卷提供6个常见词及自填，并非8个独立能力。
- 热情喷火每次命中由12提高到20稳定度，完整两次由24提高到40（增加约67%）；两次判定、只推一次、10秒冷却、4米范围、施放时减速及墙体遮挡／侧绕／起身保护保持。稳定度不是剩余Persona数量；耗尽时仍按原规则掉落，不直接扣20件。
- AI统一成一道“你喜欢什么 AI？”，可多选DeepSeek、Claude、GPT、Gemini、Grok、Kimi、千问、MiniMax、豆包、GLM及自填。提示“回答后可能解锁相应的彩蛋模型，可在衣橱切换。”真实流程仍需将草稿追加并保存介绍识别资格，再在衣橱主动选择模型。
- 合并题目后，MBTI和能力选择改为按题目计算选项位置，避免INTP误触旧能力区；16道题，保留44个兴趣／动物视觉选项。

## 身份与交付

主责Persona任务，喷火按玩法流程验证；专业工作区 `/Users/jitian/.codex/worktrees/persona-limit-20/About Me`，分支 `codex/persona-limit-20`。功能提交 `18141d0`；包含latest入口的冻结交付提交 `d7667c7de965a9098d9ef9a9a13fd82f1020b13b`。

- Mac `builds/playtest-persona-r10-mac/About Me.app`；Web `builds/playtest-persona-r10-web/`；配套API `builds/playtest-persona-r10-service/`。
- Mac程序集SHA256 `89c4cce52b819a09286cb237ecdcceaf890b335dc5b80771191a6a8a9f1c5fb2`；完整源码快照 `029bab8dd7cad23ae1797f26128cc0c924fa3d8a00941ba51d1dba6dc18a4ea9`。构建时间、文件清单、继承的R9身份与dirty范围见随包 `release-manifest.json`；冻结源在专业工作区 `artifacts/persona-r10/source-freeze/`。
- API8093／权威7793，启动核对全部hash、功能门槛、PID、端口及发布身份；未知占用拒绝。首次以SQLite一致备份导入现有R9库，若不存在则使用当前人机R6／动物R7／旧R5库；此后新库独立。包不含数据库、令牌和密钥。
- 根工作区只新增成品、入口和记录并定点修改README／STATUS；未覆盖或提交其他角色源码。当前旧窗口与服务未停止；关闭原窗口后由latest重新启动R10。

## 验收与实际覆盖

- 471后端检查通过，provider为mock，无新增真实AI调用。
- 20项生产Match文字规则通过，覆盖16个MBTI完整映射、20件接受／21件拒绝及文字能力；17项场地权威规则通过，包含喷火两次伤害、仅一次推力、侧面不命中、墙体阻挡、保护与单次掉落。
- Mac Release构建自动31基础权威＋17菜单/NPC检查通过；同一运行源码Web Release编译通过，成对源码hash核对。
- 独立QA存储域的实际Mac Release界面20项检查通过：10品牌真实按钮、统一句子、INTP与能力位置、衣橱预览不修改资料、中英文完整。截图 `artifacts/persona-r10/ui/ai-question-zh.png` 与 `ai-question-en.png`；这是隔离UI构建证据，不称生产包再次UI实测。
- 最终生产程序集两个实际Mac客户端、独立API库和本机权威验证通过：各20件入场／观察剩余上限20、414/480物理帧、能力使用各12次、隐私／主机重连／结算；明确fixture账号与道具，没有修改用户资料或当前进程。结果 `artifacts/persona-r10/network/result.json`。
- 默认入口逐文件核验通过。未新增真人平衡手感、Web交互、手机／微信／公网及最大离线140件渲染验收；喷火数值增强不能据自动检查宣称完成平衡。

生产修改及本机构建交付完成。产品负责人／测试角色接收上述成品与结果；真人手感和进一步平衡由玩法角色跟进。
