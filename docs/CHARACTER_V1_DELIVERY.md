# 圆头校园玩偶 V1 · 工程交付

实现于团结 2022.3.62t11（1.9.3），使用现有 UI 的奶油白、薄荷绿、粉色、淡紫、黄色与深蓝灰。角色是约 2.5 头身的统一体型，头脸、头发、衣领、袖口、简化拇指和厚鞋底具有独立轮廓。外观不提供数值属性，不改变服务器的移动、抓取、攻击或坠落判定。

## 可以直接查看的内容

- `artifacts/characters-v1/character-turnarounds.png`：三个示范角色，各自正、侧、背三视图。
- `artifacts/characters-v1/campus-character-scale.png`：六个角色在校园中的比例。
- `artifacts/characters-v1/wardrobe.png`：实际客户端中的衣橱。
- `artifacts/characters-v1/character-motion.mp4`：团结渲染的连续动作检查，走路、抓握、夸张吞噬、吞咽、布娃娃与恢复。
- `artifacts/characters-v1/exaggerated-eat.gif`：吞噬动作片段。
- `artifacts/characters-v1/giant-mouth-side-check.png`：单个角色的侧面送入口中检查。
- `artifacts/characters-v1/About Me Characters.app`：Mac 开发测试版。
- `client/Assets/AboutMe/Scenes/CharacterV1Preview.unity`：校园比例预览场景。
- `client/Assets/AboutMe/Art/CharactersV1/Demo0.prefab`、`Demo1.prefab`、`Demo2.prefab`：已保存蒙皮、骨架、材质和形变的示范预制体。实际对局由 `ToyActor` 按保存的外观配置组装并驱动。

## 骨架与动作

共用 10 根物理骨骼：躯干、头、左右上臂、左右前臂、左右大腿、左右小腿。肩窝位于躯干内侧，肘部与膝部可以单独弯曲。衣袖使用从躯干过渡到上臂、前臂的连续权重，避免原来整条手臂像挂在身体侧面的情况。其余衣物、头发和配件绑定同一套骨架，各搭配的骨骼及基础碰撞体相同。

主体合并成一个使用顶点色的 SkinnedMeshRenderer。三个示范角色总几何量分别约为 1.27 万、1.18 万、1.32 万三角面（含独立嘴部与眉毛）。嘴沿、口腔、舌头、牙齿和眉毛保留为独立的表情部件。使用纯色哑光和简单明暗，不生成写实贴图或 PBR 材质。三种形变分别用于大嘴/面部、吞咽鼓肚、送入口中时伸长手臂。

吞噬以服务器 `interaction` 和 `progress` 驱动，默认耗时仍为原有的 2.5 秒：仰头约 50°、嘴张至约 1.25 米宽和 1.06 米高、伸长双臂、Persona 向嘴部移动并缩小、吞咽后收手。手掌和前臂走脸的外侧，手留在嘴沿外，避免随物件穿入脸或嘴沿。手臂拉长只用于画面，服务器不因此扩大攻击或抓取距离。服务器的已吞噬记录触发鼓肚收尾；被打断不会误播成功收尾，物件恢复到地面状态。

动作采用共用的程序化骨骼驱动与形变，不需要每次调用模型生成服务。走路相位由实际显示移动距离推进；动作权重、朝向、起身采用时间相关的平滑过渡。远端位置使用 75 毫秒快照缓冲，丢包外推限制为 45 毫秒。该缓冲会引入少量显示延迟；目前没有额外加入本地预测与服务器回滚。

## 外观、保存与 AI

统一目录保存在 `shared/appearance-catalog.json`，客户端 Resources 中保留相同副本。

| 部位 | 已实现选项 |
|---|---|
| 发型 | 短碎发、卷发、齐耳短发、扎发 |
| 上衣 | 连帽卫衣、棒球外套、宽松衬衫、运动上衣 |
| 下装 | 直筒长裤、短裤、裙装 |
| 鞋 | 板鞋、运动鞋、厚底短靴 |
| 头部配件 | 无、棒球帽、耳机 |
| 包 | 无、双肩包、斜挎包 |
| 颜色 | 独立肤色、发色、上衣、下装、鞋、配件色 |

“我的衣橱”支持正、侧、背预览、逐项修改、根据介绍 AI 推荐、保存、撤销草稿、重新载入。最多一个头部配件和一个包。兴趣仅影响可修改的推荐，AI 不推断肤色、种族、性别或体型，保留用户已选肤色和发色。

- `GET /appearance/catalog` 获取目录。
- `POST /appearance/recommend` 调用已配置的真实 LLM，只返回目录内的搭配，不自动保存。没有配置或调用失败时明确返回错误，仍可手动搭配。
- `POST /profiles/me/appearance` 接收 `appearance` 和 `expectedRevision`，使用并发版本检查保存外观。
- 外观版本 `appearanceRevision` 与 Persona 版本 `revision` 独立。换装保留 Persona、资产 ID、自我介绍与 Persona 版本。重新生成 Persona 保留穿搭。
- 旧档案自动获得确定的默认搭配。真实联机角色从认证接口取得外观，忽略客户端自行提供的外观字段；样例模式才接受本地测试外观。
- 战斗重连保留本局外观；大厅可以载入刚保存的穿搭。客户端和服务端需要一起更新，加入消息新增了样例外观字段。

## Tripo 使用记录

只对头部基础体、耳机和斜挎包进行生成；脸部表情、服装库、拓扑整理、绑定、动作和客户端集成由本地工程完成。参考图由统一几何设定渲染，原图、干运行参数、任务结果、源模型与预览保留在 `artifacts/characters-v1`。

模型均为 P1-20260311，`texture=false`、`pbr=false`，每个资产仅生成一次，没有付费重试。

| 资产 | 实际三角面 | 任务 ID | 积分 |
|---|---:|---|---:|
| 头部基础体 | 2327 | e361f37c-7e56-4681-9598-75e2bc5337f8 | 40 |
| 耳机 | 1528 | 54f799b7-b949-416c-932c-70f3d040bd3d | 40 |
| 斜挎包 | 1554 | 6a4d3893-0089-46aa-8ae7-be003d7df93a | 40 |

合计 120 积分；完成后余额 24740，冻结 0。之后的肩膀、大嘴、仰头、伸手和防穿插修改均为本地修改，没有再次调用付费生成。

## 验证与复现

后端 17 项测试通过，覆盖外观目录、保存、旧档案迁移、并发冲突、重连读取、保留 Persona、重新生成 Persona 保留外观、真实供应商协议模拟和未配置 AI 的错误处理。AI 协议测试使用 mock，不等于线上供应商实测。

团结既有 22 项权威玩法规则测试通过；新增检查覆盖目录选项、统一骨架、蒙皮权重、三个形变、Persona 掉落不改衣服、重连/快照、快照插值/角度跨界/丢包外推，以及不同渲染步长下的大嘴动作和手臂安全间距。30 / 60 / 120 FPS 步长的吞噬与收手测试中，最小保守间距分别为 5.37 / 5.23 / 5.18 厘米。手臂检查使用保守的前臂/手掌包络与嘴沿、头部距离，并辅以正、侧面渲染；并非对全部组合进行精确网格碰撞证明。

菜单入口：`About Me / Characters`。可通过命令行执行 `AboutMe.Editor.CharacterTools.Review` 重建预制体和检查图，执行 `AboutMe.Editor.CharacterTools.BuildPlayer` 构建 Mac 客户端。`scripts/character-assets.py` 仅整理已下载模型；`scripts/character-tripo.py` 会调用付费服务，已存在任务结果时拒绝覆盖。

本机 Apple M5、1600×900、开发构建、六角色校园动画场景，预热后 832 帧：平均 59.4 FPS，P95 17.57 毫秒，记录到的最大单帧 GC 分配 740 字节。截图录制不计入这次帧时间测试。物理关节最大分离约 3.3 厘米。运行数据写入 `artifacts/characters-v1/runtime-motion-metrics.json`；本地六个独立客户端的 WebSocket 测试通过：6 套不同配色均同步到所有客户端，吞噬 40 个 Persona 后完整结束对局，所有客户端的外观校验通过。输出在 `artifacts/network-6`。这些是桌面验证，不代表手机或微信平台的帧率保证。移动端真机、弱网长时间测试及服装组合的完整人工美术验收仍需在目标设备继续进行。

独立测试端口可用 `ABOUTME_GAME_PORT` 配置；默认仍为 7777。网络复现：`python3 scripts/test-network.py --players 6 --port 7789 --binary "artifacts/characters-v1/About Me Characters.app/Contents/MacOS/About Me"`。录制复现：运行该客户端并传入 `--character-motion-probe --probe-capture --probe-output <绝对输出目录>`；测帧率时省略 `--probe-capture`。
