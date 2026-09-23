# Remember About Me 校园地图 V0.4 · 已生成版本

本版由 Tripo 生成的社团立面与手部资产，以及精确搭建的校园结构组成。最终预览是实际三维模型的渲染，不是概念图。

## 本轮外观修正

以用户原始校园图的双层教学楼体量为依据，后排与两翼采用连续的教室开间、粗柱横梁、厚楼板、楼檐、中央楼体、门和成组窗格。教室后墙与门窗退入柱廊后方约 2.7 米，外侧可以看出浅层房间深度；封闭底墙与独立碰撞阻止进入。没有制作可探索的教室内部。

占地 80×64 米，中央争夺区 36×30 米，二层地面高 4.5 米、走廊宽 4.5 米。左右各一座宽 4 米楼梯，二层 U 形走廊连续。音乐、摄影、动漫、篮球在二层；创客、桌游在一层前端。中央空地无树，固定几何树放在外围。建筑使用 UI 的奶油白、薄荷、粉彩和深蓝灰识别框。

## 已写入工程

- `client/Assets/AboutMe/Resources/CampusV04/Campus.prefab`：地图，含几何碰撞。
- 同目录 `ClubBay.prefab`、`HandOpen.prefab`、`HandFist.prefab`：可复用资产。
- `client/Assets/AboutMe/Scenes/CampusV04Preview.unity`：整体场景预览。
- 游戏运行时由 `ToyWorld.Build()` 加载校园；同一份 Collision.json 同时用于服务器高度/阻挡判断和 Unity 碰撞盒。
- 二层跌落：落地清空韧性，只掉落 1 个自身可掉落 Persona，保持落地点；楼梯下降与普通跳跃不触发。
- 地图外坠落：掉落 1 个并返回 Arena。无可掉落 Persona 时不伪造新 Persona，也不删除已落地的生命。
- 大手由天空出现，一层掌拍、二层握拳形态；先给动作预警，在接触时刻按位置范围判定，可以躲开。
- 动态公共道具限定为纸箱和垃圾桶。

## 三维文件

所有文件已在本地目录 `/Users/jitian/Documents/ChatGPT/About Me/artifacts/campus-v04`：

- `Remember-About-Me-Campus-V04.blend`：保留独立教室外壳、社团立面、图标和基础结构，可编辑。
- `Campus.fbx` / `Campus.glb`：完整静态地图。
- `ClubBay.fbx` / `.glb`，`HandOpen.fbx` / `.glb`，`HandFist.fbx` / `.glb`：独立资产。
- `tripo-out/`：未覆盖的原始 Tripo 输出。
- `references/map-final-entrance.png` / `map-final-overview.png`：Blender 实际模型预览。
- `unity-preview.png`：团结引擎实际渲染。
- `manifest.json`：来源、任务及费用记录。

地图 46,964 三角面 / 14 种纯色材质（CampusFlat 简单受光着色，无 PBR 贴图）；独立立面 5,837 面；开掌 3,900 面；握拳 3,899 面。手模型原始输出超过请求的 4,000 面目标，已本地简化，未调用付费减面。

## 验证及边界

团结引擎编译和 21 项规则测试通过，覆盖双侧楼梯所用的相同结构、二层连通高度、落地不传送/只掉一个、楼梯下降不掉落、普通跳跃、不可进入教室、二层护栏和缺口、大手接触时机与躲避，以及原有 Recover/Eat、争夺、淘汰规则。

当前是可导入、接入玩法的第一版。握拳由同一手模型本地弯曲得到，是独立姿态模型，不是完整骨骼动画；未进行移动端真机帧率或完整多人长局测试。空间和美术仍可继续迭代。

## 本轮 Tripo 实际费用

| 任务 | task ID | 积分 |
| --- | --- | ---: |
| 入口参考图（未采用） | `ab17cf13-fa46-4f2c-b759-5dc6504778bf` | 5 |
| 俯视参考图（仅参考构图） | `3cab3cee-550c-4181-b10d-1aa006cc161b` | 5 |
| 社团立面参考图 | `b8d9289b-3c84-4766-9264-156bb79448d6` | 5 |
| 手部参考图 | `e0b29b86-ff75-48d8-a6f0-272624b92e60` | 5 |
| 立面3D模型 | `9eef5f06-e5b2-4242-8e5b-d25009017923` | 40 |
| 手部3D模型 | `dc0b0285-f0af-483f-82ce-8d34b638338b` | 40 |

合计 **100 积分**，账户余额 **24,860**，冻结 **0**。另一次入口图任务失败退款 0 消耗，一次参数校验未创建任务。未采用入口图的 5 积分包含在这 100 中。此前旧整图尝试的 40 积分不计入本轮。

## 重建

本地几何更新不消耗 Tripo 积分：依次运行 `scripts/campus-v04-build.py`、`scripts/campus-v04-assemble.py`（使用 Blender Python），再执行编辑器菜单 **About Me → Import Tripo campus V0.4**。此步骤会重新导出当前资产并运行规则测试。编辑器菜单 **Preview Tripo campus V0.4** 可重建预览场景与截图。
