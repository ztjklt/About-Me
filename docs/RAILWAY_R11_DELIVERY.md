# Railway 完整 R11 发布

主责：联机与后端；用户授权同步 GitHub 和 Railway。发布分支 `codex/r11-railway-release`。

基线：完整 R11 生产源码 `2cfebf4b9454ecd7571abd4e4ef699310af9da6f`，包含20件 Persona、16型MBTI独立机制、喷火每次30稳定度、277道具与96机制、协议5、动物行走、人机、设置、衣橱和双语。源码冻结与Git文件逐一匹配；既有Web Release资产复用并逐项核对R11交付hash，Linux Dedicated Server从本发布提交的相同客户端源码重新构建。

发布增量：Linux构建复用已冻结场景并生成Release；网页注入匹配的HTTPS API/WSS地址、显示R11与提交、为全部WebGL资源添加内容指纹，index/version禁止缓存；API健康接口提供发布ID/提交/协议；游戏启动日志提供发布身份。原有SQLite挂载卷和配置保留。

公网入口：https://web-production-fb9b1.up.railway.app/ 。GitHub仓库：https://github.com/ztjklt/About-Me 。构建产物不混入源码Git提交。

发布提交、资源hash、部署ID与实测结果在部署完成后追加。R11既有本地验收见PERSONA_R11_DELIVERY；本次结果只按实际完成的公网检查记录，不代替真人、手机或微信验收。
