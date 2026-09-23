# V3 动作目标采样与受力表现

本轮新增 `client/Assets/AboutMe/Runtime/DollCombatMotion.cs`，只负责连续目标姿势和受击回弹。它没有创建动态骨架、抓取关节、命中判定或网络同步；这些应由调用方的角色控制层和服务器实现。

## 接口

每个角色持有一个 `DollCombatMotion`，每个显示帧调用 `Sample(Input)`；传送、换局或重置角色时调用 `Reset()`。

- `dt/time`：显示时间步与单调时钟。
- `localVelocity`：角色朝向局部坐标的平面速度。+Z 为角色前方，+X 为右方。调用方在进食、失衡等状态应传经过许可的实际移动速度。
- `runBlend`：走跑混合权重，0～1；不负责决定实际速度。
- `actionKind/actionElapsed/actionVariant/actionId`：权威动作类型、已用秒数、左右变体及动作序号。变体奇数使用左手，偶数使用右手。`actionElapsed < 0` 时才使用本地后备时钟。
- `impactId/localImpact`：递增受击序号和角色局部冲量。每个新序号只触发一次；约 6m/s 视为较强冲击。0 表示没有事件。
- `grabTension/escapeProgress`：归一化拉扯强度及挣脱进度。
- `airborne/holdingGrab`：离地及施抓者仍在保持连接。被抓者不能把 `holdingGrab` 设为 true。

`Pose` 中骨骼 Euler 值都是**相对 rest 的完整目标角度**，不能与旧行走角度重复叠加。`bodyOffset` 为 rest 的表现偏移，总长度不超过 0.12m。`leftFootOffset/rightFootOffset` 是可选脚 IK 目标，不能再叠加到已经校正好的落脚点。

推荐应用顺序：目标采样 → 有限驱动力/显示平滑 → 手、包和脸部避让 → 地面/落脚校正 → 吞噬专用大嘴与手臂 IK。严禁将这些表现偏移写回服务器 root 位置。

## 已实现动作

| 类型 | 目标时长/命中时刻 | 表现 |
|---|---|---|
| light | 0.45s / 0.10s | 单侧预备、快速伸肘、躯干扭转、收回 |
| heavy | 0.65s / 0.16s | 更大的肩部蓄势、前倾挥击、较长恢复 |
| charge | 持续至状态改变 | 肩臂逐步蓄力，身体降低重心 |
| grab / grab_item | 0.45s / 0.20s | 双手伸出，连接失败后收回 |
| hold | 持续至状态改变 | 维持伸手并随张力后倾 |
| release / escape | 0.25s | 放手收回 / 短促扭肩挣脱 |
| throw | 0.65s | 双臂预备和向前抛出 |
| pickup | 0.55s | 俯身、屈膝和双手接近物件 |
| recover | 持续至状态改变 | 保持较低姿态与双手操作 |
| equip | 0.30s | 单手取出物件 |
| eat | 持续至状态改变 | 仅辅助躯干与 feedingWeight，嘴和手保持现有防穿模 IK |
| hurt / jump / fall | 0.42 / 0.35 / 最长1.10s | 受击防御姿态、腾空收腿、软倒蜷腿后恢复 |

命中时刻是姿态设计对齐点，采样器不发送命中事件。服务器仍决定伤害、连接与释放。

## 连续性与约束

- 走跑共用连续相位；前后方向改变矢状面摆腿方向，侧移增加侧向髋摆动，不改变角色根朝向。
- 速度、走跑、动作目标分别进行指数平滑，动作切换不清空步态相位。
- 受击使用零目标解析阻尼振子，按事件序号只施加一次冲量；不把上一帧回弹叠回动作目标，避免累计漂移。
- 身体偏移、受击角速度、骨骼目标角度均限幅。
- 采样不生成 GameObject、不改变关节锚点、不分配每帧列表。

## 验收边界

本类可以独立编译并验证有限输出、时长、帧率差异、冲击去重及限位。它**不能独立证明没有穿模、脚不滑或主动布娃娃稳定**：这些必须在接入最终骨架、衣服、物理驱动与相机后录像检查。运行骨架现已增加至22个变形骨，具有独立骨盆、脊柱、胸、颈、双锁骨、手腕、脚踝和脚趾；物理代理仍10个且仅运动学。额外足部目标留给后续地形 IK 使用。重击的较高抬臂尤其需要检查大头、耳机和包的最终净距。

## 有限力矩的数值关节层

另新增 `DollJointDynamics.cs`。它是受控的**表现层数值关节模型**，不是 PhysX `ConfigurableJoint`，也没有双向推动服务器角色或道具。

```csharp
var joints = new DollJointDynamics(10);
Quaternion displayed = joints.Sample(
    index, targetLocalRotation, deltaTime, parentLocalTorque, fastAction);
```

默认索引对应现有创建顺序：胸、头、左肩、右肩、左肘、右肘、左髋、右髋、左膝、右膝。可用 `SetProfile(index, Profile)` 改映射；`Reset(index, currentLocalRotation)` 用于生成、传送或布娃娃恢复。第一次 Sample 初始化到当前 target，若需从静止姿态开始驱动，应先 Reset 到 rest。

- 每关节独立保存四元数和角速度，使用最短弧旋转误差计算弹簧力矩。
- 具有不同的惯性、刚度、阻尼、最大力矩、最大角速度及角度范围。
- 最长以 1/120s 子步积分，单次显示时间步上限 0.1s，有限力矩产生有限角加速度。
- `externalTorque` 是父局部坐标中的**持续力矩**，适合抓取张力；不是速度冲量。撤去力矩后，关节会回弹到目标姿势。
- 肩/肘 `fastAction=true` 增加刚度和力矩上限，最大角速度为1200°/s；仍保留有限加速度。胸、头、髋、膝不会因这个标志变得同样坚硬。
- `GetAngularVelocity(index)` 输出 °/s。内部旋转向量限位是可控的近似关节范围，不等同于完整解剖关节或物理引擎 swing/twist 限位。
- 不要再对同一骨骼叠加旧的强 Slerp 平滑，否则会叠加延迟。手包净距约束和吞噬 IK 应在关节驱动后执行。

## 本轮独立测试结果

使用团结2022.3自带 Mono/C# 编译器及 `UnityEngine.CoreModule.dll` 独立编译并运行，未启动主工程构建：

- 16类动作 × 30/60/120fps，目标角度和0.12m表现偏移保持有限。
- 同一受击ID重复出现不会反复注入力；连续新受击和无效浮点输入不会使状态失控。
- 侧移产生侧向髋部摆动，不会把整个人转向侧方。
- 快速肩部从0°追踪-90°阶跃目标，在100ms达到 **-86.38°**；普通配置为 **-62.42°**。
- 数值关节30/120fps结果一致；持续力矩能偏转胸部，撤力后回到目标。
- 10个关节的交替极端目标、大力矩、初始角加速度和最大角速度检查通过。

这些是独立数值检查，不能代替接入后的动作视频、服装穿模、联机一致性或移动端帧率验收。

## 最终重击与倒地目标

重击使用肩后外侧蓄势，主肩从约+48°摆到-72°，外展约28°；胸腰从-23°扭转至+23°，屈膝后蹬伸，副臂反向平衡。命中仍对齐0.16s，身体前冲表现约0.085m且最终 `bodyOffset` 保持0.12m上限。

倒地增加 `Input.staggerRemaining`，以及已经加权的 `Pose.fallWeight/fallOffset/fallEuler`。约0.13s内进入坐倒，髋折叠约-42°/-38°、膝弯120°/116°，独立下沉0.38m和38°后仰/12°侧倾；有权威失衡剩余时间时，在最后0.30s恢复，否则本地动作最长1.10s。调用方应使用不改变22根变形骨数量的表现父节点应用这些偏移，并继续检查脚底及头部地面净距。它是受控软倒动作，不是完整物理布娃娃。

## 已导入的真实 Tripo 动作

使用 native `spec=tripo` 骨架 `d4d47422-ff77-416e-a003-413a44f2db11`。此前 mixamo rig 的 retarget 被 Tripo 拒绝；失败文件保留，不被当作动作来源。

| 动作 | 原始长度 / 30Hz帧数 | 动作任务ID |
|---|---|---|
| idle | 15.333s / 461 | `0f202c70-b984-4557-9a7f-da1d5294d384` |
| walk | 2.333s / 71 | `d88a73f8-52e7-4713-8d0a-b9f5b530ebd1` |
| run | 1.267s / 39 | `ada93506-dbc9-4117-a878-ee11fbf12df1` |
| box_01 | 2.233s / 68 | `1bab40dc-b3db-47a1-a6ae-c20b1daa2541` |
| hurt | 13.833s / 416 | `454659b6-90d3-4e43-9c42-e17474df1492` |

离线工具：`artifacts/characters-v3/extract-tripo-motion.py`。它只读取已成功的 FBX，不调用 API、不生成模型、不伪造缺失动作。输出为 `Resources/CampusDoll/Motion/*.json`，保留源任务ID、文件哈希、FBX动作名、骨名和重定向方法。

真实源骨名是 Hip/Pelvis/Waist/Spine01/Spine02、NeckTwist01/02 及左右 Clavicle/Upperarm/Forearm/Hand/Thigh/Calf/Foot/ToeBase。映射到22根玩具骨，使用源全局 `pose * inverse(rest)`，经 `(-BlenderY, BlenderZ, BlenderX)` 基变换，再作用到玩具 T-pose 并转为父局部旋转。所有五个动作与批准 native rig 的 bind 朝向差为0°。源根位移不导入，游戏仍由权威 motor 控制位置。

- `box_01` 是**左手单拳**。按实际肘伸展选择源0.30–1.10s，源0.60s对应本地命中；右手变体应镜像。
- `hurt` 是较长的受伤状态动作。采用源0–1.1667s的首次反应，源0.50s对应冲击节点；本地惯性与混合承担恢复。
- `run` 原始端点存在约22.28°最大差。选择源0.40–1.0333s步态循环，加2帧重叠混合后播放周期约0.566667s；原始四元数样本仍保留，未假称源循环本身无接缝。

运行时 `DollTripoMotion` 缓存资源和四元数数组；`TrySample/Duration/MapActionTime/SourceStrikeSide/SourceTaskId` 可直接调用。`MapActionTime` 用选定单次动作窗对齐服务器命中时刻。缺失或非法资源返回 false，不能标为 Tripo 已成功播放。真正的角色混合和使用断言由主工程验收入口负责。

## 已接入校园玩偶

CampusDoll 按骨盆、脊柱、胸、颈、锁骨、手脚分配22个驱动器，先采样动作目标，再施加有限关节驱动，最后计算避让和接触 IK。旧的自由布娃娃切换已关闭，代理 Rigidbody 始终运动学且不参与碰撞；没有放在世界场景中、销毁角色后会遗留的动态容器。

这是刻意选择的数值表现关节，不是 PhysX ConfigurableJoint 主动布娃娃：现有根移动由服务器motor控制，直接让其父子层级骨骼成为动态体会与Transform和抓取接触写入竞争。当前关节积分能表现受力偏转和恢复，但不能产生全身悬挂或把可见手骨碰撞反馈成真实服务器反力。服务器 SoftCombat 单独计算双向拉扯的位移。

衣服躯干部位重新分权到骨盆、脊柱和胸，手掌绑定独立手骨，鞋绑定脚踝、鞋头绑定脚趾。表情和穿搭配置保持原有保存逻辑。RightHandAttachment/LeftHandAttachment 跟随最终手骨；装备推荐局部scale .60、position(0,0,.12)。

SetGrabContact(worldAnchor) 在所有角色插值显示后应用，最多增加.11m臂长；只改变表现，不更改根或服务器连接。GrabContactError暴露左右手分别到各自接触目标的最大误差，不能用两手平均掩盖单手偏移。吞噬的巨嘴、仰头和伸臂 IK 最后计算；不能用目标骨姿势检查结果代替最终蒙皮录像。

主工程验证入口：AboutMe.Editor.LocomotionTools.CombatValidate；渲染入口：AboutMe.Editor.LocomotionTools.CombatReview。准备了10种动作、各3个时间点的正侧双视图联系表，以及600张1280×720双视图帧，并扩充runtime probe轻击/抓取截图。入口准备完成不等于全部通过；以主任务实际验证日志为准。

## Tripo 动作来源与实际使用

真实生成完成：idle、walk、run、box_01、hurt。每条经 Blender 从 FBX 采样30Hz，将源全局姿态相对核验rest的旋转差转换到玩偶22骨的parent-local旋转；不使用源根平移、不替换玩家衣服或头脸。运行时DollTripoMotion缓存Resources中的JSON，CampusDoll在有限关节驱动之前混合其目标旋转。待机主要使用Tripo，正向走跑混合Tripo与本地目标，侧移/后退坚持本地独立方向步态。轻击使用真实box_01的单拳片段与本地夸张动作混合，hurt使用首个受伤片段；大嘴、接触、跳跃、失衡、重击、挣脱等由本地动作/IK补齐。

首次原校园玩偶自动rig存在错误手腕位置，未交付为可用骨架。纠正为无遮挡、连续表面、正scale、法线朝外、+X朝前的motion donor后，Mixamo rig形状正确，但服务端明确拒绝Mixamo retarget（1004）。改原生tripo spec后五条动作成功。绑定任务为v1.0-20240301，动画为对应preset:biped:*，全部animate_in_place=true。

计费：原rig c007f914-012f-4569-b6c3-47c60a60d9ae 25；纠正Mixamo rig 634c9c57-dd66-44e0-b7a0-631fb0972162 25；原生rig d4d47422-ff77-416e-a003-413a44f2db11 25；idle 0f202c70-b984-4557-9a7f-da1d5294d384 10；walk d88a73f8-52e7-4713-8d0a-b9f5b530ebd1 10；run ada93506-dbc9-4117-a878-ee11fbf12df1 10；box_01 1bab40dc-b3db-47a1-a6ae-c20b1daa2541 10；hurt 454659b6-90d3-4e43-9c42-e17474df1492 10。总计125credits。失败的Mixamo idle 21ab4976-0137-4593-8393-5007b44192be已退款，收费0。完整记录artifacts/characters-v3/billing.json。

本地基线CombatValidate已由主任务执行通过：50套外观组合×30/60/120fps，最大回弹.0783m，接触误差.0321m，手嘴最小净距.0713m。此结果是接入Tripo和最后fall调整之前的基线，不能作为最后版本复验结果。

## 视觉复核后的修正

出拳在有限关节驱动后增加有界手部轨迹：轻击0.10s、重击0.16s达到命中姿势，重击先抬至肩外侧上方。手臂最多增加0.11m表现长度，不改变服务器攻击距离。检测最终手骨前伸及BakeMesh后的手掌顶点，避免仅目标角度通过而最终画面幅度不足。

服务器保留最后一次actionKind，表现层按动作时长让该事件过期；结束后可恢复真实Tripo走跑，吞噬/Recover交互和蓄力仍按当前状态覆盖。CombatAmplitudeValidate覆盖30/60/120fps的轻重击命中、屈膝及事件过期后的跑步恢复。新combat-contactsheet.png已实际渲染复核；此前600帧视频未自动刷新，需CombatReview重渲染。
