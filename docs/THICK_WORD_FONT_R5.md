# R5 真实字体厚字交付

本交付只实现字体轮廓、实体网格、缓存及独立引擎验收。基线 `78208762`，分支 `codex/trait-text-mesh-r5`。未修改现有 ToyFactory、ToyWorld、GameModel、GameUI 或玩法能力；未构建新的试玩 Release，未停止用户运行进程。调用、字段、正式能力和联机整合由主责 agent 接手。

## 接口与实物

`ThickWordVisual.Create(string text, string abilityId, Transform parent, string color)` 返回实体 GameObject。失败返回 null 并输出明确原因；接入端宜先使用 `TryValidate(text, out reason)`，或使用 `TryCreate(..., out model, out reason)` 展示失败卡，禁止替换成机器人或假成功。

来源是已有 `Resources/AboutMeFont.otf` 的真实 CFF 字形轮廓，SHA-256 `2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b`。离线使用非零绕数填充、带洞三角化；运行时挤出正面、反面及所有外沿/孔洞侧壁。模型没有漂浮 TextMesh、字图纹理平面或网络字体。B 保留两个孔洞，中文内孔保留。

前面朝 ToyActor 本地 +Z；实际阅读方向已用引擎截图确认。XY 居中并归一到最多 1.25 × 0.65，厚度固定 0.16，不随长词缩薄。每实例一个 MeshRenderer/一个共享材质；Grip 为中心子节点。`ThickWordMetadata` 保存 Text/AbilityId；`PersonaVisualMeta` 保存 visualId、来源、许可、碰撞尺寸和吞噬缩放。

BoxCollider 使用实体紧包围边界，不是字形孔洞级碰撞；默认 `isTrigger=true`，避免持握字块增加本地刚体推力导致人物腿部/布娃娃回归。没有新增 Rigidbody，位置继续服从现有权威逻辑。掉落需要实体本地碰撞时由接入端按状态显式配置。命中/能力效果仍使用既有权威规则。

## 字符、资源与缓存

- 最多 12 个 Unicode 码点，正确处理补充平面代理对。16 个 MBTI、16 个当前性格、6 个当前表情全部通过。
- `Resources/ThickWords/supported-codepoints.json` 是 API 与客户端共享字符表：44,802 映射字符，含 ASCII 空格作为字体 advance；44,801 个可见字体字符映射到 44,295 独立实体字形。空白串、其它不可见字符、缺字 emoji 和超长文本明确失败；缺字原因包含 U+ 码点。
- 自填文字按可见码点布局；没有引入 OpenType 复杂脚本 shaping 或心理测评推断。API 应继续执行自己的正常化与内容校验。
- 压缩索引/轮廓合计 32,016,973 字节（30.53 MiB），119 个不超过 256 KiB 的本地页。不会整体加载轮廓包。
- 缓存上限：256 glyph、8 resource 页、96 word mesh；24 MiB 是空闲组合网格的回收阈值。活动实例持有共享 mesh 引用，不能为凑预算回收正在显示的网格；活动词数量超过约定上限或特别复杂长词可超过该内存阈值。
- 所有字形最大轮廓顶点 494；12 个最复杂字最多 35,568 个网格顶点，低于 16 位索引限额。Mesh 只在新词出现时构造，效果实例可复用同一网格，逐帧无轮廓计算。

## 验收证据与预算

`artifacts/thick-word-r5/font-validation.json` 验证完整 44,295 字形载荷、索引/分页哈希、所有要求文字，以及 96,937 个清晰区域字体非零绕数/三角网格掩码比对采样；B 两个孔洞通过。所有离线导出字形的三角化面积检验通过，无轮廓三角化失败。

`artifacts/thick-word-r5/engine/engine-validation.json` 来自 macOS Apple M5、团结 `2022.3.62t11`，实际 Camera.Render 隔离场景。38 个要求文字真实网格、正背面/侧壁、代理对、缺字失败、实物边界射线、6 个真实 ToyActor 手部挂点、共享 mesh、310 个不同自填词和超过 256 不同 glyph 的缓存压力均通过。活动网格未被回收，所有引用销毁后能清空缓存。

正常 6 角色/72 件模型场景：68,940 渲染顶点、44,724 三角；40 个共享网格估算 1,314,732 字节，8 页托管 payload 2,095,144 字节。页 TextAsset 的引擎原生副本及索引/字形对象另占内存。38 词冷构造约 62 ms；复用 72 实例约 2.50 ms。网格预算按 vertex 32 bytes + uint16 index 估算，不冒充 Profiler 实测总内存。Camera.Render CPU 提交均值约 0.25 ms，p95 0.31 ms；GPU 异步提交、游戏逻辑及帧节奏未计入，不能换算为游戏帧率。

另有对抗测试：72 个不同的 12 码点词，每词 11 个全字体最复杂罕见汉字，再加不同末字。实测合计 2,366,964 顶点、1,520,516 三角，网格估算 84,865,944 字节（80.93 MiB），冷构造约 505 ms。完整字库和任意合法 12 字文本的承诺带来这个明确上限风险。正常目录词通过不等于任意极端长词的微信/手机性能通过；真机预算、必要的 LOD/分批预热和正式多人能力需整合方验收。

截图：`all-38-thick-words.png`、`all-38-front.png`、`all-38-back.png`、`counters-and-side-walls.png`、`six-actors-72-words.png`。均为本次真实引擎网格渲染，不是概念图。6 角色是离线渲染 fixture；没有声称真实多人对局、手机触控或本轮 API 实际生成通过。

## 复现入口

离线导出依赖 `scripts/thick-word-font-requirements.txt`。使用 `scripts/export-thick-word-glyphs.py` 从现有字体导出；`scripts/verify-thick-word-glyphs.py` 检查全部载荷和字体掩码；Unity Editor 入口为 `AboutMe.Editor.ThickWordAudit.Run`，环境变量 `ABOUTME_THICK_WORD_AUDIT_OUTPUT` 指定证据目录。无需构建试玩包或连接服务器。

保留字体原始嵌入版权说明 `Resources/ThickWords/FONT-NOTICE.txt`，并附完整 [Noto CJK 官方 OFL 1.1](https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/LICENSE) 的 `OFL.txt`。轮廓衍生资源继续随 OFL 许可分发。
