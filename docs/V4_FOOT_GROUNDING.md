# V4 玩偶脚部接地

## 原因与处理

修正前的平地物理采样中，静止时鞋底平均离地约 5 cm；跑动时平均离地 43–48 cm，急停后仍约 50 cm。旧脚部碰撞盒底面比可见鞋底低约 5.8 cm。动画虽输出左右脚的着地权重，物理驱动此前只让双脚持续追逐动画目标。

权威物理骨架现在按真实地面接触区分支撑脚与摆动脚。支撑脚保存世界落脚点，脚底使用较高摩擦并由有限的肌肉力和腿部 IK 保持；摆动脚降低摩擦，按动画意图和地面高度寻找下一目标。脚踝另有有限朝向力矩，避免后退时鞋底翻向上方。普通跳跃、失衡、倒地和起身释放步态支撑；起身末段单独让双脚重新接近地面。鞋底与碰撞盒底面已对齐。

`DollCombatMotion.Input.strideMeters` 是物理目标骨架使用的可选步幅，未提供时沿用原相位速度。游戏移动规则仍为走 5.5、跑 7.8 m/s，`BodyPose` 和网络协议没有变化，客户端继续插值服务器骨骼快照。

## 验证

- `AboutMe.Editor.BodyPhysicsTests.ValidateFeet`：静止、前进、跑动、急停、侧移、后退和转向的鞋底高度、接触率、支撑时长和滑移；日志为 `artifacts/feet-grounding-verified.log`。
- `AboutMe.Editor.BodyPhysicsTests.Validate`：双人和六人骨骼快照与物理、四向倒地与起身、楼梯、跳跃、拖拽、外观蒙皮和性能；日志为 `artifacts/feet-physics-verified-final.log`。
- `AboutMe.Editor.BodyPhysicsTests.RenderReview`：正侧视角画面位于 `artifacts/feet-grounding-review/`；本次从修正前的跑动悬空画面复核到鞋底贴近地面。

最终专项采样中，七种移动阶段的鞋底平均离地约 0–1 cm，静止时约 0–0.2 cm；跑动阶段最远离地约 4.7 cm。高速移动时支撑脚仍可产生约 15 cm 的短时滑移，这是当前玩偶短腿与原有高移动速度共同造成的表现边界，不代表完全无脚滑。
