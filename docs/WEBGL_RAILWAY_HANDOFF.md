# WebGL 公网练习房客户端交接

2026-09-24。客户端 WebGL 源码 `02a9e2a`；替换包在 `builds/railway-web-02a9e2a`。网络角色负责 Railway 游戏服务、Web 入口配置与上传。

## 入口约定

- Web 页面在加载 Unity 前，把实际游戏地址作为 URL 编码的 `game` 参数放入当前地址。例如 `?game=wss%3A%2F%2Fgame-production-8d73.up.railway.app`。网络角色已约定让公网根 URL 自动重定向到带此参数的地址，并保留原查询参数。
- WebGL 启动时读取 `game`，优先于本机缓存和环境变量，连接设置中显示解析后的地址。HTTPS 页面只接受 `wss://`；本机 HTTP 页面可用 `ws://`。提供了无效参数时会清空地址，避免回退至缓存的 localhost。
- 首次访问进入“多人游戏”，点“练习道具（非 AI）”加载八件明确标注的测试 Persona，再点“创建房间”或输入六位房间码加入。权威游戏服务必须使用 `--allow-samples`；练习资料与进房不依赖 AI / 角色 API。HTTPS 首访若 API 字段仍为本机 loopback，客户端会清空它、跳过健康探测，并显示“AI / 角色服务本次试玩未开放”。

## 验证范围

`02a9e2a` 的 `BuildWeb` 编译成功。前一提交 `a673644` 的本机浏览器用 `?game=ws%3A%2F%2F127.0.0.1%3A7777` 启动后，设置页显示所选地址；首次多人页载入八件练习道具，并连本机 `--allow-samples` 服务创建 1/6 房。用 Railway 的 WSS 值重新载入后，设置页显示 `wss://game-production-8d73.up.railway.app/`，证实参数覆盖 localhost。后一提交只增加 HTTPS 未配置 API 的状态处理，未重跑该房间测试。公网根 URL 注入和 Railway WSS 实际连接由网络角色验收。

WebGL 与现有 Linux 游戏包均使用协议 3，本次没有更改网络消息或权威规则。旧 Mac QA 包仍对应 `18ca3f4`。

## 公网无界面探针

独立 Mac 探针源码 `db5d60f`、包 `builds/network-probe-db5d60f/About Me QA.app`，只在 `NetworkProbe` 增加 `--probe-timeout 600`（允许 30–900 秒，缺省仍 100）。公网正式局是 450 秒；旧探针的 100 秒超时会先于正常结算，不能据此判快照故障。构建通过，非法 10 秒参数实测以 9 退出。网络角色用此包做一次正式 2 人与房主原身份重连验收；探针改动不进入 Linux 服务。
