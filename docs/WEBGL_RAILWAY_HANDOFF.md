# WebGL 公网练习房客户端交接

2026-09-24。客户端源码 `a673644`；WebGL 替换包在 `builds/railway-web-a673644`。网络角色负责 Railway 游戏服务、Web 入口配置与上传。

## 入口约定

- Web 页面在加载 Unity 前，把实际游戏地址作为 URL 编码的 `game` 参数放入当前地址。例如 `?game=wss%3A%2F%2Fgame-production-8d73.up.railway.app`。网络角色已约定让公网根 URL 自动重定向到带此参数的地址，并保留原查询参数。
- WebGL 启动时读取 `game`，优先于本机缓存和环境变量，连接设置中显示解析后的地址。HTTPS 页面只接受 `wss://`；本机 HTTP 页面可用 `ws://`。提供了无效参数时会清空地址，避免回退至缓存的 localhost。
- 首次访问进入“多人游戏”，点“练习道具（非 AI）”加载八件明确标注的测试 Persona，再点“创建房间”或输入六位房间码加入。权威游戏服务必须使用 `--allow-samples`；练习资料与进房不依赖 AI / 角色 API。页面仍会尝试一次 AI 服务状态探测，未配置时显示不可达。

## 验证范围

`BuildWeb` 编译成功。本机浏览器用 `?game=ws%3A%2F%2F127.0.0.1%3A7777` 启动后，设置页显示所选地址；首次多人页载入八件练习道具，并连本机 `--allow-samples` 服务创建 1/6 房。用 Railway 的 WSS 值重新载入后，设置页显示 `wss://game-production-8d73.up.railway.app/`，证实参数覆盖 localhost。上述是本机页面与本机房间测试；公网根 URL 注入和 Railway WSS 实际连接由网络角色验收。

WebGL 与现有 Linux 游戏包均使用协议 3，本次没有更改网络消息或权威规则。旧 Mac QA 包仍对应 `18ca3f4`；`a673644` 的改动只为 WebGL 公网入口和多人练习资料。
