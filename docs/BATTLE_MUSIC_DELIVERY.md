# 战斗背景音乐

- 曲目：`Happy People`，用户提供的文件标记为 `米罗爷爷 - 【118】Happy People - Mat Andasun.mp3`。
- 源文件：用户在 2026-09-24 的对话中提供；仓库导入路径为 `client/Assets/AboutMe/Resources/Audio/BattleMusic.mp3`。源文件 SHA-256：`893acdfee3bc3a52a6189a6640d61249c6e66b3ba7b947015947606238520e39`。
- 游戏行为：Persona 展示结束并进入 Battle 时淡入循环，FinalBurst 继续播放；Lobby、Reveal、Finished、离开房间和失去联机连接时淡出停止。设置页可选择关闭、轻柔、标准或响亮，默认标准。
- Unity 导入：44.1 kHz 立体声，Streaming、Vorbis，避免把约 136 秒的音频完整解码驻留内存。
- 授权：目前只有用户提供文件和用于本游戏的要求，仓库中没有独立的公开发行授权证明；正式对外发布前由产品负责人确认曲目使用权。
