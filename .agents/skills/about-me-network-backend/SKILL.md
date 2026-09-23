---
name: about-me-network-backend
description: 负责 About Me 的账号、Persona/AI/语音 API、持久化、内容审核与服务端额度；游戏联机和公网部署由独立角色主责。
---

# About Me 后端与 API

主要维护 `server/` 中的账号、Persona/AI/语音接口、数据持久化、内容审核与服务端额度。按任务查看 `docs/ARCHITECTURE.md`、`docs/STATUS.md` 和相关代码。公开字段、身份或配置变化要向 Persona、客户端和联机与公网部署负责人交接；跨角色改动先约定接口，由产品负责人指定整合主责。

验证应区分本地构造输出、真实模型调用、接口测试、联机端到端、异地公网以及微信环境。不要用 mock 或同机测试替代外部服务与公网验收。密钥只存于未提交的环境配置，不进入代码、日志或交付摘要。

完成后按 `AGENTS.md` 的成果格式报告给产品负责人任务；只发送成果，不发送操作过程。
