# 本机 Persona API 启停

本机 macOS 游戏默认连接 `http://127.0.0.1:8081`。直接打开游戏 App 不会启动 API；若 8081 无监听，客户端请求在 HTTP 前就会连接失败。先运行本分支的 `scripts/start-local-ai-api.command`，或在终端执行 `python3 scripts/local-ai-api.py start`。启动器仅运行 API，不打开或关闭游戏窗口。

同时打开当前 DS 娘窗口版客户端和 API，可双击 `scripts/start-local-ds-windowed.command`。它先确认 QA 包存在、启动本分支 API，再以 1280×720 窗口打开独立的 `com.aboutme.party.integrationqa` 游戏；不会关闭已打开的旧版游戏。介绍页的“免费解锁 DS 娘”使用穿搭保存接口，不调用 Persona 生成模型。解锁成功后在衣橱点“切换为 DS 娘”并保存，才会在游戏里使用。

启动器从已存在的 Git worktree 中查找唯一未提交 `server/.env`，仅将密钥送入服务端进程环境；若找到多个配置，使用 `--env-file /absolute/path/to/server/.env` 指定。它把已有本机 SQLite 和校徽文件**首次复制**到本分支忽略的 `artifacts/local-debug/`，后续使用这个独立副本，不改原库。校徽目录由 `SCHOOL_EMBLEM_DIR` 指向同一持久位置。服务只监听 `127.0.0.1:8081`，日志在 `artifacts/local-debug/api.log`，不会自动上传、发布或改变 Railway 配置。

查看状态：`python3 scripts/local-ai-api.py status`。停止此启动器管理的 API：`python3 scripts/local-ai-api.py stop`。若端口由另一个 API 占用，启动器会拒绝覆盖它。重启本机后再次运行启动器；其数据库副本持续保留。

本机快速验收：`/health` 应返回 200，`aiConfigured=true`；真人游戏中的 Persona 生成还需检查实际请求状态。DS 娘纯“喜欢DeepSeek”解锁由 `POST /profiles/me/appearance` 的 `introduction` 与 `characterSkin` 免费完成，要求客户端构建含对应衣橱入口。校徽 Vision 仍按已有服务端流程工作；此启动器不额外调用模型。
