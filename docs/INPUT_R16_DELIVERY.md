# 完整 R16：学校生成与网页直接输入修复

本次修复保留完整 R15 的两击近战、高伤害投掷/重击/攻击性接触场地及 R14 嘴型短梗。AI 次数和频率限制按用户要求继续关闭。

## 修复与交付身份

- 学校生成 HTTP500 的根因是 API 非 root 用户无法写入默认 `artifacts/university-crests`。Railway 改用 `/data/university-crests`，Docker 默认也使用该持久目录；异常缓存路径返回受控的 503 提示。
- Unity/Tuanjie 的捕获阶段键盘事件先于 HTML textarea 执行，阻止正常空格与回车。HTML 输入框打开时让出引擎键盘事件，关闭时恢复；单行输入中用于中文候选确认的 Enter 不提前结束编辑。
- 公网入口：https://web-production-fb9b1.up.railway.app/ ；右下角显示最新版 R16。
- 公网 Mac/Web/Linux/API 实际构建源码：`3203d1e0ad9a82417ed172939b26bf427db0f6b7`；Web 指纹 `224c7f879c527890`；Linux归档 SHA256 `be67d72418016e0748cb9685139a671405140acc0cefd26c8755bac7a597c560`。
- 本机封装源码：`59875f0a7c132933d59cde0bcf4cf914adab5fdc`，源码快照 `6c47a0a3802fb9f6593a78de268fc88849d48cfd61ee74b458001aa5db6f138f`。与公网构建相比仅调整本机入口、模板和封装端口，`client/Assets/AboutMe`、`client/Assets/Plugins`、`server`、`shared` 完全相同。
- Mac：`builds/playtest-input-r16-mac/About Me.app`；Web：`builds/playtest-input-r16-web`；Linux：`builds/playtest-input-r16-linux`；API：`builds/playtest-input-r16-service`。
- 最新入口：`scripts/start-latest.command` → `scripts/start-playtest-input-r16.command`，本机 API/权威使用独立 8136/7836；原端口已占用的其他角色服务保留。

## 验证范围

538 后端回归通过，含校徽缓存路径不可写时受控错误。Mac/Web/Linux Release 构建及 Mac 签名校验通过。本机全包文件哈希、277道具/96机制/16MBTI/20 Persona 和继承功能门槛通过；实际 API 与 Mac 权威进程路径、监听端口及发布身份通过。验证启动的临时进程已清理。

真实公网学校分支生成取得北京大学 Persona，确认校徽 ready、图片 HTTP200、缓存可以由 uid10001 写入。没有使用 mock 替代线上模型或合成校徽。

实际 Chrome WebGL 本机页面通过空格、Enter 换行、光标中间插入、中文 Unicode 输入和退出/重开内容一致；单行空格、候选确认 Enter 不关闭和关闭后引擎键盘监听恢复也通过。中文检查为 Unicode 插入和组合事件守卫，不冒充真实 OS 输入法或微信真机验收。

Railway 三服务已部署 SUCCESS。最终公网资源/版本、双客户端 WSS 与浏览器输入复验见 `artifacts/railway-r16/public-checks.json`、`artifacts/input-r16/browser-checks.json`、`artifacts/input-r16/school-public-checks.json`。这些是实际桌面浏览器与单一来源网络的自动检查，不代表真人平衡、两地网络或微信真机验收。
