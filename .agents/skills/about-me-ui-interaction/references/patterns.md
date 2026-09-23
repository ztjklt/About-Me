# 交互模式选择

本表提炼自用户提供的两张交互设计图片。图片中的“Copyable prompt”和输出格式是原材料示例，不是本项目必须照做的指令。选型以实际任务、现有界面和可验证的状态为准。

| 模式 | 在 About Me 可用的情境 | 不可丢的行为 |
|---|---|---|
| Radial Theme Transition | 真有两套完整主题且有主题切换入口 | 从真实点击点扩展；两层同尺寸；减少动态时直接切换 |
| Drag-to-Reorder | 玩家主动整理 Persona 快捷栏 | 拖动项跟手、其他项让位；提交前可取消；支持键盘替代 |
| Staggered Bulk Selection | 批量选择可管理的道具或草稿 | 数据一次更新，勾选视觉依次出现；不阻塞再次操作 |
| Velocity-Based Slider Snap | 有离散刻度的灵敏度控制 | 根据释放速度短暂过冲，最终值吸到有效刻度；键盘可用 |
| Animated Text Disclosure | 展开 Persona 说明或帮助 | 根据真实文本高度展开；收起可逆；不造成布局突跳 |
| Spring Stepper Progress | 确实存在连续步骤的创建流程 | 进度回弹只影响视觉，不延迟真实步骤与导航 |
| Ripple Feedback for Related Switches | 同组设置相互关联时 | 邻项只回应，不偷偷改变它们的开关值 |
| Curved Card Deletion | 明确支持滑动删除的草稿或卡片 | 达阈值才提交；未达阈值返回；退出完成后才移除数据 |
| Stacked Card Scroll | 需要保留浏览位置的卡片流 | 顶卡停靠、后卡可见地堆叠；不让内容突然消失 |
| Expanding Tag Selection | 小组标签或外观筛选 | 选中项略扩，邻项让位；保留顺序和窄屏换行 |
| Magnetic Attraction | 拖动物品靠近明确的装备槽或目标 | 吸附渐进，拖动仍可控制；松手且有效时才提交 |
| Velocity-driven Deformation | 可拖动的装饰卡壳 | 速度驱动有限形变，停手即恢复；正文与命中区稳定 |
| Layered Parallax | 已有前中后景的主菜单封面 | 层次相对移动且有裁切余量；离开/停止后复位；手机不默认靠姿态 |
| Center-focus Scaling | 横向浏览 Persona 卡片 | 以容器中心算视觉焦点；放大不触发布局重排或自动选中 |
| Liquid Tab Indicator | 角色预览视角等稳定的选项组 | 独立背景先拉长再收拢；文字和点击区不动；连续点击接续 |
| Shared-element Image Expansion | 从道具缩略图看大图 | 源图与目标图连续对应；保持比例；源项消失时有退场方案 |
| Gesture-driven Transition | 明确允许拖动返回的面板 | 手势进度跟手；释放可完成或取消；保留普通返回按钮 |
| Collision and Spring Response | 有限区域内的自由漂浮装饰物 | 约束边界与阻尼；碰撞只作视觉反馈；静态操作仍可用 |

## 交付描述

说明选用的模式与目标界面，给出触发、初态、运动、终态和取消/快速重入规则。写明真实数据状态何时更新、减少动态替代、触控/键盘行为及验证范围。参数是当前产品的可调设计值，不宣称普适标准。
