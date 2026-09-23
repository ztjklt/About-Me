# DS 娘装配与脸部增量交付

本交付沿用 DS 模块化女仆套装及原玩偶身体。嘴巴静止位置下移 0.035 单位，增加蓝色虹膜、深色瞳孔、双高光、上眼线和每侧三根睫毛。眼睛与眉毛垂直位置保持原样，没有新增眼镜；“眼镜／眼睛”含义及是否下移由 PM 等待用户澄清。本候选可供客户端接入和预览，不能把尚未确认的眼部位置标为已获批准。

## 唯一成品与限定范围

- 工作树：`/Users/jitian/.codex/worktrees/ds-girl-modular/About Me`，分支 `codex/ds-girl-modular`。HEAD `545b4ce` 是旧基线，**不是成品提交**；不要 cherry-pick HEAD 或整体复制工作树。
- 成品包：`artifacts/ds-girl-face-v2/DSGirl-FaceV2-Integration.zip`。其中 `integration-abc89c4.patch` 只修改 `CampusDoll.cs` 和 `DollSkin.cs`，明确针对客户端基线 `abc89c4`；`new-files/` 为限定新增文件；`manifest.json` 记录每份文件 SHA256。
- `source-and-evidence/` 保存 Blender 源文件、服饰源 JSON、原身体拟合数据、当前引擎图和验证结果。FBX、材质及 Unity Mesh 位于 `new-files/client/Assets/AboutMe/Art/DSGirlModular/`。
- 原 `DSGirl-Modular-Delivery.zip` 是此前服饰版本，未含本次脸部增量；新接入只使用本次成品包。
- 不包含旧整模 `DSGirlMotionAdapter`、历史 Tripo 整模、GPT 角色、菜单、网络或服务端修改。

## 客户端接入契约

此补丁只负责外观，依赖客户端提供 `AppearanceData.character`，值为 `normal` 或 `ds`；`SameAs` 必须比较 character，复制和规范化必须保留合法值。干净 `abc89c4` 尚无此字段，所以补丁本身不是一个可独立构建的完整功能分支。客户端负责将可信 `characterSkin` / `dsGirlUnlocked` 映射到该字段，接入“喜欢 DeepSeek”解锁、显式选择、保存和房间同步。

1. 在隔离客户端分支先检查并应用 `integration-abc89c4.patch`，再按原路径复制 `new-files/`。若目标已改动上述两个文件，按补丁合并，不覆盖客户端的新功能。
2. 保持原 ToyActor 身体 Skin 启用。`DSGirlOutfit.Attach(ToyActor actor)` 从 `Resources/CampusDoll/DSGirlModular.prefab` 装配服饰，直接使用 `actor.Skin.bones`。
3. 原身体最终姿态和物理姿态应用后调用 `Pose(dt, opening, belly)`。销毁或换装前调用 `Release()`，避免辅助骨残留。补丁已经接好这些调用。
4. 使用原头部、背部、左右手挂点；不创建 Animator，不创建碰撞体，不绑定旧整模动作适配器。
5. 单位与原身体一致，Y 向上、Z 向前；配件相对 actor 为位置 0、旋转单位、缩放 1。身体根在世界 Y≈1 时原脚底贴地，不沿用旧整模 FBX 的额外旋转或缩放。
6. 眼睛、瞳孔、高光、睫毛进入原身体蒙皮及眨眼/吞噬形变；嘴巴下移量随张嘴归零，完全张嘴沿用原开口。喂食手部目标补偿静止嘴位偏移，保持原动作包络。

引擎验收入口：`AboutMe.Editor.DSGirlFaceReview.Review`。若重导服饰，先把源数据恢复到 `artifacts/ds-girl-modular/`，再执行 `DSGirlModularTools.Import`；仅接入已制作 Prefab/网格不需要重导。

## 验证范围

当前脸部版本在团结 2022.3.62t11 的窗口外渲染中检查：正面、三分之四、近景、眨眼、六种表情、抓取与吞噬。原身体 22 根变形骨、15 根物理姿态骨不变。11 类动作共 660 帧及 20 次往返切换已通过；数值详见同包 `pose-parity.json`。喂食 120 帧，最大手部差异小于 0.000001 单位，完全张嘴位置差异为 0。眨眼形变覆盖新增虹膜。

服饰几何保持 10,762 三角面、6,614 顶点、8 根外观辅助骨、1 个材质与 1 个额外 SkinnedMeshRenderer、零贴图。计入新增眼部几何相对原眼部的增量后，新增外观合计仍不超过 12,000 面，具体计数及断言见 `face-validation.json`。脸部细节合入原身体渲染器，不增加单独眼部渲染器。

服饰 FBX、Mesh、材质、Prefab 与 DSGirlOutfit 相对此次脸部任务开始时 SHA256 全部一致，证明衣服和绑定没有被替换。Blender 中的原身体穿着预览已刷新为最新脸部。

此前服饰版本已有 macOS 自动物理对局录屏、六角色短时性能对比和 WebGL 构建证据，详见 `DS_GIRL_MODULAR.md`。这些运行包和性能数据**早于本次脸部增量**，不能视为新版整合包运行验收。本次客户端完整 macOS / WebGL 构建与彩蛋流程由客户端整合任务继续验证；手机、微信真机、真人多人联机均未验证。未发布线上。

## 来源与费用

参考图由用户提供，使用项目原身体，服饰和脸部几何在本地制作。没有新增第三方素材、贴图或付费服务调用。原 Tripo 网络失败后按用户批准的 Blender 备用方案完成，累计消耗 0 点。
