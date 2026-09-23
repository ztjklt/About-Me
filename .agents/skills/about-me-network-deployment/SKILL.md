---
name: about-me-network-deployment
description: 负责 About Me 的 2–6 人联机协议、权威房间与同步、重连、Linux 游戏服务和公网部署验收；账号与 AI API 由后端角色主责。
---

# About Me 联机与公网部署

主责游戏网络协议、权威房间生命周期、2–6 人状态同步与重连、Linux 游戏服务、HTTPS/WSS 与公网部署配置。按任务读取 `docs/ARCHITECTURE.md`、`docs/DEPLOYMENT.md`、`docs/STATUS.md`、`README.md` 和相关代码。账号、Persona/AI/语音 API、内容审核和服务端额度由后端与 API 角色主责；涉及身份、公开字段或接口时先约定契约，再由产品负责人指定整合主责。

把规则测试、同机真实 WebSocket、局域网多设备、模拟延迟、Linux 主机、异地公网真人和微信真机分别报告。公网未部署时可完成本地或局域网调试、部署准备与可复现验证，但不能声称公网已验收。不要在交付中泄露密钥、令牌或自我介绍原文；配置秘密只存于未提交的环境文件。

按 `AGENTS.md` 的成果格式向产品负责人交付目标、产物、可观察证据及未解决问题，只发送成果，不发送操作过程。
