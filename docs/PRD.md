# About Me — 产品需求文档（PRD）

**版本：V0.1**  
**平台：微信小游戏优先**  
**类型：3D 多人物理派对对战**  
**人数：2–6 人动态支持**  
**目标单局时长：约 15 分钟**

---

## 1. 产品定义

### 1.1 一句话介绍
**About Me** 是一款 2–6 人 3D 多人物理派对对战游戏。玩家通过一段自我介绍建立长期 Persona，AI 从中提取具有辨识度、可视觉化、可游戏化的信息，将其转化为可战斗、可掉落、可抢夺、可吞食的 3D Persona 道具。

> **你的“自我”既是武器，也是生命。**

玩家通过战斗让其他人的 Persona 掉落并尝试吃掉。一个玩家的全部 Persona 被吃掉后淘汰；最终仍拥有 Persona 的最后一名玩家获胜。

### 1.2 核心体验
传统对战：选择角色 → 选择武器 → 战斗。  
About Me：**介绍自己 → AI 将“你”游戏化 → 拿“自己”去战斗 → 把别人打出来 → 吃掉别人。**

核心社交笑点包括：“他把你的篮球吃了”“快把我的学校还给我”“谁把我的猫吃了”。

---

## 2. 核心设计支柱

### 2.1 Me as Content
现实个人信息 → AI 理解 → Persona → 游戏内容。玩家本人就是 UGC 来源。

### 2.2 Fight With Yourself
Persona 必须进入实际 Gameplay，而不是只做皮肤。篮球、学校、摄影、AI、专业等都可以被实体化。

### 2.3 Lose Yourself
Persona 同时是武器与生命资源。被打落后可回收，也可被别人吞噬；被吃后本局永久消失。

### 2.4 Social Comedy
不追求电竞级绝对公平。底层模板保持总体可玩，允许少量“荒诞的不公平”，优先制造朋友之间的节目效果。

---

## 3. 平台、房间与社交

- **微信小游戏优先**，从第一天按小游戏性能预算设计。
- 第一阶段以熟人房为核心，不以陌生人匹配为主。
- 加入流程：房主建房 → 二维码/微信分享 → 朋友扫码或点击 → 进入小游戏 → 自动进房 → 2–6 人 Ready → 开始。
- 核心场景：宿舍、聚会、同学朋友、社团、黑客松及线下活动。

---

## 4. 角色与美术

### 4.1 视觉方向
统一采用 **Stylized / Toy-like / Soft 3D**：
- 大头、短身体、粗短四肢
- 简洁低复杂度材质
- 玩具/橡胶感
- 统一基础身体、Skeleton、Animator、Physics Rig
- 半物理角色表现

不为每名玩家生成完全不同的身体模型，以保证动画、物理、联机与性能稳定。

### 4.2 外观
玩家自主选择基础颜色、服装和常规装饰；AI 根据 Persona 添加少量个性化装饰。

### 4.3 局外成长
不做永久攻击力、Persona 强化等数值成长。只允许皮肤、颜色、表情、装饰等非数值内容。

---

## 5. Persona 创建

### 5.1 输入
首次创建角色时完成自我介绍，支持**文字 + 语音**。以自由表达为主，可提供学校/工作、兴趣、地域、经历、爱好等非强制提示。

### 5.2 长期 Persona
Persona 是长期角色资产，以后每局继续使用。玩家可以随时修改自我介绍并重新生成 Persona。

### 5.3 真实性
**完全不验证真实性。** 玩家可以夸张、虚构或为了玩法增加设定。

### 5.4 数量
不设固定数量，也不执行“一条信息 = 一个 Persona”。AI 根据辨识度、可视觉化程度、游戏化潜力、与玩家关联度判断。自我介绍越丰富，通常能生成越多 Persona；更多 Persona 也确实带来更高生存空间。

### 5.5 不允许单项刷结果
玩家不能单独重 Roll 某个 Persona。若不满意，只能修改原始自我介绍后重新生成。

---

## 6. Persona AI Pipeline

```text
Self Introduction
        ↓
Semantic Understanding
        ↓
Candidate Persona Extraction
        ↓
Game-worthiness Evaluation
        ↓
Persona Concept
        ↓
Gameplay Template Mapping
        ↓
Visual Configuration
        ↓
Persona Item
```

采用 **AI Creative Layer + Controlled Gameplay Template**。

AI 可以决定：哪些信息成为 Persona、名称、视觉隐喻、外观和主题包装；但不能自由创造不可控的底层战斗逻辑。

主要 Gameplay Template 可包括：
- Projectile
- Disc
- Melee
- Heavy Weapon
- Utility
- Deployable
- Summon
- Special Ability

例如：
- 篮球 → Basketball Visual → Projectile
- 中山大学 → School Disc Visual → Disc
- 经济学 → AI 生成视觉隐喻 → 既有 Template

少数高频且辨识度极强的 Persona 可由开发团队制作专属机制。

---

## 7. Persona Inventory

采用**快捷栏 + 完整背包**。常用 Persona 放快捷栏，其余保存在完整背包。

切换流程：当前 Persona 收回 → 短暂动画 → 新 Persona 掏出。没有额外切换 CD，但切换窗口可被攻击。

对手只能看到“昵称 · Persona ×剩余数量”，不能查看完整库存。

---

## 8. 战斗系统

### 8.1 Persona = Weapon + Life
当前正在使用的 Persona 在满足掉落条件时**优先掉落**，形成 Risk/Reward。

### 8.2 Persona 操作
统一使用 **Light Attack + Heavy Attack**：
- 篮球：轻击近距离砸；重击蓄力投掷
- 圆盘：轻击拍人；重击飞盘投掷
- 相机：轻击物理攻击；重击闪光等特殊效果

普通 Persona 主要靠前摇、后摇、蓄力、硬直、失衡限制频率；特殊能力才使用明确 CD。

### 8.3 空手
空手仍可推、抓、拉、轻击、抢位置、打断吞噬/回收，但削韧、击飞和输出明显弱于 Persona。

---

## 9. Stability / 韧性

不采用传统 HP 清零死亡。

```text
普通攻击 → 削韧 → 连续受击 → 不稳定
→ Heavy Hit / 强碰撞 / 坠落
→ Persona Pop → Persona 飞出
```

Persona 主要由连续受击后的重击、强碰撞、明显失衡、大手攻击和坠落等高强度事件打出。数值后续原型测试。

---

## 10. 抓取

玩家可以抓其他玩家，进行拉扯、拖拽、阻止逃跑、推向危险区域、打断吞噬和回收。

也可以抓住对方当前手里的 Persona，但**抓住 ≠ 直接抢走**；必须通过拉扯、攻击或失衡使其真正脱手。

---

## 11. Persona 掉落、回收与吞噬

### 11.1 掉落
满足爆装条件后，优先掉当前使用的 Persona。备用掉落算法待定。

### 11.2 地面存在
掉落 Persona **永久留在场上**，不会自动消失或回到主人，直到：
1. 原主人成功回收；
2. 其他玩家成功吞噬。

### 11.3 回收
原主人必须：找到 → 拿起 → Recover 动作 → 完成 → 回到 Inventory。过程可被攻击打断。

### 11.4 吞噬
其他玩家不能使用别人的 Persona，只能吃掉：

```text
拿起 → Eat → 张大嘴 → 硬塞入口中 → 吞咽
→ Persona 本局永久消失
```

吞噬需要时间，可被打断。篮球、电脑、圆盘等大型物体也要用夸张动画真正“吃”进去。

成功后全局播报：
> **「玩家 A 吃掉了玩家 B 的『篮球』！」**

吞噬者不继承能力、不获得 Buff。

---

## 12. 淘汰、坠落与观战

- 唯一核心淘汰条件：**Persona 数量 = 0**。
- Persona 全被吃掉后淘汰，进入纯观战，不能继续影响比赛。
- 掉出地图不会直接淘汰；玩家重新进入 Arena，但需要付出 Persona 代价。具体掉落数量后续平衡。
- 坠落不能绕过 Persona 淘汰机制。
- 最后一个 Persona 不获得额外强化或保护。

---

## 13. 角色运动与物理

采用 **Stable Control + Physics Reaction**。

正常移动、跳跃、转向、攻击必须稳定；强受击时使用 Stagger、Knockback、Partial Ragdoll、Ragdoll、Fall。

目标体验：**角色平时很好控制，但被揍的时候非常蠢。**

玩家可以自由跳跃。

---

## 14. 地图

采用 **Small Arena**，让 2–6 人持续碰撞，不强调探索或躲藏。

地图机关很少，仅保留：
- 少量高低差
- 边缘
- 小障碍
- 少量物理交互物
- 少量公共道具

公共道具如椅子、箱子、垃圾桶等可用于防身和干扰，但总体价值低于 Persona。

---

## 15. The Hand / 大手

游戏不设置传统 Boss、小怪或大量 NPC。

唯一第三方角色是 **The Hand**，定位为 **Game Master / 主持人 / 比赛节奏控制器**。

### 15.1 开局 Persona Reveal
比赛开始时 Persona 暂时不可抢。大手一次性把所有玩家拍扁，所有人同时爆出部分代表性 Persona。

此阶段只展示，不可抢、不可吃。随后 Persona 回归玩家，大手宣布类似：

> **“你们可以开始互相介绍了。”**

正式开战。

### 15.2 周期事件
正常比赛中**每 5 分钟触发一次**。大手通过“局势判断 + 一定随机性”选择一个或多个目标，而不是机械全拍。

被拍中的玩家强制爆出部分 Persona，此时掉落立即进入正常争夺状态。

---

## 16. 单局时长与终局

目标单局约 **15 分钟**。如果提前只剩一名仍拥有 Persona 的玩家，则立即结束。

若 15 分钟到时仍有多人存活，触发 **Final Persona Burst**：

> 大手出现 → 拍扁所有存活玩家 → 所有剩余 Persona 全部爆出 → 最终抢夺战

玩家同时抢救自己的 Persona、吃别人的 Persona、打断吞噬和回收，直到只剩最后一个仍拥有 Persona 的玩家。

胜利条件：**Last Persona Holder Standing**。

---

## 17. 赛后结算

重点展示 **What Did You Eat?**

例如：
- 张三的「篮球」
- 王五的「相机」
- 李四的「学校圆盘」
- 赵六的「猫」
- 张三的「AI 机器人」

结算应具有强视觉喜剧感并适合截图分享。

系统记录吞噬者、Persona 原主人、Persona 名称和对局信息。

---

## 18. 手机端操作

第三人称跟随镜头，位于角色后上方，玩家可控制视角。

基础输入：
- 左摇杆：移动
- 右侧拖动：镜头
- Jump：跳跃
- Grab：抓取
- Light Attack：轻击
- Heavy Attack：重击
- Persona 快捷栏：快速切换
- 完整背包：管理其他 Persona
- Eat / Recover：上下文交互

最终 UI 布局需真机测试，控制按钮密度。

---

## 19. 单局完整流程

```text
创建长期 Persona
        ↓
创建/扫码加入熟人房
        ↓
2–6 人 Ready
        ↓
进入 Small Arena
        ↓
The Hand 一次拍扁所有人
        ↓
Persona Reveal（不可抢）
        ↓
“你们可以开始互相介绍了”
        ↓
正式混战
        ↓
攻击 → 削韧 → 重击 → Persona 掉落
                    ↙           ↘
             原主人 Recover    对手 Eat
                    ↘           ↙
                     均可被打断
                          ↓
              被吃 Persona 本局永久消失
                          ↓
               每 5 分钟 The Hand 介入
                          ↓
              Persona = 0 → 淘汰 → 观战
                          ↓
                 15 分钟仍多人存活
                          ↓
                 Final Persona Burst
                          ↓
                     最终抢夺战
                          ↓
              Last Persona Holder Standing
                          ↓
                  What Did You Eat? 结算
```

---

## 20. 当前明确不做

V0.1 不包含：
- 实体剑、自研硬件、IMU、蓝牙交互
- 传统 Boss 战和普通 PvE 小怪
- 大型探索地图
- 大量复杂地图机关
- 淘汰后的幽灵干扰
- 传统 HP 死亡制
- 吞噬后继承别人 Persona
- 被吃 Persona 本局自动恢复
- Persona 单项无限重 Roll
- 永久战斗数值成长
- 电竞级绝对平衡
- 陌生人匹配优先

---

## 21. MVP / Vertical Slice

第一版不要实现“无限 Persona”，而要验证三个核心问题：

1. **Persona 既是武器又是生命，是否真的好玩？**
2. **打落 → 抢夺 → 回收/吞噬是否能持续制造冲突和笑点？**
3. **开局 Reveal + The Hand + Final Burst 是否能形成完整单局节奏？**

建议第一版范围：
- 2–4 人联机先跑通，架构保留 6 人
- 1 张 Small Arena
- 1 套统一软体小人
- 移动 / 跳跃 / 抓取 / 轻击 / 重击
- Stability + 击飞 + Ragdoll
- 6–10 个 Persona Gameplay Template/示例道具
- Persona 掉落 / Recover / Eat
- The Hand 开局 Reveal
- 1 次周期大手事件
- Final Persona Burst
- Persona 数量 UI
- What Did You Eat? 结算
- Persona AI 第一版可先用结构化文本 → Template 映射验证，不必一开始实现无限资产生成

---

## 22. 待原型阶段确定

以下内容不在当前 PRD 中强行定死：
- Stability 具体数值
- Recover / Eat 精确时长
- 坠落具体掉几个 Persona
- 大手周期事件的目标选择权重
- 大手每次拍落多少 Persona
- Persona Template 初始完整列表
- 角色移动速度、跳跃高度、击飞力度
- 微信小游戏最终网络同步方案
- 模型面数、骨骼数、材质和内存预算
- 第一张 Arena 的具体主题
- Persona 内容安全与不可游戏化信息的处理边界

---

## 23. 产品核心判断

About Me 的差异化不在“AI 生成一个武器”，而在于形成完整闭环：

> **现实中的我 → AI 理解我 → 游戏把我实体化 → 我拿自己去打朋友 → 我的自我被打出来 → 朋友把我的一部分吃掉。**

只要这个循环本身足够好玩，AI、Persona、物理战斗和熟人社交就不是四个拼在一起的功能，而是同一个玩法系统。
