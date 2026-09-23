# DS 娘彩蛋后端接口

DS 娘是纯视觉角色。服务端只按玩家**提交的自我介绍原文**判断 `dsGirlUnlocked`，不读取模型生成的 Persona 名称或来源作为解锁证据。`喜欢DeepSeek` 支持英文大小写和 `Deep Seek` 中的空格；`不喜欢DeepSeek`、条件句、他人的喜好不解锁。同一介绍里有相反表述时，以最后一个明确的本人喜好表述为准。

`POST /personas/generate` 成功保存介绍后，响应的可信 Profile 增加：

```json
{"dsGirlUnlocked":true,"characterSkin":"normal"}
```

`GET /profiles/me`、微信身份恢复与 `POST /internal/verify` 返回同样字段。旧资料没有选过角色时默认 `characterSkin:"normal"`；服务端会用旧资料已保存的介绍重新判定解锁。新介绍取消解锁时，已选择的 DS 娘自动回到 `normal`。模型生成失败或资料保存冲突时，旧资料与选择保持原样。

复用 `POST /profiles/me/appearance` 保存显式选择：原有 `appearance`、`expectedRevision` 必填，新增可选 `characterSkin` 和 `introduction`。`characterSkin` 仅接受 `"normal"` 或 `"ds-girl"`；省略时保留当前选择。传入 `introduction` 时，服务端直接依据该原文重算解锁和角色选择，不调用模型。这样只有“我喜欢 DeepSeek”而无可转化道具的介绍也能解锁。未解锁而选择 `"ds-girl"`、未知值或客户端自报 `dsGirlUnlocked` 返回 422；版本过期返回 409。成功时 `appearanceRevision` 加一，并在响应中返回完整 Profile。Game 只能使用 `/internal/verify` 的可信选择；多人快照传角色外观供客户端绘制，不能接受 Hello 自报覆盖。

已有 Persona 生成成功时也会根据新介绍重算解锁；生成失败时保留此前状态。此接口不改变 AI 生成及道具事实规则，也不发起额外模型调用。
