# 首批 28 件原创模型

从 320 件候选中制作并接入第一批 28 件，另加前一批的游泳圈，共新增 29 件。实际目录从原先 60 件增加到 89 件。28 件采用明确编写的几何部件组合，单网格、单持久化调色板材质、最多 1500 三角面，均有 Grip、SwallowCenter、碰撞尺寸和 160×160 缩略图。

本批包含泳镜、浮板、脚蹼、呼吸管面镜、泳棒、排球、拳击手套、跳绳、瑜伽垫、攀岩扣、调色盘、油画笔、毛笔、显微镜、磁铁、法槌、灭火器、雨伞、肥皂、寿司、甜筒、爆米花、细嘴壶、蘑菇、登山杖、无人机、礼炮、云朵罐。

模型制作源码在 `OriginalPersonaModels.cs`，配方在 `shared/persona-original-models.json`。使用项目原创几何；没有下载或声称使用第三方游戏模型。预览在 `artifacts/persona-original-models/priority-28-review.png`。

本阶段实际执行 `stage_0_template` 的既有行为。候选机制中的礼炮爆炸、脚蹼冲刺、跳绳绊人等尚未实现，不能因为对应模型已上线就把它们计作完成。320 候选还剩 292 个模型待制作，100 项道具/环境新机制仍需逐项接入与验收。

## 验证

- 后端 120 项测试通过：本批 28 项分别验证肯定事实允许、否定事实拒绝；目录客户端副本完全一致、配方与 id 唯一、原创来源标记明确。
- Unity 检查 28 件几何指纹不同，网格预算、材质持久化、握点与吞噬锚正常；Mac / Web 构建通过。
- 真实 DeepSeek：输入显微镜、油画和跳绳兴趣，约 1.51 秒生成三个相应新模型；该次结果在 `artifacts/persona-original-models/real-deepseek-models.json`，不是通用速度承诺。
- Web 界面真实填写、生成三个绿色词条、保存并进入单人游戏，三件道具均在 NPC 对局里掉落并被吞噬，结算正确列出名称。截图在 `generated-three.png` 和 `three-swallowed.png`。
- 本批未做真人平衡、触屏设备和公网验收；不可宣称 100 种机制已经完成或趣味性得到证明。

构建入口：`AboutMe.Editor.OriginalPersonaModelTools.BuildDeliverables`。启动器验证 `personaCatalogCount >= 89`，避免复用旧素材服务。
