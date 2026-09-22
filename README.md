# About Me

团结引擎 2022.3.62t11 / 3D Persona 派对对战原型。

这是可以编译运行的开发工程。**样例测试、真实 AI 生成、本机联机验证、异地联机验证是不同状态，请勿混用。** 验证结果与尚未完成的工作见 [开发状态](docs/STATUS.md)。

## 玩具贴纸派对 UI

完整流程使用统一的奶油纸张、深蓝粗描边、彩色贴纸、软阴影和轻量弹性动效。首页、连接设置、六人房、战斗 HUD、Persona 背包、横屏触控、二维码和结算均使用同一主题；所有形状与图标由小型运行时图集生成，不依赖第三方 UI 或动画插件。

界面以 1600×900 为桌面基准，通过安全区画布适配 16:9、19.5:9 和 4:3 横屏。横屏手机隐藏键盘提示，并启用独立的摇杆和分组动作按钮。竖屏不在当前范围内，微信真机安全区仍需取得 AppID 和设备后复验。

开发构建可以直接生成确定性的界面截图：

```sh
'builds/mac/About Me.app/Contents/MacOS/About Me' \
  --ui-preview home --capture-size 1600x900 \
  --capture "$PWD/artifacts/ui-home.png"
```

`--ui-preview` 支持 `home`、`settings`、`lobby`、`qr`、`hud`、`bag`、`touch` 和 `results`。预览数据明确标注为 UI 预览，不连接服务器，也不写入正式结算。

## 打开和运行

**本机双客户端体验（已有构建）：**双击 `scripts/start-local.command`。脚本启动本机游戏服务、API 和网页服务，并打开 macOS 游戏与浏览器。两边各点击「样例测试」，一边留空房间码创建，另一边填该房间码加入；各自 Ready 后由房主开始。保留终端，结束后按 Ctrl+C 停止服务。此流程不需要 AI 账号，使用明确标注的测试资产。

首页会直接显示“真实 AI 已连接 / 未配置 / 服务不可达”。“AI 生成 Persona”只调用真实模型；未配置 `server/.env` 时不会用固定内容冒充生成结果。此时请点击“本地样例（无需 AI）”测试房间和战斗。macOS、WebGL 和微信输入框均接入中文组合输入；网页首次点击输入框后由浏览器原生输入控件接管键盘。

构建产物在 `builds/mac/About Me.app`、`builds/web` 和 `builds/linux`；它们不提交到 Git，换电脑后按下方命令重建。浏览器版本需通过 HTTP 打开，不能直接双击 HTML。

**引擎中开发：**

1. 在团结 Hub 中添加本仓库的 `client` 目录，使用 **2022.3.62t11**。
2. 打开 `Assets/AboutMe/Scenes/Bootstrap.unity`，点击 Play。也可用菜单 `About Me / 1. Generate project scenes` 重建场景。
3. 无 AI 账号时，明确点击「样例测试」。打开「连接设置」，点击「本机测试服务器」，地址保留 `ws://127.0.0.1:7777`。
4. 房间码留空创建。第二个独立客户端填相同地址和房间码。每人 Ready 后，由房主开始。

同机体验建议使用一个 macOS 客户端加一个浏览器，它们各自保存身份。重复打开两个 macOS 窗口或同浏览器的两个普通标签，会共享保存的凭据并被拒绝重复登录。自动多客户端测试脚本为每位测试者生成独立身份。

## 控制

| 操作 | 键位 |
|---|---|
| 移动 / 视角 / 跳跃 | WASD / 按住鼠标右键拖动 / Space |
| 轻击 / 蓄力重击 | J / 按住 K，松开释放 |
| 抓人 / 抓对手的道具 | G / H |
| 拾取并吞噬或回收 / 取消 | E / Q |
| 快捷栏 / 完整背包 | 1–5 / B |
| 抓起公共道具 | 靠近椅子、纸箱或垃圾桶按 G，K 重击扔出 |
| 观战切换 | Tab |

手机端有共用操作协议的触控控件，真机手感仍需测试。吞噬和回收期间角色停步，受到攻击或抓取会中断。背包中可将任意 Persona 放入快捷栏。

## AI 和存储服务

```sh
python3 -m venv .venv
.venv/bin/pip install -r server/requirements.txt
cp server/.env.example server/.env
# 编辑 server/.env，设置模型服务及 GAME_SERVER_KEY；不要提交密钥。
sh scripts/start-api.sh
```

服务默认 `http://127.0.0.1:8081`，接口文档为 `/docs`。聊天服务须支持 `/chat/completions` 的 JSON 对象输出；语音服务须支持 multipart `/audio/transcriptions`。自我介绍只作为模型输入数据，不允许生成任意可执行逻辑。未配置服务时返回明确错误，不自动伪造 AI 输出。

生产服务器设置 `ABOUTME_API_URL` 和 `GAME_SERVER_KEY`；客户端不能携带服务器密钥。桌面原型使用设备登录令牌，微信登录接口支持绑定微信身份。原始介绍保存在 SQLite，不输出到应用日志。

## 构建与测试

```sh
sh scripts/editor.sh TestRules
.venv/bin/python -m pytest server/tests -q
sh scripts/editor.sh BuildMac
python3 scripts/test-network.py --players 2
python3 scripts/test-network.py --players 4
python3 scripts/test-network.py --players 6
sh scripts/editor.sh BuildWeb
sh scripts/editor.sh BuildServer
```

游戏服务器启动参数是 **`--aboutme-server`**，不要用引擎占用的 `--server`。

```sh
'builds/mac/About Me.app/Contents/MacOS/About Me' -batchmode -nographics --aboutme-server --allow-samples -logFile artifacts/server.log
```

`--allow-samples` 仅用于明确标注的测试；`--accelerated` 仅用于自动验收，压缩时间到 2 秒介绍、4 秒大手、8 秒终局。普通对局仍是每 5 分钟大手、15 分钟终局。公网部署见 [部署文档](docs/DEPLOYMENT.md)。

## 项目组织

- `client/Assets/AboutMe/Runtime`：权威规则、网络、角色表现、UI 和平台桥接。
- `client/Assets/AboutMe/Editor`：场景生成、规则测试、构建命令。
- `server`：账号、AI、语音、微信接口和 SQLite。
- `shared/catalog.json`：允许生成的模板与外观目录；与客户端 Resources 副本保持一致。
- `deploy`：Linux 游戏服务与 API 容器、HTTPS/WSS 反向代理。
- `docs`：PRD、接口、协作和验收状态。

Mirror 固定为 `v96.9.23 / 1aa5a33c24a81d1999c33367952e39694ff525b3`，源码与许可证在 `client/Assets/Mirror`。中文字体使用 Noto Sans CJK SC，许可证位于字体同目录 `OFL.txt`。
