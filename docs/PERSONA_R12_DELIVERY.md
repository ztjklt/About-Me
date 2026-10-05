# 完整 R12：吞噬者调侃

主责：联机与后端负责权威触发、鉴权和广播；Unity 客户端负责单人人机接入与角色气泡。此版本在已部署 R11 上增量整合，保留原有玩法、20件 Persona、16型 MBTI、277道具／96机制、四种动物动画和1–6人机。

## 行为

- 真人、陪练 NPC 和 AI 皮肤角色开始吃别人的 Persona 时，各自生成一条吞噬尝试。服务端只发公开道具名称、固定目录类型给 DeepSeek，不发自我介绍或主人信息。
- 提示词要求以吞噬者口吻，对具体道具的形状、用途或手感开一句玩笑。中文口语可以省略第一人称主语；发言者由权威状态明确指定。
- 公开快照的调侃事件包含 `speakerId`、`itemId`、`roastId` 与真实句子。所有观察者把气泡定位在该吞噬者头顶；每个角色独立显示。字幕同时标明昵称。
- 找回自己的道具不触发。文字积木、借用后吞噬的文字也接入。取消或被打断的尝试不播放迟到回复；正常完成后仍可说出稍晚返回的句子，持续8秒。生成期间显示“调侃酝酿中…”。
- 联机由权威服务器调用 `/internal/roast`；单人练习通过玩家会话调用 `/roast/eating`，真人与所有 NPC 共用该本机对局鉴权。客户端不包含服务密钥。样例练习也允许真实调用。Linux权威发布构建单独允许Railway内网HTTP，再恢复客户端构建设置；公网客户端使用HTTPS入口。
- 同一尝试的重复请求读取缓存。本机对局调侃来源默认30次／分钟，允许六个人机加真人同时各发一条；介绍生成仍保留原6次／分钟来源门槛，总体30次／分钟和每日500次上限不变，服务端可配置。保留内容检查；超时、断网、审核拒绝和额度耗尽仍可能使某次调侃不可用，不用预设句冒充 DeepSeek，也不延长吞噬动作等待模型。

## 验收范围

后端506项测试通过，覆盖鉴权、公开数据边界、事件缓存与玩家隔离、文字积木和既有功能。Unity完整规则／菜单检查通过，新增20项检查覆盖真人、NPC、AI皮肤、吞噬起始、取消、找回、迟到回复、观察者归属和借用文字、七个同时气泡互不遮挡。

Mac原生客户端在显式准备的吞噬状态中，真人吃篮球、人机吃相机，两名角色均取得真实DeepSeek回复；气泡显示在各自头顶，截图验证互不遮挡。这是自动准备场景和本机权威／真实API证据，不代表真人操作手感验收。

两个实际Mac自动客户端连接Railway生产WSS，一个吞噬样例道具、一个观察者：同一句真实调侃在吞噬完成前已广播，`speakerId`／`itemId`正确，观察者收到相同文字。此证据来自同一网络，未覆盖两地真人、完整对局或微信真机。

公网HTTPS独立调用篮球和相机均返回200／`aiSource=provider`，耗时约1.5–1.8秒；重放读取同一缓存。实际公网下载的四个WebGL资源哈希与发布清单完全一致；首页和版本清单禁缓存，显示最新版R12。Mac／Web／Linux均成功生成Release构建，本机默认入口的客户端／API／权威来源及真实进程检查通过。

Linux发布包的旧设置曾拦截Railway内网HTTP。已在专用权威服务器构建期间设置允许HTTP并恢复客户端设置；修正后以生产WSS真实吞噬和观察者广播作为验收证据。

默认入口 `scripts/start-latest.command` 指向完整 R12；本机匹配 API／权威端口为8096／7796。只打包冻结服务，不随当前工作树代码变化。生产网址沿用 Railway 入口，版本角标和 `/version.json` 标明实际提交，静态资源由内容指纹更新。

## 发布身份

- 版本：`playtest-persona-r12`；分支：`codex/eater-roast-r12`；运行源码：`b6e05be811dc543960e8104e6914d5e28df8333f`。后续交付文档提交不改变运行构建。
- GitHub：`https://github.com/ztjklt/About-Me`；公网入口：`https://web-production-fb9b1.up.railway.app/`；网页角标：`最新版 R12 · b6e05be`。
- Web内容指纹：`2af20e2485e04e24`；Linux归档SHA256：`d2f2cf83a40054717bfe4cfe6d9453b792a92533cad00ea4b25a211283ad6ab7`。
- 源码冻结：`artifacts/railway-r12/source-freeze`，4505文件；构建清单：`builds/playtest-persona-r12-mac/release-manifest.json`。Mac、Web、冻结服务位于`builds/playtest-persona-r12-{mac,web,service}`；Linux位于`builds/playtest-persona-r12-linux`。
- 结果证据：`artifacts/railway-r12/public-checks.json`、`network-final/result.json`、`runtime-delivery/eater-bubbles.png`及`deployments.json`。规则与API测试结果与上述真实API、公网自动客户端证据区分记录。
- 已知限制：模型和网络有延迟，审核、断网或配置额度耗尽仍可能跳过某句；没有使用预设句冒充AI。未完成真人手感、两地真人或微信真机验收。
