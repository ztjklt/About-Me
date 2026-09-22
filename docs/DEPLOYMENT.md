# 部署和外部依赖

## 你需要准备

1. 公网 Linux x86_64 主机及可登录账号。演示测试先按 4 核 8 GB 配置，容量与延迟以实测为准。
2. 三个指向该主机的域名，例如 `api.example.com`、`game.example.com`、`play.example.com`。开放 80/443；游戏端口 7777 仅在容器内使用。
3. 真实模型 API 地址、模型名称和密钥；语音 API 地址、模型名称和密钥。不要在聊天或 GitHub 中发布密钥。
4. 微信小游戏 AppID、AppSecret、微信开发者工具及体验成员。AppSecret 只进服务端环境。
5. 至少一位异地测试者、两种网络，以及 Android/iPhone 测试机。

## 公网电脑和浏览器联机

先生成 `builds/linux/AboutMeServer` 和 `builds/web`，将工程与构建产物同步到服务器。Linux Dedicated Server 模块须与引擎版本一致。

创建 `server/.env` 并填写实际配置；`GAME_SERVER_KEY` 使用随机长字符串。设置 `ALLOWED_ORIGINS=https://play.example.com`。

```sh
export API_DOMAIN=api.example.com
export GAME_DOMAIN=game.example.com
export PLAY_DOMAIN=play.example.com
docker compose -f deploy/compose.yaml up -d --build
```

访问游戏页面后，在连接设置填写 `wss://game.example.com` 和 `https://api.example.com`。Caddy 自动申请 HTTPS 证书并转发 WebSocket。外网无法访问 `/internal/*`；该接口仅由游戏容器调用。无需暴露 SQLite 或 API 容器端口。

首次验收用两台真正处于不同网络的电脑完成一局，检查两端赢家、吞噬记录和剩余数量一致；再扩展到 4、6 人。**本机多进程测试不代替异地验收。**

## 微信小游戏

官方 WXSDK 固定提交为 `a09d4b29daa1dd8358b09b5b5639554ab08cfdc2`。安装命令：

```sh
python3 scripts/install-wechat-sdk.py
```

重新打开团结工程，等待 Package Manager 完成安装。在微信小游戏转换面板配置真实 AppID、导出目录和 HTTPS 资源 CDN。选择对应小游戏构建平台，再生成并转换。缺少 AppID 或资源地址时不要将导出结果称为已可用的体验版。

微信开发者工具打开转换产物的 `minigame` 目录。在微信后台添加 request、downloadFile、socket 合法域名，添加体验成员，再上传体验版。

平台桥接覆盖微信分享参数、二维码 scene 参数、录音和登录；依赖真实 SDK、AppID、服务器配置及真机验证。二维码接口默认生成 `trial` 体验版码，可通过 `WECHAT_ENV_VERSION` 修改。

## 运行约束

- 对局内断线角色保留；重连使用原身份和原房间码，不做房主迁移，因为判定在独立服务器。
- 游戏服务器重启会中断内存中的比赛；长期 Persona 和成功写入的结算保存在持久卷。
- 目前是演示部署，不包含生产级流量保护、完整内容审核、账号恢复后台、弹性扩缩容或线上运营体系。
- LLM/ASR 未开通、微信未配置或公网主机未提供时，只能验证相应接口边界及本地功能，不能验证外部服务的实际效果。
