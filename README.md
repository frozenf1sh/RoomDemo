# RoomDemo — The room remembers

Unity **6000.6.3f1** / URP / Input System。第二阶段灰盒 Vertical Slice：
观察 → 收集 Clue → Notebook 组合 → Fact → 自动 Reaction → 新发现 → 旧解释交叉。

## 启动与操作

打开 `Assets/_Game/Content/Scenes/DemoRoom.unity` 进入 Play Mode，或运行
`Builds/StandaloneOSX/RoomDemo.app`。

- 左键调查；按住右键拖动视角。观察点固定，Yaw ±80°、Pitch ±45°。
- **N / Tab** 或按钮开关 Notebook，**Esc** 关闭，**Home** 复位视角。
- 点击碎片填入最多三个组合槽；点击槽或同一碎片可清除选择。
- **INTERPRET** 提交给 GameSession；成功短暂强调组合、自动收起 Notebook。
- **H** 显示/隐藏当前提示；**RESTART** 创建全新 Session 并立即恢复初始表现。
- **F3** 打开 Domain Debug。状态区可滚动，输入区支持 Add Fact。

## 完整流程（含答案）

1. 调查 Plant，得到 **Plant + Growth**。组合它们：植物长大、枝叶伸向书架，书自动落下。
2. 调查地上的红色 Book，得到 **Book + Far + Window**。书在书架上可见，但尚不可调查。
3. 组合 **Far + Window**：模糊窗景变为天空、远山和原野。
4. 旧的植物生长和新的远方窗景自动触发风：植物摇摆、纸张飘动、风声开始。
   一张原本卡在画框后的纸滑到地毯上。
5. 调查这张 Paper，得到 **Sea + Painting**。也可提前调查 Painting；重复线索不会再次授予。
6. 组合 **Sea + Painting**：静态画变为动态海面、海浪声开始、画框滴水、地面出现局部水渍。
7. 海水自动联系之前的解释：植物变为湿润的青绿色水生形态，窗外原野变为海平线。
   只提交了 Painting 的解释，没有再次提交 Plant 或 Window。
8. 环境光与声音改变，显示 **The room remembers.**。这是临时切片结束提示。

当前英文 UI 避免引入字体和本地化系统。声音是本地生成的占位音效；场景全部为原创简单几何，
未下载外部素材。目标首次试玩时间为 8～12 分钟，**尚未由不知答案的真人验证**。
已知答案的鼠标/键盘走查不能替代这项验收，见 `Docs/PLAYTEST.md`。

## CLI 流水线

```sh
python3 Tools/pipeline.py                      # 验证现有 Scene，不自动覆盖人工调整
python3 Tools/pipeline.py --domain-only        # Compile + EditMode，完全不读/生成 Scene
python3 Tools/pipeline.py --regenerate-scene   # 明确重建灰盒及生成的 Prefab/Material/Audio
```

连接当前项目的 Editor 时复用它，否则使用安装的 Editor 官方 batch CLI：

**Compile → EditMode Tests → 可选灰盒生成 → Scene Validation → PlayMode Tests → Build**。

每步失败立即停止；空测试套件也算失败。默认不会保存或重建人工场景。
Validator 拒绝未保存的 DemoRoom、缺少组件、重复 ID、遗漏 Presenter 绑定和缺失资源。
显式 `--regenerate-scene` 会覆盖生成器管理的 DemoRoom、Prefab、材料和音频；
生成器拒绝覆盖当前未保存的场景。人工美术版应另存为其他 Scene 或只运行验证。

关闭本项目的 Editor 后可运行：

```sh
python3 Tools/pipeline.py --headless
```

可选 `--editor /absolute/path/to/Unity` 覆盖安装路径。不要启动第二个 Editor 打开同一项目。
日志/测试报告在 `Logs/Pipeline/`，构建结果在 `Logs/build-summary.json`。
`ROOM_DEMO_BUILD_OUTPUT` 可指定输出；macOS 默认 `Builds/StandaloneOSX/RoomDemo.app`。
支持 macOS、Windows64、Linux64，但另外两个平台需要先安装其构建模块。

独立 Editor 入口：

```sh
unity command eval 'Game.Editor.DemoSceneBuilder.BuildGreyboxScene();' --project-path "$PWD" --format json
unity command eval 'Game.Editor.DemoSceneValidator.ValidateScene();' --project-path "$PWD" --format json
unity command eval 'Game.Editor.CliBuild.QueueBuild();' --project-path "$PWD" --format json
# QueueBuild 返回后轮询 Logs/build-summary.json，直到 Succeeded 或 Failed。
```

离线 Editor CLI 使用 `-executeMethod Game.Editor.DemoSceneBuilder.BuildGreyboxScene`、
`Game.Editor.DemoSceneValidator.ValidateScene` 和 `Game.Editor.CliBuild.BuildDemo`。
测试使用 `-runTests -testPlatform EditMode/PlayMode -testResults <XML>`，测试时不要传 `-quit`。
直接 BuildDemo 也会执行 Validator。桌面输出是 Release Build，无运行时 Pipeline 服务。

## 规则与扩展内容

`Assets/_Game/Runtime/Domain/Rules/DemoRules.cs` 集中定义 **7 Clue / 5 Discovery / 3 Recipe / 8 Reaction**。

| 玩家 Recipe | 产生 Fact |
| --- | --- |
| plant + growth | plant.grown |
| window + far | window.far |
| painting + sea | painting.sea |

| 自动 Reaction | Requires | Produces |
| --- | --- | --- |
| plant-knocks-book | plant.grown | book.fallen |
| book-becomes-inspectable | book.fallen | book.inspectable |
| wind-enters-room | plant.grown + window.far | wind.entering |
| wind-reveals-paper | wind.entering | paper.revealed |
| painting-leaks-water | painting.sea | water.present |
| plant-becomes-aquatic | water.present + plant.grown | plant.aquatic |
| window-becomes-ocean | water.present + window.far | window.ocean |
| room-remembers | plant.aquatic + window.ocean + painting.sea | vertical_slice.complete |

新增内容只需要 ClueDefinition、DiscoveryRule、SemanticRecipe、ReactionRule 数据，
以及对应的新 Visual/Presenter。**RuleEngine 没有 Plant/Sea/Window 内容特判**。
Recipes 使用无序精确匹配，支持两个或三个不同 Clue；多余/重复输入不会匹配。
相同输入不能配置两个 Recipe。Modifier/Phrase 分类已存在，测试用镜子三元配方未进入正式游戏。
旧 ObjectId/ConceptId/Interpret 和规则构造入口仍作为兼容 adapter 存在；Notebook 主入口是 KnownClues/Combine。

GameState 私有集合是唯一事实来源，外部得到只读快照。成功记录 CompletedRecipes；
失败不消费线索、不写历史、不产生 Fact。Reaction 先标记已触发，再添加去重 Fact，扫描直到稳定。
规则定义顺序不影响最终闭包，循环也不会重复触发。

## 架构与恢复

- **Game.Domain**：ID、Clue、数据定义、Commands/Events、GameState、RuleEngine。
- **Game.Application**：GameSession 执行并发布事件；提供检查、组合和明确的 Debug 操作。
- **Game.Unity**：输入、Notebook、Debug UI、固定相机、八个 Presenter。
- **Game.Editor**：SceneBuilder、SceneValidator、Build。

Domain/Application 均 `noEngineReferences: true`，源码无 UnityEngine/UnityEditor 引用。
Unity 只读取 Fact，Presenter 不写 GameState，也不调用其他 Presenter。
Update 仅处理输入/动画/VFX；没有世界规则或 Reaction 判定。

交互 Prefab 保持 **Logic / Visual** 分离。更换模型只替换 Visual 并重绑表现引用，
不要改 SceneEntity/ObjectId 或 Domain 规则来适配素材。

每个 Presenter 支持 `ApplyState(state, animate: false)` 立即设置最终状态；
FactAdded 时才执行 transition。Restart 重建 Session、重新绑定监听并恢复所有表现，
包括书/纸位置、植物形态/颜色、窗景、音频、水渍、灯光与结束提示。

## Debug

F3 内：Reset、Grant all clues、Add Fact，以及数据预设 **After Plant / After Window / Before Sea / Emergence**。
预设只列种子 Fact，再使用正式闭包生成后续状态。跳转立即 Restore，不等待动画。
Before Sea 已授予海水线索但没有海画；可直接提交 Sea + Painting 检查最后一段。
Add Fact 输入示例 `plant.grown`、`window.far`、`painting.sea`；未知拼写会报错而不修改状态。
Debug 加 Fact 不会伪造玩家的 CompletedRecipes。

状态区显示 Known Clues、Facts、Completed Recipes、Fired Reactions；Editor/Development 额外显示 Available Recipes。
详细 `[DISCOVERY] / [RECIPE] / [FACT] / [REACTION]` 日志仅在 Editor/Development 输出。
Release 保留 F3 状态工具，隐藏 Available Recipes 和详细日志。

## 当前验证边界

2026-10-04：旧 MVP 基线通过 14 EditMode / 3 PlayMode / Validation / macOS Build。
新灰盒完整非 Debug 鼠标/键盘流程已在实际 standalone 窗口完成；真人首次时长验收待做。
最终自动测试与构建结果参见 `Logs/verification-summary.json`。
最终结果：31/31 EditMode、7/7 PlayMode、Scene Validation 与 macOS Build 通过，0 error。
构建保留 1 条警告：项目未配置 RuntimePipelineConfig，Release 中不启用 Pipeline 服务。

EditMode 覆盖旧 MVP、无序精确配方、三元能力、条件发现、完整海水链、打乱规则、循环和 Debug 预设。
PlayMode 覆盖实际场景射线 + Notebook 按钮的完整流程、UI 失败/第三槽、初始化、相机、跳转、Restart 和 Restore。
音频验证为 AudioSource 播放状态，尚未完成人工听感验收。

未制作镜子、翻转、第二房间、正式结局、自由移动、自由语义、存档或通用规则编辑器。
