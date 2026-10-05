# 完整统一整合 R9：20件Persona、动物行走与可调人机

目标完成；默认入口 `scripts/start-latest.command` → `scripts/start-playtest-combined-r9.command`。每玩家Persona上限20件，实体／性格／表情／MBTI共同计数；第21件文字明确失败。再次生成可补回旧12件资料中被挤掉的友好微笑与ENFP，已有assetId保持，不修改原介绍。单词最多12字符的字体约束不变。

## 已整合的明确成品

- 完整R7动物骨骼走动基线 `8ab8525`：猫、狗、兔子、仓鼠真实模型／骨骼动画，277道具、96机制、单次投掷落地规则和协议5。
- 20件Persona生产修改 `9151aec`：服务端统一容量、缓存补回、权威入场、20件中英提示、模型输出4000 tokens。原安全和能力白名单保持。
- 已交付人机成品 `b19744cfe8dd034e62e1714c261af64904ae693a`：1–6个人机，默认／推荐3个，稍慢反应与出手、重玩沿用人数。只导入明确交付的局部代码／测试／新增审计文件及新增语言项，未套用旧R5整树／目录／协议；旧场景差异只有重新生成的内部fileID，保留R7场景。

主责 Persona 专业任务，分支 `codex/persona-limit-20`，工作区 `/Users/jitian/.codex/worktrees/persona-limit-20/About Me`。最终生产整合提交 `3dd89586ac55c3e94b55ca938f77cdce8638fa3a`；原12件缓存升级R8候选保留，未用它覆盖人机成品入口。

## 发布身份和数据

- Mac：`builds/playtest-combined-r9-mac/About Me.app`；Web：`builds/playtest-combined-r9-web/`；API：`builds/playtest-combined-r9-service/`。
- Mac程序集 SHA256 `7070b25a5d4fef51f2b784682db9c5bd8d79156cd271b97ee769d7e721aff24e`；完整源码快照 `99fa6358a9ec6a079ceec989e9bcf9e7e700113291409296a9946a166897376b`。精确文件hash、构建时间、原成品身份及dirty范围以Mac随包 `release-manifest.json` 为准；冻结源码在专业工作区 `artifacts/combined-r9/source-freeze/`。
- 配套API8093／权威7793，保留当前人机版服务地址和登录凭据；协议5，旧人机版协议4须客户端和权威一起换新。启动器校验所有文件hash、功能门槛、实际PID、端口和API发布身份，未知占用拒绝，不复用旧服务。
- 首次启动在独立 `artifacts/playtest-combined-r9/profiles.sqlite3` 备份当前单人人机版SQLite，若其不存在则使用实际R7库／旧R5库；采用SQLite一致备份，原库／当前窗口／进程保留。此后库独立，不声称持续同步。包不含用户数据库、令牌或密钥。
- 当前窗口仍为旧人机版，关闭后由latest启动R9。未覆盖或提交根工作区其他人的未提交源码；根目录仅新增本次成品与记录，并定点更新README、STATUS和默认入口。

## 验收结果和实际范围

- 同一API源码 **471项后端检查通过**：20件/21件边界、缓存补回、ID/revision稳定、持久化、公开／内部资料、安全回归；provider为mock。服务源码与此前R8测试版本同字节，未新增真实AI调用。
- 最终整合 **20项生产Match文字规则通过**：20件接受、21件拒绝、六在线玩家120件、离线玩家+6人机共140件不丢资产／ID唯一，借用／能力／隐私规则。平地规则夹具，非六人实机渲染性能。
- Mac Release构建自动 **31权威规则+17菜单/NPC规则通过**，包括推荐3人机、1–6人数、稍慢反应与出手、寻路与回收；同源Web Release编译通过。客户端成对构建与打包hash一致，继承动物动画身份核对通过。
- 最终R9程序集，独立API数据库，两个真实Unity客户端各20件经本机WebSocket入场，双方权威剩余数实际观察到20；414／479物理帧、文字能力使用12／11次、主机重连、结算及隐私通过。测试资料为明确构造的fixture，未改用户当前资料／偏好／运行进程。证据 `artifacts/combined-r9/network/result.json`。
- 默认入口逐文件验证通过。人机成品原30项实际Mac UI／7角色物理验收、动物成品原26项实际Mac动画验收作为各自来源证据继承；本次未把来源证据称为R9重复实测。Web实际交互、真人手感／手机／微信／公网及最多140件离线物理渲染性能仍待专门验收。

生产功能已交付。测试审核角色复核本摘要与最终成品；后续试玩体验由玩法／客户端角色跟进。本任务没有派子代理。
