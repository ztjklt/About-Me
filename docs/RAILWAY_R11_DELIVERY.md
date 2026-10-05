# Railway 完整 R11 发布

主责：联机与后端；用户授权同步 GitHub 和 Railway。发布分支 `codex/r11-railway-release`。

基线：完整 R11 生产源码 `2cfebf4b9454ecd7571abd4e4ef699310af9da6f`，包含20件 Persona、16型MBTI独立机制、喷火每次30稳定度、277道具与96机制、协议5、动物行走、人机、设置、衣橱和双语。源码冻结与Git文件逐一匹配；既有Web Release资产复用并逐项核对R11交付hash，Linux Dedicated Server从本发布提交的相同客户端源码重新构建。

发布增量：Linux构建复用已冻结场景并生成Release；网页注入匹配的HTTPS API/WSS地址、显示R11与提交、为全部WebGL资源添加内容指纹，index/version禁止缓存；API健康接口提供发布ID/提交/协议；游戏启动日志提供发布身份。原有SQLite挂载卷和配置保留。

公网入口：https://web-production-fb9b1.up.railway.app/ 。GitHub仓库：https://github.com/ztjklt/About-Me 。构建产物不混入源码Git提交。

发布已完成（2026-10-06，新加坡时间）。R11既有本地验收见PERSONA_R11_DELIVERY；本次结果只按实际完成的公网检查记录，不代替真人、手机或微信验收。

## 最终发布身份与验收

- 生产发布提交：`44c5a7208eac3491284009447e1df0946b03c6aa`；GitHub `main` 与 `codex/r11-railway-release` 已推送。后续证据提交只修改文档，生产代码仍以上述提交及发布标签为准。
- Web客户端编译源码：R11 `2cfebf4b9454ecd7571abd4e4ef699310af9da6f`；发布提交的客户端运行代码与此相同，只调整Linux构建方法。生产Web的data/framework/loader/wasm全部SHA256均与R11冻结生产包相同；公开内容指纹 `dc6b71642461190f`。
- Linux Release tar：136文件，80,988,341字节；SHA256 `f2a527a98e7eea390d9dd1bb133d49b8f5fb0a344d34e94be8057d16aa3fc50d`。构建输出 `builds/linux` 与 `builds/linux.tar.gz`；Web `builds/web`；独立上传目录 `artifacts/railway-r11/stage/{api,game,web}`，不含密钥、数据库或其他角色未交付源码。
- Railway API部署 `3836a4c0-df10-49e2-bc15-0ca56e9c8405`、Game `9c772239-14c0-4acc-b13a-9fb75f4b23b5`、Web `a5195d45-87a6-4450-9dca-8a5fe3a41dd6` 均为SUCCESS。
- 503后端检查通过；Linux Dedicated Server Release构建成功并在Railway实际启动，启动日志声明R11/提交/协议5。保留原API SQLite卷、AI配置和服务间密钥；没有导入本地用户资料。
- 公网HTML含可见“最新版 R11 · 44c5a72”版本标记，`/version.json` 与 `/api/health`发布ID/提交匹配；健康接口核对协议5、20件、277目录、96机制、文字目录2。网页和版本JSON的Cache-Control含no-store，四个WebGL公开资源均按SHA256验证，全部匹配冻结包。`/api/internal/verify`被代理拒绝为404。
- 两个实际R11 Mac自动客户端从本机连接Railway公网WSS，通过入房、两人快照、物理字段/隐私检查、房主断线后同身份重连及后续Battle快照。使用现有样例资产，验证到比赛时间12秒，未完成整局；这是单一外部网络到公网服务的自动测试，不是两地真人、20件真实生成对局或微信验收。
- 验收证据：`artifacts/railway-r11/public-checks.json`、`deployments.json`、`network-smoke/result.json`；截图/浏览器交互未新增验收（内嵌浏览器导航工具超时），不把HTML返回与资源校验称为浏览器实操。
- 未解决范围：真人趣味性、手机/微信和两地多人仍由玩法/测试角色后续验收；真实AI已配置，本次没有新增计费生成调用。旧文档中的未推送/未部署描述属于历史基线，当前公网发布以本记录为准。
