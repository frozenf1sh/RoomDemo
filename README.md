# RoomDemo

Unity 6000.6.3f1 / URP。一个围绕观察、绘画和意义迁移展开的交互原型。

## 从这里开始

建议按顺序阅读：

1. [新人上手](Docs/Guide/新人上手.md)：项目目录、场景入口和常用开发步骤。
2. [项目开发规范](AGENTS.md)：架构边界、文案约定、资源管理和验收要求。
3. [系统架构](Docs/Architecture/系统架构.md)与[系统数据流图](Docs/Architecture/系统数据流.drawio)：领域、应用、Unity 适配和 Editor 工具如何协作。
4. [玩法流程](Docs/Design/玩法流程.md)：当前玩家操作顺序、路线分支和未完成范围。
5. [画室玩法实施记录](Docs/Design/画室玩法实施记录.md)：分阶段的实现内容、验证结果和限制。
6. 修改技术美术资源前，阅读[生产接入说明](Docs/TechnicalArt/Production/接入说明.md)及相关素材许可。

## 当前场景与进度

`Assets/_Game/Content/Scenes/AtelierPlayable.unity` 是可玩副本的开发入口，场景已纳入 Git。正式 Build Settings 仍指向 `AtelierNight.unity`；开发副本与正式构建入口分开维护。

当前主流程从桌面便条和空白画布开始，玩家亲自观察静物、逐件落笔，通过实体洗笔形成画中水纹，再用火柴点灯、比较灯芯变化并为画中灯添色。之后玩家拉动实体窗帘，等待布褶落稳、重新观察变化，再把“打开”用于画中窗。窗帘沿帘杆受约束收拢的演出已完成。

当前完成范围与待办边界：

- 已实现：开场静物画、实体洗笔与画中水纹、点灯及画中灯、拉帘后的前后观察与画中窗补笔。
- 尚未完成：独立静物画重做、手部握持与接触表现、真人鼠标操作验收、屋顶变化和统一全房间擦除。
- 自动事件回放和独立构建截图不能代替真人鼠标验收；已实现的窗帘演出也不代表手部接触表现已完成。

## 游玩文档

[玩法流程](Docs/Design/玩法流程.md)记录了目前详细的操作步骤；[实施记录](Docs/Design/画室玩法实施记录.md)记录每阶段的实现与验证边界。当前还没有覆盖完整结尾的已完成实机攻略：屋顶和全房间擦除仍在开发中。

## 常用入口

- 运行开发副本：在 Unity 中打开 `AtelierPlayable.unity` 并进入 Play Mode。
- 正式构建场景：查看 `ProjectSettings/EditorBuildSettings.asset`，目前启用 `AtelierNight.unity`。不要为了运行开发副本而改写正式构建列表。
- 技术美术资源：从 [`Assets/_Game/Content/TechnicalArt/Production/`](Assets/_Game/Content/TechnicalArt/Production/) 与[接入说明](Docs/TechnicalArt/Production/接入说明.md)开始。
- 游戏玩法新增资源的来源和许可见[玩法资产来源与许可](Assets/_Game/Content/ArtRebuild/Gameplay/来源与许可.md)；第三方资源总表见[许可证](Assets/ThirdParty/LICENSES.md)。
