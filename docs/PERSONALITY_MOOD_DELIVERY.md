# 国内大学进度与性格试验台交付

完成状态：国内大学优先及可见阶段进度已进入最新版 Persona 生成流程；六种代码特效已进入原生客户端试验台。性格、心情与 MBTI 的正式对战规则已形成可评审策划案，尚未接入伤害、冷却或联机判定。

## 交付物

- `docs/PERSONALITY_MOOD_DESIGN.md`：三层系统、六种能力、五种心情、十六种 MBTI 风格预设、代价及反制、获取与 Persona 生命周期、权威接口和分期验收。
- `scripts/start-university-crests.command`：最新版大学生成入口，1280×720 窗口；国内学校先生成，海外或地区未知的学校可见排队状态。
- `scripts/start-personality-lab.command`：同版客户端试验台，六种主题按钮、F／释放一次、自动演示及 MBTI 推荐切换；返回现有菜单。
- `builds/university-crests-mac`、`builds/university-crests-web`、`builds/university-crests-service`：匹配的客户端及服务包。

实现分支：`codex/persona-university-crests-latest-ui`。源代码已同步到 `About Me DS Local Current`；旧项目通过同版交付包运行，保留其已有开发改动。具体提交与文件摘要见 `artifacts/university-crests-release.json`。

## 可观察结果

学校进度反映服务端实际阶段：排队 0%，启动 5%，检索来源 12%，定位图片 30%，下载 45%，DeepSeek 核对 65%，制作 85%，写入校验缓存 95%，成功 100%。进度不是时间预测；候选更换不倒退。正在处理为灰色，成功整块绿色且满格；失败整块红色，保留失败阶段进度，并显示原因和重试提示。重复生成仅补失败项。整体学校队列默认 100 秒截止，防止国内和海外依次处理超出客户端请求期限；已经成功的结果保留。

国内优先使用 DeepSeek 返回且经过格式验证的地区代码 CN／HK／MO／TW，兼容已有国内校名种子；不根据中文名称把哈佛等海外学校误归国内。未知地区继续检索，排在国内批次之后。

试验台中的喷火使用程序纹理与真实 Unity 粒子系统；六种主题复用同一受限表现接口。当前单次预算最多 240 粒子，没有粒子碰撞或动态灯。MBTI 切换给出创作推荐，玩家仍能自由切换主题。当前视觉预览不扣韧性、不改变背包或生命。

## 验收证据及范围

- 本轮服务源码与交付服务包各 85 项测试通过。新增检查覆盖海外在前、国内在后的优先顺序，排队和阶段进度，65% 失败保留，地区字段合并，以及全队列超时保留已成功项目。
- Unity 15 项菜单、31 项规则及校徽贴图、全红缓存检查通过；代码特效检查验证真实粒子、240 上限、无碰撞和火焰到泡泡切换。Mac／WebGL 构建成功。
- `artifacts/persona-progress-live/report.json`：真实 DeepSeek 提取、两所学校的已验证图片缓存、资料保存和内部复核；本次约 2.15 秒、重复约 0.01 秒，两校进度 100%。本次使用缓存，**不作为真实冷搜索耗时证据**。
- `artifacts/personality-lab/fire-native.png`：最新版 Mac 原生喷火窗口截图；`university-progress-states.png` 是明确标注的阶段布局示例，展示排队、45% 处理中、65% 失败、100% 成功；`university-progress-saved.png` 展示实际保存资料的结果。

本轮为自动检查、本机 API 和原生窗口验收；未证明正式对战伤害、六人特效性能或微信真机性能。正式能力的服务器权威判定和真人平衡验证按策划案分期接入。
