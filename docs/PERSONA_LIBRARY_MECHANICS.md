# Persona 道具机制清单（首批扩库）

状态：目录与客户端当前模板映射；代码静态审读加资源校验，不代表真人平衡试玩。每件道具只继承一类已实现模板；名称、颜色和模型不会赋予私有数值或附加技能。以下“机制”均指当前重击行为。

| visualId | 道具 | 模板 | 当前机制 | 可观察反制 |
| --- | --- | --- | --- | --- |
| `basketball` | 篮球 | `Projectile` | 直线投掷，命中后消失 | 横移躲开飞行路线 |
| `school-disc` | 大学校徽圆盘 | `Disc` | 蓄力重击直线投出；首次命中后或触地后落地保留 6 秒，单次投掷最多命中一名玩家；命中使用现有重击伤害、击倒与掉落判定 | 横向闪避；看准它落地后再接近 |
| `book` | 知识之书 | `Melee` | 正面 2.8 米玩具重击 | 拉开距离或侧移 |
| `computer` | 电脑 | `HeavyWeapon` | 较强近战冲量，收招较慢 | 避开正面，趁收招反击 |
| `camera` | 相机 | `Utility` | 较远距离的定向玩具冲击，冷却较长 | 侧移并趁冷却接近 |
| `plant` | 盆栽 | `Deployable` | 抛出后减速落地，短时占位 | 绕开落点 |
| `cat` | 猫 | `Summon` | 短时追逐最近对手 | 拉开距离并打断使用者 |
| `robot` | AI 机器人 | `SpecialAbility` | 近身范围冲击，冷却较长 | 保持距离并趁冷却反击 |
| `basketball-cc0` | 低模篮球 | `Ricochet` | 飞行道具在边界或静态障碍反弹 | 观察反弹方向并移动 |
| `bowling-ball` | 保龄球 | `Ricochet` | 飞行道具在边界或静态障碍反弹 | 观察反弹方向并移动 |
| `bowling-pin` | 保龄球瓶 | `Projectile` | 直线投掷，命中后消失 | 横移躲开飞行路线 |
| `baseball-bat` | 棒球棒 | `Melee` | 正面 2.8 米玩具重击 | 拉开距离或侧移 |
| `book-single` | 单本书 | `Melee` | 正面 2.8 米玩具重击 | 拉开距离或侧移 |
| `book-stack` | 书堆 | `Barricade` | 放置短时可重击拆除的掩体 | 绕行或重击拆除 |
| `red-pen` | 红笔 | `Projectile` | 直线投掷，命中后消失 | 横移躲开飞行路线 |
| `computer-tower` | 电脑主机 | `HeavyWeapon` | 较强近战冲量，收招较慢 | 避开正面，趁收招反击 |
| `flashlight` | 手电筒 | `Tether` | 前方短时牵引，无直接伤害 | 横移脱离前方目标锥 |
| `pillow-a` | 方抱枕 | `Cushion` | 短时抵挡一次重击并反弹 | 诱发防护后等待结束 |
| `pillow-b` | 圆抱枕 | `Cushion` | 短时抵挡一次重击并反弹 | 诱发防护后等待结束 |
| `armchair` | 单人沙发 | `HeavyWeapon` | 较强近战冲量，收招较慢 | 避开正面，趁收招反击 |
| `armchair-pillows` | 抱枕扶手椅 | `Cushion` | 短时抵挡一次重击并反弹 | 诱发防护后等待结束 |
| `couch` | 双人沙发 | `Barricade` | 放置短时可重击拆除的掩体 | 绕行或重击拆除 |
| `couch-pillows` | 抱枕沙发 | `Cushion` | 短时抵挡一次重击并反弹 | 诱发防护后等待结束 |
| `cactus-small-a` | 小仙人掌 | `Deployable` | 抛出后减速落地，短时占位 | 绕开落点 |
| `cactus-small-b` | 圆仙人掌 | `Deployable` | 抛出后减速落地，短时占位 | 绕开落点 |
| `cactus-tall-a` | 高仙人掌 | `Deployable` | 抛出后减速落地，短时占位 | 绕开落点 |
| `cactus-tall-b` | 分枝仙人掌 | `Deployable` | 抛出后减速落地，短时占位 | 绕开落点 |
| `table-lamp` | 台灯 | `Utility` | 较远距离的定向玩具冲击，冷却较长 | 侧移并趁冷却接近 |
| `standing-lamp` | 落地灯 | `Utility` | 较远距离的定向玩具冲击，冷却较长 | 侧移并趁冷却接近 |
| `photo-frame` | 照片框 | `Disc` | 飞出后返回，单目标最多命中一次 | 避开去程与回程路线 |
| `standing-frame` | 立式相框 | `Disc` | 飞出后返回，单目标最多命中一次 | 避开去程与回程路线 |
| `oval-rug` | 椭圆地毯 | `Barricade` | 放置短时可重击拆除的掩体 | 绕行或重击拆除 |
| `stool` | 小圆凳 | `HeavyWeapon` | 较强近战冲量，收招较慢 | 避开正面，趁收招反击 |
| `burger` | 汉堡 | `Projectile` | 直线投掷，命中后消失 | 横移躲开飞行路线 |
| `veggie-burger` | 蔬菜汉堡 | `Projectile` | 直线投掷，命中后消失 | 横移躲开飞行路线 |
| `dinner-plate` | 晚餐盘 | `HeavyWeapon` | 较强近战冲量，收招较慢 | 避开正面，趁收招反击 |
| `stew` | 炖菜 | `Cushion` | 短时抵挡一次重击并反弹 | 诱发防护后等待结束 |
| `carrot` | 胡萝卜 | `Ricochet` | 飞行道具在边界或静态障碍反弹 | 观察反弹方向并移动 |
| `tomato` | 番茄 | `Projectile` | 直线投掷，命中后消失 | 横移躲开飞行路线 |
| `potato` | 土豆 | `Projectile` | 直线投掷，命中后消失 | 横移躲开飞行路线 |
| `steak` | 牛排 | `HeavyWeapon` | 较强近战冲量，收招较慢 | 避开正面，趁收招反击 |
| `bun` | 面包胚 | `Ricochet` | 飞行道具在边界或静态障碍反弹 | 观察反弹方向并移动 |
| `bowl` | 碗 | `Disc` | 飞出后返回，单目标最多命中一次 | 避开去程与回程路线 |
| `plate` | 餐盘 | `Disc` | 飞出后返回，单目标最多命中一次 | 避开去程与回程路线 |
| `cooking-pot` | 小锅 | `HeavyWeapon` | 较强近战冲量，收招较慢 | 避开正面，趁收招反击 |
| `toy-airplane` | 玩具飞机 | `Ricochet` | 飞行道具在边界或静态障碍反弹 | 观察反弹方向并移动 |
| `crowbar` | 撬棍 | `Tether` | 前方短时牵引，无直接伤害 | 横移脱离前方目标锥 |
| `screwdriver` | 螺丝刀 | `Tether` | 前方短时牵引，无直接伤害 | 横移脱离前方目标锥 |
| `wrench` | 扳手 | `Tether` | 前方短时牵引，无直接伤害 | 横移脱离前方目标锥 |
| `hammer` | 羊角锤 | `Melee` | 正面 2.8 米玩具重击 | 拉开距离或侧移 |
| `watering-can` | 浇水壶 | `Tether` | 前方短时牵引，无直接伤害 | 横移脱离前方目标锥 |
| `tree-round` | 圆冠小树 | `Summon` | 短时追逐最近对手 | 拉开距离并打断使用者 |
| `pine-tree` | 松树 | `Summon` | 短时追逐最近对手 | 拉开距离并打断使用者 |
| `trashcan` | 垃圾桶 | `Barricade` | 放置短时可重击拆除的掩体 | 绕行或重击拆除 |
| `sword` | 短剑 | `Melee` | 正面 2.8 米玩具重击 | 拉开距离或侧移 |
| `toy-bomb` | 玩具炸弹 | `SpecialAbility` | 近身范围冲击，冷却较长 | 保持距离并趁冷却反击 |
| `football-cc0` | 足球 | `Ricochet` | 飞行道具在边界或静态障碍反弹 | 观察反弹方向并移动 |
| `gamepad-cc0` | 游戏手柄 | `Utility` | 较远距离的定向玩具冲击，冷却较长 | 侧移并趁冷却接近 |
| `ukulele-cc0` | 尤克里里 | `Melee` | 正面 2.8 米玩具重击 | 拉开距离或侧移 |
| `notebook-cc0` | 纸质笔记本 | `Disc` | 飞出后返回，单目标最多命中一次 | 避开去程与回程路线 |

## 新增四件的来源、视觉与生成边界

| 道具 | 合格介绍片段示例 | 视觉来源 | 发布条件 |
| --- | --- | --- | --- |
| 足球 | “我每周踢足球” | [Poly Haven football](https://polyhaven.com/a/football)，CC0，1K FBX 精选充气球体 | `Ricochet/football-cc0`；只在介绍直接提到普通足球时选择，排除“美式足球” |
| 游戏手柄 | “我喜欢用手柄玩游戏” | [Poly Haven gamepad](https://polyhaven.com/a/gamepad)，CC0，1K FBX | `Utility/gamepad-cc0`；只表现远距玩具冲击，不宣称操纵对手 |
| 尤克里里 | “我会弹尤克里里” | [Poly Haven Ukulele 01](https://polyhaven.com/a/Ukulele_01)，CC0，1K FBX | `Melee/ukulele-cc0`；不增加音波、眩晕或演奏加成 |
| 纸质笔记本 | “我习惯用纸质笔记本记手账” | [Poly Haven binder notebook](https://polyhaven.com/a/binder_notebook)，CC0，1K FBX 精选展开模型 | `Disc/notebook-cc0`；排除“笔记本电脑”“电脑笔记本”“notebook computer”等电子设备表述 |

游戏内的 `source` 必须逐字对应玩家介绍中的连续片段；以上只是生成测试输入，不写入固定玩家资产。四件没有专属 Buff，掉落、回收、吞噬和生命数量遵循统一规则。

## 尚需玩法与 Unity 复核

现有飞行物未统一做墙面遮挡，`Tether` 未检查遮挡，`Summon` 目前不可击杀且可重复命中，`Deployable` 不能主动拆除。这些限制来自玩法负责人本轮交付的代码审读结论；若需进一步差异化，先由玩法制定模板参数和反制，再由 Unity 修改权威逻辑及表现。
