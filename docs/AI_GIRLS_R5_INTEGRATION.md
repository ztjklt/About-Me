# AI 娘语义整合（R5 源码交付）

分支 `codex/ai-girls-r5`，基于 R4 完整整合的 R5 厚字源码 `11ff7e8`。不是专项预览包，也不修改默认试玩入口；最终匹配的 Mac/Web/API/权威服务器由整合主责构建和发布。

## 已完成接口

- 普通 + DS、Claude、GPT、Gemini、Grok、Kimi、千问、MiniMax、豆包、GLM，衣橱横滑选择真实外挂模型。固定 AI 套装保留原身体、骨架、动作、物理与手挂点；切回普通恢复保留穿搭。
- `shared/ai-skins.json` 与客户端 Resources 目录同字节。新增可信 `Profile.unlockedCharacterSkins`，保留旧 DS/Claude 布尔资格；API 明确偏好识别、资格撤回、手动选择保存、资料重读、内部可信验证及权威 `Match.Add/View` 使用发布白名单。`AppearanceData.character` 仍 NonSerialized，不能通过外观 JSON 绕过资格。
- 偏好不会自动换肤；新增问答末题「其他 AI 偏好」，每题支持自由填写。旧 52/53、MBTI 54..69、能力 70..75 保留，新 AI 76..83；问题总数 16。
- `AISkinRegistry.All/CanUse/Verified/CharacterForSkin/SkinForCharacter` 是渲染和身份交接接口。ToyWorld 默认预览的旧仅 DS 分支由整合主责用 `SkinForCharacter` 更新。
- 保留协议 4、现有文字装备字段、90 度握持抵消、文字朝向沿角色 forward、配色计入装备缓存、已保存画像与厚字预览相机。

## 来源与验证

仅导入八个 `*Girl-Modular-Delivery.zip` 的 manifest 新文件白名单，126 个新增资源及对应 meta/辅助代码；不套用旧整树和旧源码提交。八个旧四文件补丁按语义合并到最新代码。`artifacts/ai-girls-r5/asset-origins.json` 记录 zip 哈希、分支、交付快照与逐文件哈希，保留八份 manifest 和受限补丁。GPT 确为修订版灰紫眉毛 `#A58CB4`、深度 0.535、宽高 0.20/0.042。

API 32 项通过：十种 AI 正负/假设/第三人称与别名、未解锁拒绝、非法外观字段拒绝、主动选择、SQLite 重读、重开 TestClient、资格独立撤回、公共身份白名单、厚字共存。解锁/保存无需 AI；厚字共存一项仅 provider 提取为 fixture，不是实际模型调用。API 旧 DS/Claude 检查全部保留并通过。

Unity Editor 编译和真实引擎审计通过：11 种渲染身份、7,260 动作帧、220 次连续切换、420 次真正 Match 物理 tick；Standing/Falling/Grounded/GettingUp 均覆盖，最大骨位差 7.73e-7、角差 0、手挂点差 8.62e-7。身体骨和挂点对象身份保留、碰撞数量不变、切回普通无活动外挂/辅助骨残留，所有外挂实际存在且蒙皮网格有限。

这不是真人联机、客户端进程重启、浏览器 WebGL、微信或手机性能验收。现阶段引擎模型截图发现旧主场景 UI 叠影，正隔离审计场景重拍；图片不作为完整造型验收，数值动作/真实物理结果独立有效。最终同源 Release、实际 API/权威/双客户端、设置及介绍 UI 回归由整合主责接手。八套服饰没有自碰撞，极端多人挤压仍需试玩。
