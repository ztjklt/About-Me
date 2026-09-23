# Persona 首批四件客户端整合

客户端纳入足球 `Ricochet`、游戏手柄 `Utility`、尤克里里 `Melee`、纸质笔记本 `Disc` 的受控目录、52 条视觉清单、模型纹理和 Unity prefab。四件继续使用现有模板规则；本地样例仍固定为八类各一件。共享目录由后端 `230bb81` 建立、`be0f6b8` 同步“纸质笔记本”名称与英文误配排除词；两份客户端 Resources 目录与共享目录逐项一致。

## 复现入口

- `sh scripts/editor.sh TestRules`：现有权威规则及菜单 / NPC 回归。
- `sh scripts/editor.sh ValidatePersonaVisuals`：模型、prefab、`Resources.Load` 路径、元数据和材质校验。
- 团结编辑器执行 `AboutMe.Editor.PersonaFourClientAudit.Run`：逐件检查模型加载、装备、掉落、回收、吞噬和对应 `use_item` 模板效果，并检查本地样例 8 件。
- macOS Development 构建添加 `--persona-four-audit -batchmode -nographics`：在玩家构建中执行同一审计，输出 `PERSONA_FOUR_CLIENT_AUDIT_PASSED` 后退出。

后续单一攻击规则已将审计输入改为 `use_item`，并检查“纸质笔记本”名称。此前 `charge`→`heavy` 的结果仅属于旧构建；本轮同版构建与网络验收见 [联机安全记录](NETWORK_SAFETY.md)。

新整合基线 `61470c8` 已在团结编辑器和同版 macOS 玩家构建中通过上述检查，日志为 `artifacts/IntegratedPersonaFourClientAudit.log` 与 `artifacts/IntegratedPersonaFourPlayerAudit.log`；此前隔离构建记录保留在 `artifacts/persona-four-integration/`。证据覆盖离线权威 Match、资源加载和开发构建执行；真实模型输出、真人操作手感、微信真机和公网尚待各自验收。
