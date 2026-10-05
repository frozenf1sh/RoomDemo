# 房间：意义构成的世界

RoomDemo 使用 Unity **6000.6.3f1**、通用渲染管线（URP）与新版输入系统。
当前第七阶段围绕“画布与房间”展开两条完整路线。旁白要求完成画架上的作品；玩家也能把同一意义交给真实物体。连续服从得到一幅完整而静止的风景画；现实解释则让屋顶、墙与整个房间渐渐成为流动星夜中的森林和营地。

**观察世界 → 检视细节 → 收集观察 → 形成概念 → 解释现实 → 时间让解释继续生长 → 房间成为新的世界。**

项目自有文档、游戏文字、日志与 C# 文件头、XML 文档和关键行注释统一使用简体中文。
后续开发遵守 [项目规范](AGENTS.md)；稳定 ID、API、路径、机器报告字段和第三方许可原文保持已有格式。
中文字体随包嵌入，不依赖开发机系统字体。提交格式为 `feat(范围): 中文说明` 等。

## 启动与操作

打开 `Assets/_Game/Content/Scenes/DemoRoom.unity` 进入运行模式，或运行 `Builds/StandaloneOSX/RoomDemo.app`。
正式玩家模式默认隐藏世界标签、开发面板和调试快捷键。

- 房间：左键调查；按住右键拖动视角；Home 复位。位置固定，偏航 ±80°、俯仰 ±45°，没有移动控制。
- 检视：左键拖动旋转，滚轮缩放，左键点击细节；右键、Esc 或“返回房间”退出。书本可点击封面打开、页角翻页、上方“合上”关闭。
- 笔记本：N、Tab 打开，Esc 关闭。把边栏词条拖到中央纸面，重新摆放或拖回边栏；点击词条也可放入／移回。所有词统一列在左侧。两个或三个词靠近时都可以出现同样微弱的构图线和“联想／解释”按钮；只有提交后才判断是否成立，不靠发光扫描正确答案。
- 新发现有短暂中文通知，小型“笔记 · N”提醒闪烁至打开笔记本，平时隐藏。重复观察不重复通知，重开清除未读状态。
- Esc 打开暂停菜单；生长解释和海画解释会留下次日效果，此时可从菜单“结束今天”。黑屏与短暂日序分别进入第二天、第三天，日序不常驻。
- H 显示短暂提示；暂停菜单“重新开始”创建新会话并恢复建筑、家具、检视、演出、音频和 UI。正常画面没有标题、底部操作栏或开发状态文字。

## 画布与房间（第七阶段）

开场旁白：“展览还有三天。先完成那幅画吧。”形成生长后，它会要求把想法留给画布。没有选择对话框，选择发生在原有解释中。

- 画布路线：生长 + 画 → 结束第一天 → 书页远方 → 远方 + 画 → 亲点显露的草稿获得海 → 海 + 画 → 结束第二天 → 夜色 + 无边 → 星空 → 星空 + 画。四层颜料完成一幅普通风景，真实房间保持普通，最后安静淡出。
- 现实路线：生长 + 盆栽 → 次日繁茂与落书 → 远方 + 窗户 → 海 + 画外溢 → 第三天星空 + 天花板 → 亲点窗外与房间 → 森林 + 房间 → 森林进入、营火最后点燃。

三个核心解释连续交给画且从未偏离，才开始收束；有过偏离以后，后续画布解释不会突然锁死现实路线。“房间”不是开局材料，屋顶已打开且至少两次偏离后仍需亲点才能获得。
旁白的带停顿短句和世界解释字幕分开显示，HUD 不展示路线计数。

当前路线、配置和验收边界见 [第七阶段交付](Docs/Narrative/README.md)。下方保留既有现实路线的详细操作答案。

## 生长来自观察（含答案）

1. 点击盆栽，只认识 **盆栽**，进入近景检视。旋转到背面、拉近，在土边亲点 **新芽**。
2. 返回房间并亲点书。第一天即可检视它；打开封面、翻到短句页，亲点 **向光**。拿起或翻页不会自动授予短语。
3. 笔记本中的 **新芽 + 向光 → 生长**。纸面出现新概念的书写反馈；这是线索产出，没有世界事实。
4. **生长 + 盆栽** 成立后，当天只轻微舒展，并排定一次次日反应；书还没有落下。
5. 主动结束今天。第二天植物繁茂、书自行掉落，熟悉房间继续保留先前解释。
6. 次日检视书的第三页并亲点地平线可获得 **远方**。还需亲点窗户，才能建立 **远方 + 窗户**；旧解释引来风，吹出纸张。
7. 调查纸张得到 **海**，还需亲点画。**海 + 画** 继续播放画 → 滴水 → 水生植物 → 海窗 → 氛围的旧样片。

普通探索中物体线索必须亲点对应物体；描述中提到窗或画不等于已经认识它们。
发现规则不得授予其他来源的物体线索，细节和配方也不得直接产出物体线索。开发预设和明确“授予全部”是例外。

## 第三天：星空、森林与最后营火（含答案）

1. 第二天建立海画解释并结束今天。第三天屋内光线转为夜色。
2. 亲点天花板获得 **天花板、夜色**，重新亲点窗外获得 **无边**。
3. **夜色 + 无边 → 星空**，随后 **星空 + 天花板**。仅屋顶在约六秒中被星光侵蚀，墙、窗、地板全部保留。
4. 星空打开后再次亲点窗外获得 **树影**，点击脚下木地板认识 **房间** 并获得 **风声**。
5. **树影 + 风声 → 森林**，随后 **森林 + 房间**。墙、窗的边界退去，外景进入；约十二秒后才开始点燃营火，完整森林段约十八秒。
6. 家具仍留在原地，没有换关。营火是森林解释产生的自动反应，其事实立即成立、视觉在最后一拍展示；不需要第四天或调试按钮。

重要解释字幕只显示一行并自动淡出；H 提示同样是临时文字，玩家画面没有常驻任务栏。
主线入口、画布用法、内容扩展与验收边界见 [第六阶段交付](Docs/Mainline/README.md)。
第五阶段前后截图和三段实际构建录像保留在 [视觉重制交付](Docs/VisualSprint/README.md)。
第三方素材与字体许可见 [资源说明](Assets/ThirdParty/LICENSES.md)；本轮没有新增下载资产，继续使用已有许可清楚的资源。

## 规则与架构

依赖方向为 **Game.Domain → Game.Application → Game.Unity**；编辑工具单独依赖 UnityEditor。
领域层和应用层均设置 `noEngineReferences: true`，不引用 Unity API。GameState 的私有集合是唯一世界事实来源，外部只读查询。

正式内容入口是 `Assets/_Game/Runtime/Domain/Rules/NarrativeRules.cs`，复用历史 `MainlineRules` 并追加画布配方、共享次日探索和叙事门槛。`RuleEngine` 只处理通用倾向与条件，没有对具体旁白或天空 Shader 的判断。

| 有限配方 | 产出 |
| --- | --- |
| 新芽 + 向光 | 线索 `growth` |
| 生长 + 盆栽 | 事实 `plant.starts_growing` |
| 远方 + 窗户 | 事实 `window.far` |
| 海 + 画 | 事实 `painting.sea` |
| 夜色 + 无边 | 线索 `starry` |
| 星空 + 天花板 | 事实 `ceiling.starry` |
| 树影 + 风声 | 线索 `forest` |
| 森林 + 房间 | 事实 `world.forest` |

同一个 `SemanticRecipe` 支持 `ProducesClues`、`ProducesFacts` 或混合产出；没有两套规则引擎。
无序精确匹配两个或三个不同碎片，多余或重复输入不匹配。新增内容追加定义、配方、反应与表现，无需改 RuleEngine。
失败不消费线索、不记录关系、不产生事实。成功关系留在 `CompletedRecipes`。

延迟反应按明确日序排定，到期一次性执行，再运行普通闭包；循环和重复事实不会反复触发。
生长链为 `plant.starts_growing → 下一天 plant.overgrown → plant.grown → book.fallen`；海画还安排 `painting.sea → 下一天 night.arrived`。
日序属于领域；淡出、黑屏、声音和重新看见结果属于表现。

检视的有限状态位于应用层，可独立测试；近景复制体、页动画、热点角度和相机切换属于 Unity 适配层。
正式画布只按空间距离组织最多三个词条，不调用 `PreviewCombination` 预告答案；提交始终调用 `Combine`。三个词都靠近时，第三词不能被悄悄丢掉以命中二词配方。只读预览 API 保留给历史灰盒和开发工具。UI 不维护正确配方；Presenter 不写状态、不互相调用。
海画和星空 Sequence 只负责演出节拍；事实成立与视觉播放时间分离。
`WorldMutation` 支持显隐、替换、生成、移动、旋转、缩放、材质和主题；薄主题配置管理光照、雾、声音及整体风格参数。

`ApplyState(state, animate: false)` 与最终预设即时恢复，不重播故事。重开、切换预设会取消旧检视与演出、释放输入锁、销毁生成物。
交互对象保持逻辑与视觉分层，更换模型不改变 SceneEntity 或稳定 ID。

旧二元 `Interpret`、旧构造入口和 `DemoRules.Create()` 保留为回归兼容；正式场景使用三天主线规则。旧演出测试显式选择旧内容表，不改变新游戏体验。

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
没有加入自由移动、镜子、重量翻转、存档、真实物理翻书、流体模拟或自由语义系统。三天内容是有限主线切片，陌生玩家理解度与 10～15 分钟目标时长仍需实际试玩。

## 第六阶段原生主线走查

```sh
Builds/StandaloneOSX/RoomDemo.app/Contents/MacOS/RoomDemo \
  -roomDemoMainlineReview "$PWD/Logs/MainlineReviewNative" -roomDemoQuitAfterReview \
  -screen-fullscreen 0 -screen-width 1600 -screen-height 900 \
  -logFile "$PWD/Logs/player-mainline.log"
```

从新游戏经实际场景射线、检视热点、UGUI 拖动／按钮和暂停菜单走完主线，不使用预设、授予全部或 Inspector 改状态。只输出简短操作记录与几个实际画面；它是自动技术走查，不能代替陌生玩家真人试玩。

第七阶段绑定使用 `Game.Editor.NarrativeSceneSetup.Apply()`；绘画风格绑定使用 `Game.Editor.NarrativePainterlyPass.Apply()`。更新旁白或表现内容表后执行前者并保存，才能同步序列化内容。历史 `MainlineSceneSetup.Apply()` 可安装基础主线，但随后必须再执行叙事安装器。工具会拒绝运行模式和未保存的场景，不重建工作室美术。
