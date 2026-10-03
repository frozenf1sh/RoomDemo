# 房间：意义构成的世界

项目名称：RoomDemo。使用 Unity **6000.6.3f1**、通用渲染管线（URP）与新版输入系统（Input System）。

第二阶段可玩切片的核心流程：

**观察 → 收集碎片 → 笔记本组合 → 事实成立 → 自动反应 → 新发现 → 旧解释交叉。**

所有项目自有文档、代码注释与游戏文字统一使用简体中文，后续开发必须遵守 [项目规范](AGENTS.md)。
类型、命令、路径和稳定内容 ID 保留已有命名。界面和场景标签使用随包嵌入的中文字体，不依赖系统字体。

## 启动与操作

打开 `Assets/_Game/Content/Scenes/DemoRoom.unity` 进入运行模式，或运行 `Builds/StandaloneOSX/RoomDemo.app`。

- 左键调查；按住右键拖动视角。观察点固定，偏航范围 ±80°、俯仰范围 ±45°。
- **N / Tab** 或按钮开关笔记本，**Esc** 关闭，**Home** 复位视角。
- 点击已收集碎片填入最多三个组合槽；点击槽或同一碎片可取消选择。
- 点击 **建立解释** 提交组合；成功后短暂强调并自动收起笔记本，世界立即开始变化。
- **H** 显示或隐藏当前提示；**重新开始** 创建新会话并立即恢复初始表现。
- **F3** 打开领域调试面板。状态区可滚动，输入稳定事实 ID 后点击 **添加事实**。

## 完整流程（含答案）

1. 调查盆栽，得到 **盆栽 + 生长**。组合它们：植物长大、枝叶伸向书架，书自动落下。
2. 调查地上的红色书，得到 **书 + 远方 + 窗户**。书在书架上可见，但尚不可调查。
3. 组合 **远方 + 窗户**：朦胧窗景变为天空、远山和原野。
4. 已成立的植物生长与远方窗景自动触发风：植物摇摆、纸张飘动、风声开始。画框后的纸滑到地毯上。
5. 调查纸张，得到 **海 + 画**。也可提前观察画；重复碎片不会再次授予。
6. 组合 **海 + 画**：静态画变为动态海面，海浪声开始，画框滴水，地面出现局部水渍。
7. 海水自动联系旧解释：植物变为湿润的青绿色水生形态，窗外原野变为海平线。玩家没有再次解释植物或窗户。
8. 环境光和声音变化，出现 **房间记得。** 临时结束提示。

核心可交互对象只有盆栽、书、窗户、画与纸张；实际可收集七个碎片，提交三个配方。
几何、海面动画和音效为原创占位表现；中文字体的来源与许可见 [资源说明](Assets/ThirdParty/LICENSES.md)。
目标首次试玩时间为 8～12 分钟，**尚未由不知答案的真人验证**，见 [试玩记录](Docs/PLAYTEST.md)。

## 命令行流水线

```sh
python3 Tools/pipeline.py                      # 验证现有场景，不覆盖人工调整
python3 Tools/pipeline.py --domain-only        # 仅编译与编辑模式测试
python3 Tools/pipeline.py --regenerate-scene   # 明确重建灰盒及生成资产
```

连接本项目的 Editor 时复用它，否则使用已安装 Editor 的官方批处理入口：

**编译 → 编辑模式测试 → 可选灰盒生成 → 场景验证 → 运行模式测试 → 构建。**

任一步骤失败立即停止，空测试套件也算失败。默认不保存或重建人工场景。
验证器拒绝未保存的房间、缺失组件、重复 ID、遗漏表现绑定、缺失资源及未正确嵌入的中文字体。
显式重建会覆盖生成器管理的场景、Prefab、材料和音频；生成器拒绝覆盖未保存场景。
人工美术版本应另存为其他场景，或只运行验证。

关闭本项目的 Editor 后可运行：

```sh
python3 Tools/pipeline.py --headless
```

使用 `--editor /absolute/path/to/Unity` 指定安装路径。不要启动第二个 Editor 打开同一项目。
测试报告位于 `Logs/Pipeline/`，构建结果位于 `Logs/build-summary.json`，汇总位于 `Logs/verification-summary.json`。
`ROOM_DEMO_BUILD_OUTPUT` 可指定输出；默认是 `Builds/StandaloneOSX/RoomDemo.app`。
桌面构建还支持 Windows64 和 Linux64，但需要安装对应模块。
构建输出目录同时提供中文字体许可原文，分发试玩包时一起保留。

连接 Editor 的独立入口：

```sh
unity command eval 'Game.Editor.DemoSceneBuilder.BuildGreyboxScene();' --project-path "$PWD" --format json
unity command eval 'Game.Editor.DemoSceneValidator.ValidateScene();' --project-path "$PWD" --format json
unity command eval 'Game.Editor.CliBuild.QueueBuild();' --project-path "$PWD" --format json
# 排队后轮询 Logs/build-summary.json，直到 Succeeded 或 Failed。
```

未连接时使用 `-executeMethod` 调用 `Game.Editor.DemoSceneBuilder.BuildGreyboxScene`、
`Game.Editor.DemoSceneValidator.ValidateScene` 或 `Game.Editor.CliBuild.BuildDemo`。
测试参数为 `-runTests -testPlatform EditMode/PlayMode -testResults <XML>`，测试时不要加 `-quit`。
构建入口自己也会先校验场景，输出为桌面发行版，不开启运行时 Pipeline 服务。
机器报告中的命令、字段和结果枚举保持稳定英文，人工说明与项目自己的错误消息使用中文。

## 规则数据与扩展

`Assets/_Game/Runtime/Domain/Rules/DemoRules.cs` 集中定义七个碎片、五个发现、三个配方和八个反应。

| 玩家建立的解释 | 产出事实 ID |
| --- | --- |
| 盆栽 + 生长 | `plant.grown` |
| 窗户 + 远方 | `window.far` |
| 画 + 海 | `painting.sea` |

| 自动反应 | 所需事实 | 产出事实 |
| --- | --- | --- |
| 盆栽碰落书本 | `plant.grown` | `book.fallen` |
| 书本开放调查 | `book.fallen` | `book.inspectable` |
| 旧解释引风入室 | `plant.grown`、`window.far` | `wind.entering` |
| 风吹出纸张 | `wind.entering` | `paper.revealed` |
| 画框渗出海水 | `painting.sea` | `water.present` |
| 海水改变盆栽 | `water.present`、`plant.grown` | `plant.aquatic` |
| 海水改变远方 | `water.present`、`window.far` | `window.ocean` |
| 房间记住解释 | `plant.aquatic`、`window.ocean`、`painting.sea` | `vertical_slice.complete` |

新增内容只需补充碎片定义、发现规则、语义配方、自动反应与对应视觉组件。
**RuleEngine 没有盆栽、海或窗户的具体内容特判。**
配方无序精确匹配，支持两个或三个不同碎片；多余与重复输入不匹配。
相同输入不得配置多个配方。分类预留修饰和短语；镜子三元配方只用于测试。

旧 `ObjectId`、`ConceptId`、`Interpret` 和旧规则构造入口保留为兼容适配。
笔记本主要读取 `KnownClues` 并调用 `Combine`。中文显示名与稳定 ID 分离，翻译不改变匹配或绑定。

GameState 的私有集合是唯一真相，外部只能取得只读快照。
成功记录 `CompletedRecipes`，失败不消费碎片、不写历史、不产生事实。
反应先记录触发再添加去重事实，全量扫描至稳定；规则顺序不影响最终闭包，循环不会重复触发。

## 架构与表现恢复

- **Game.Domain**：ID、碎片与规则数据、命令、事件、状态和引擎。
- **Game.Application**：会话入口、命令执行、事件发布与显式调试操作。
- **Game.Unity**：输入、笔记本、调试面板、固定视角与八个世界表现组件。
- **Game.Editor**：场景生成、验证与构建。

领域层和应用层均设置 `noEngineReferences: true`，不引用 UnityEngine 或 UnityEditor。
表现组件只读取事实、不写 GameState、不直接调用其他表现组件。
Update 仅处理输入、动画和视觉效果，不执行世界规则。

交互 Prefab 保持 **Logic / Visual** 分离。更换模型只替换视觉子层并重绑表现引用，
不要为素材修改 SceneEntity、ObjectId 或领域规则。

`ApplyState(state, animate: false)` 立即恢复最终表现；新增事实事件才执行过渡。
重新开始会创建新会话并重绑监听，恢复书和纸的位置、植物形态、窗景、音频、水渍、灯光和提示。
关键代码的文件头、XML 文档注释与行注释解释职责、边界、执行顺序和资源生命周期。

## 调试工具

F3 面板提供 **重置、获取全部碎片、添加事实**，以及四个中文预设：
**盆栽生长后、窗户通向远方后、海的解释之前、涌现完成**。

预设只列种子事实，其余状态由正式闭包产生。跳转立即恢复，不等待动画。
“海的解释之前”已经授予海与画，但尚未完成海画配方，可直接组合检查最后一段。
“添加事实”输入示例为 `plant.grown`、`window.far`、`painting.sea`；未知拼写返回中文错误且不修改状态。
调试添加事实不会伪造玩家的已完成配方历史。

状态区显示已知碎片、世界事实、已完成配方和已触发反应，中文名称后附稳定 ID。
Editor 与开发构建额外显示当前可用配方并输出详细中文事件日志；发行版保留调试工具，隐藏这两项。

## 验证边界

旧版基线曾通过 14 个编辑模式测试、3 个运行模式测试、场景验证与 macOS 构建。
完整灰盒非调试流程已在实际独立窗口走查。中文版已通过 31 项编辑模式测试、8 项运行模式测试、
场景校验与 macOS 构建；构建错误为 0，最新结果见日志汇总。

编辑模式测试覆盖兼容入口、精确配方、三元能力、条件发现、海水完整链、乱序、循环与调试预设。
运行模式测试覆盖真实射线、按钮完整流程、失败与第三槽、相机、跳转、恢复、重开及中文字体。
中文版已在实际独立窗口检查主界面、笔记本、场景标签和调试按钮的字形。
检查中发现解释历史第三行截断及观察文字与结束提示重叠，已调整生成布局，
并用运行模式测试验证三条解释均能容纳、动态中文文字均能取得字形。
以后修改中文界面仍需实际窗口检查换行、字形和截断。

当前构建保留三条 Unity 包级提示：发行版未配置运行时 Pipeline 服务，
以及两个内部调试遮挡 Shader 被渲染管线裁剪。它们不是项目脚本编译错误。

目标 8～12 分钟的首次真人体验仍待验收，音频播放状态测试不能替代人工听感。
没有制作镜子、翻转、第二房间、正式结局、自由移动、自由语义、存档或通用规则编辑器。
