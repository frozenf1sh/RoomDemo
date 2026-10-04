# 房间：意义构成的世界

RoomDemo 使用 Unity **6000.6.3f1**、通用渲染管线（URP）与新版输入系统。
当前第四阶段只制作两个案例：**深入检视、联想形成生长、次日发现后果**，以及独立的**星空世界重构样板**。海画涌现样片继续保留。

**观察世界 → 检视细节 → 收集观察 → 形成概念 → 解释现实 → 时间让解释继续生长 → 房间成为新的世界。**

项目自有文档、游戏文字、日志与 C# 文件头、XML 文档和关键行注释统一使用简体中文。
后续开发遵守 [项目规范](AGENTS.md)；稳定 ID、API、路径、机器报告字段和第三方许可原文保持已有格式。
中文字体随包嵌入，不依赖开发机系统字体。提交格式为 `feat(范围): 中文说明` 等。

## 启动与操作

打开 `Assets/_Game/Content/Scenes/DemoRoom.unity` 进入运行模式，或运行 `Builds/StandaloneOSX/RoomDemo.app`。
正式玩家模式默认隐藏世界标签、开发面板和调试快捷键。

- 房间：左键调查；按住右键拖动视角；Home 复位。位置固定，偏航 ±80°、俯仰 ±45°，没有移动控制。
- 检视：左键拖动旋转，滚轮缩放，左键点击细节；右键、Esc 或“返回房间”退出。书本可点击封面打开、页角翻页、上方“合上”关闭。
- 笔记本：N、Tab 或按钮打开；Esc 关闭。点击碎片填入最多三个组合槽，再点击“建立解释”；点击已选碎片或槽位可取消。
- 新发现有明显中文通知；笔记本按钮闪烁至打开笔记本。重复观察不重复通知，重开清除未读状态。
- 完成生长解释后可点击“结束今天”。黑屏过渡后进入第二天；没有真实时钟、能量或失败期限。
- H 显示提示；“重新开始”创建新会话并恢复建筑、家具、检视、演出、音频和 UI。

## 案例 A：生长来自观察（含答案）

1. 点击盆栽，只认识 **盆栽**，进入近景检视。旋转到背面、拉近，在土边亲点 **新芽**。
2. 返回房间并亲点书。第一天即可检视它；打开封面、翻到短句页，亲点 **向光**。拿起或翻页不会自动授予短语。
3. 笔记本中的 **新芽 + 向光 → 生长**。纸面出现新概念的书写反馈；这是线索产出，没有世界事实。
4. **生长 + 盆栽** 成立后，当天只轻微舒展，并排定一次次日反应；书还没有落下。
5. 主动结束今天。第二天植物繁茂、书自行掉落，熟悉房间继续保留先前解释。
6. 次日检视书的第三页并亲点地平线可获得 **远方**。还需亲点窗户，才能建立 **远方 + 窗户**；旧解释引来风，吹出纸张。
7. 调查纸张得到 **海**，还需亲点画。**海 + 画** 继续播放画 → 滴水 → 水生植物 → 海窗 → 氛围的旧样片。

普通探索中物体线索必须亲点对应物体；描述中提到窗或画不等于已经认识它们。
发现规则不得授予其他来源的物体线索，细节和配方也不得直接产出物体线索。开发预设和明确“授予全部”是例外。

## 案例 B：星空世界重构

这是独立开发样板，没有增加主流程谜题。切换到开发模式后，在 F7 面板点击“跳到星空之前”，再点击“提交星空事实并播放”。

约 15 秒内依次呈现：天花板退去、完整星空显现、后墙消失、侧墙退让、窗框边界打开、地板之外的草地与树石出现、运行时生成一个篝火、光照与环境声音过渡。
桌子、椅子、画架和书架仍留在原位置；没有加载另一个关卡。
“仅预览星空演出”不修改领域状态；“恢复当前真实状态”依据事实恢复，不把预览当成世界真相。

[第四阶段实施与验收](Docs/Exploration/第四阶段开发与验收.md) 记录画面、技术复测、性能和人工验收边界。
第三方模型与中文字体见 [资源说明](Assets/ThirdParty/LICENSES.md)。家具和自然资源采用小范围 Kenney CC0 网格，使用统一材质及 Wrapper Prefab；近景书本、星空、草地、火焰与临时音频由项目生成。

## 规则与架构

依赖方向为 **Game.Domain → Game.Application → Game.Unity**；编辑工具单独依赖 UnityEditor。
领域层和应用层均设置 `noEngineReferences: true`，不引用 Unity API。GameState 的私有集合是唯一世界事实来源，外部只读查询。

正常内容表集中在 `Runtime/Domain/Rules/ExplorationRules.cs`。它复用旧 `DemoRules` 的海画内容，并增加两个观察、一个灵感配方及跨日反应。

| 有限配方 | 产出 |
| --- | --- |
| 新芽 + 向光 | 线索 `growth` |
| 生长 + 盆栽 | 事实 `plant.starts_growing` |
| 远方 + 窗户 | 事实 `window.far` |
| 海 + 画 | 事实 `painting.sea` |

同一个 `SemanticRecipe` 支持 `ProducesClues`、`ProducesFacts` 或混合产出；没有两套规则引擎。
无序精确匹配两个或三个不同碎片，多余或重复输入不匹配。新增内容追加定义、配方、反应与表现，无需改 RuleEngine。
失败不消费线索、不记录关系、不产生事实。成功关系留在 `CompletedRecipes`。

延迟反应按明确日序排定，到期一次性执行，再运行普通闭包；循环和重复事实不会反复触发。
本案例为 `plant.starts_growing → 下一天 plant.overgrown → plant.grown → book.fallen`。
日序属于领域；淡出、黑屏、声音和重新看见结果属于表现。

检视的有限状态位于应用层，可独立测试；近景复制体、页动画、热点角度和相机切换属于 Unity 适配层。
UI 调用 `Combine`，不拥有正确配方；Presenter 不写状态、不互相调用。
海画和星空 Sequence 只负责演出节拍；事实成立与视觉播放时间分离。
`WorldMutation` 支持显隐、替换、生成、移动、旋转、缩放、材质和主题；薄主题配置管理光照、雾、声音及整体风格参数。

`ApplyState(state, animate: false)` 与最终预设即时恢复，不重播故事。重开、切换预设会取消旧检视与演出、释放输入锁、销毁生成物。
交互对象保持逻辑与视觉分层，更换模型不改变 SceneEntity 或稳定 ID。

旧二元 `Interpret`、旧构造入口和 `DemoRules.Create()` 保留为回归兼容；正式场景使用新探索规则。旧演出测试显式选择旧内容表，不改变新游戏体验。

## 开发模式与预设

默认关闭开发功能。原生启动添加 `-roomDemoDevelopment`；Editor 可勾选 GameBootstrap 的开发模式。

- F3：事实、已知碎片、已完成配方、触发反应、重置、全部授予、添加已声明事实及九个预设。
- F4：旧海画单节拍、完整预览、终态和恢复；F6 跳到海画前；F8 调整取样窗口。
- F7：星空事实触发、只读预览、重置、恢复、前置／终态，以及直接检视盆栽或书。
- F9／F10：旧海画技术复测／截图。它们只在显式开发模式启用。

新增预设为 `Start`、`BeforeGrowthInsight`、`GrowthUnlocked`、`BeforeDayAdvance`、`AfterGrowthDay`、`BeforeStarryWorld`、`StarryWorldFinal`。
旧 `BeforeSea` 与 `Emergence` 也可使用，并按第二天状态恢复前置生长。
未知事实拼写返回中文错误；调试加事实不伪造玩家配方历史。

## CLI 验证与构建

```sh
python3 Tools/pipeline.py                      # 验证已有场景并构建
python3 Tools/pipeline.py --domain-only        # 仅编译与编辑模式测试
python3 Tools/pipeline.py --regenerate-scene   # 只生成独立灰盒参考
```

**编译 → 编辑模式测试 → 场景验证 → 运行模式测试 → 构建**，每步失败立即停止，空测试套件也失败。
已连接 Editor 时复用，否则调用官方批处理。关闭本项目 Editor 后可用 `--headless`；`--editor /absolute/path/to/Unity` 指定安装，不同时启动第二个 Editor。

场景验证检查核心 ID、引用、检视 Prefab、全部关键 Presenter、日序、建筑模块、世界导演、音频、中文字体及 Shader；不接受未保存房间。
默认不覆盖人工美术。`BuildGreyboxScene()` 只写 DemoGreybox 和独立灰盒目录；`Build()` 拒绝覆盖已有 DemoRoom。
`CreateMissingTechnicalObjects()` 增量补缺失技术对象。场景、Prefab 和导入配置通过 Editor API 管理。

```sh
unity command eval 'Game.Editor.DemoSceneValidator.ValidateScene();' --project-path "$PWD" --format json
unity command eval 'Game.Editor.PresentationArtPass.CreateMissingTechnicalObjects();' --project-path "$PWD" --format json
unity command eval 'Game.Editor.CliBuild.QueueBuild();' --project-path "$PWD" --format json
```

独立批处理用 `-executeMethod` 调用对应方法。测试用 `-runTests -testPlatform EditMode/PlayMode -testResults <XML>`，测试时不加 `-quit`。
报告位于 `Logs/Pipeline/`、`Logs/build-summary.json` 和 `Logs/verification-summary.json`。
默认输出 `Builds/StandaloneOSX/RoomDemo.app`，`ROOM_DEMO_BUILD_OUTPUT` 可覆盖；其他桌面平台需要对应模块。
分发时一起保留输出目录的字体许可、模型许可原文与中文来源清单。

## 原生技术走查

```sh
Builds/StandaloneOSX/RoomDemo.app/Contents/MacOS/RoomDemo \
  -roomDemoExplorationCapture -roomDemoQuitAfterReview \
  -screen-fullscreen 0 -screen-width 1440 -screen-height 900 \
  -logFile Logs/player-exploration-capture.log
# 将 Capture 改为 -roomDemoExplorationReview，运行案例 A 与十轮无截图星空复测。
```

输出在 Unity 用户数据目录的 `ExplorationReview`：真实 PNG、每秒最多八帧的星空取样、功能断言和原始帧间隔。
案例 A 经实际房间射线和近景热点，不使用跳关预设；星空是明确开发预设例外。
自动技术走查不等于真人鼠标操作。截图模式包含编码开销，不能当成无截图性能；视频无音轨，不能验收音效听感。

海画历史样片见 [第三阶段评审](Docs/Benchmark/演出样片验收.md)。陌生玩家首次理解度、时长、声音和情绪仍待 [真人试玩](Docs/PLAYTEST.md)。
没有加入自由移动、镜子、重量翻转、存档、完整多日剧情、真实物理翻书、流体模拟或自由语义系统。
