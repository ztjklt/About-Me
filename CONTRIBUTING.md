# Contributing

感谢关注 About Me。请先阅读 [README](README.md) 和 [开发指南](docs/DEVELOPMENT.md)，在 issue 中描述具体的复现步骤、预期结果和运行平台；截图请去掉账号、介绍原文和凭据。

修改基于 `main` 创建功能分支，通过 pull request 提交。客户端、权威服务和共享目录的协议改动应一起说明；不要把其他工作区未完成的内容混入提交。

后端修改运行：

```sh
python3 -m venv .venv
.venv/bin/pip install -r server/requirements.txt
.venv/bin/python -m pytest server/tests -q
```

团结项目的规则和构建入口见 [开发指南](docs/DEVELOPMENT.md)。说明验证使用的是固定样例、mock、实际模型、本机自动联机还是真人测试；它们覆盖不同的范围。

提交前检查 `git diff --cached --stat`，运行 [密钥扫描](SECURITY.md)，并保留新素材的来源与许可。构建包、环境文件、账号数据库、模型服务凭据和批量过程日志不提交到仓库。
