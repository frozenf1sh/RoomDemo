# 现有画室Prefab拆分记录

日期：2026年10月19日。实际修改对象是AtelierPlayable现有场景；没有安装新四阶段玩法、阶段底稿或新资产模型。

## 本轮完成结果

通过Unity Editor API新增并连接 **71 个Prefab**。重新打开场景后仍有 **1888 个对象**；比较 **169551 项**层级、Transform、组件字段和语义对象引用，与迁移前一致。保存连接和重开两次均通过比较。

村落原组合含760个展开节点，本轮先将3栋房屋和12棵树制成15个子Prefab（当前目录名为村落房屋），再保存只负责组合的村落父Prefab。地板、坡地、室内遮挡属于同一功能的重复结构，保留为独立模块，避免给每块木板制造一个维护文件。

| 分类 | 新Prefab数量 |
| --- | --- |
| 建筑与木作 | 20 |
| 室内家具 | 7 |
| 窗外山谷 | 6 |
| 村落房屋 | 15 |
| 原创风景习作 | 3 |
| 表现工作簇 | 5 |
| 交互工作簇 | 7 |
| UI | 8 |

## 保护和边界

正式AtelierNight、EditorBuildSettings以及用户原有QualitySettings文件在迁移前后哈希一致。没有重建美术场景，没有移动现有节点，没有更改稳定ID和画布UI文案，也未替换已有NotebookPanel、PauseMenuPanel或藏品母版。

根节点、主摄像机、机位、会话装配器和跨模块调度留在场景。母版内部引用保存在Prefab中；指向其他场景对象的引用保留为场景实例覆盖。这些模块可以独立编辑视觉和内部结构，但带外部依赖的模块目前还需要场景组合根，不能直接拖到空场景就认为独立可玩。

## UI同学可直接编辑的现有模块

新增UI模块目录是`Assets/_Game/Content/Prefabs/AtelierModules/UI/`，包括未读笔记提醒、短暂新发现、世界字幕、旁白区域、悬停物体名、便签阅读、调查与动作、返回画室八个Prefab。笔记、暂停和藏品仍使用已有母版。新画布操作面板是后续任务，详见《04 UI开发交接与画布工作》。

## 完整模块与外部依赖

对象数包含已有嵌套模型和Prefab，父子模块存在重叠，不能把此列相加当场景对象总量。

| 场景模块 | Prefab路径 | 展开对象数 | 外部场景引用数 |
| --- | --- | --- | --- |
| 独立夜间画室/建筑与木作/左侧厚灰泥墙 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_左侧厚灰泥墙.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/右侧厚灰泥墙 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_右侧厚灰泥墙.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/窗左墙 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_窗左墙.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/窗右墙 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_窗右墙.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/窗下墙 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_窗下墙.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/窗上墙 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_窗上墙.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/真实厚边错缝地板 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_真实厚边错缝地板.prefab` | 117 | 0 |
| 独立夜间画室/建筑与木作/有厚度的擦开屋顶 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_有厚度的擦开屋顶.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/纵向旧木梁_-3.75 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_纵向旧木梁_-3.75.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/纵向旧木梁_-2.05 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_纵向旧木梁_-2.05.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/纵向旧木梁_2.05 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_纵向旧木梁_2.05.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/纵向旧木梁_3.75 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_纵向旧木梁_3.75.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/后墙承重横梁 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_后墙承重横梁.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/前景承重横梁 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_前景承重横梁.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/墙角木立柱_-4.23 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_墙角木立柱_-4.23.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/墙角木立柱_4.23 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_墙角木立柱_4.23.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/墙根压脚木 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_墙根压脚木.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/旧窗与亚麻帘 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_旧窗与亚麻帘.prefab` | 27 | 0 |
| 独立夜间画室/建筑与木作/屋顶干刷残留 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_屋顶干刷残留.prefab` | 1 | 0 |
| 独立夜间画室/建筑与木作/开局完整屋顶 | `Assets/_Game/Content/Prefabs/AtelierModules/建筑与木作/PF_开局完整屋顶.prefab` | 1 | 0 |
| 独立夜间画室/室内家具/有磨损的扫描工作台 | `Assets/_Game/Content/Prefabs/AtelierModules/室内家具/PF_有磨损的扫描工作台.prefab` | 2 | 0 |
| 独立夜间画室/室内家具/后移的工作椅 | `Assets/_Game/Content/Prefabs/AtelierModules/室内家具/PF_后移的工作椅.prefab` | 2 | 0 |
| 独立夜间画室/室内家具/画架旁旧凳 | `Assets/_Game/Content/Prefabs/AtelierModules/室内家具/PF_画架旁旧凳.prefab` | 2 | 0 |
| 独立夜间画室/室内家具/Krag 雕脚旧书柜 | `Assets/_Game/Content/Prefabs/AtelierModules/室内家具/PF_Krag 雕脚旧书柜.prefab` | 3 | 0 |
| 独立夜间画室/室内家具/左侧旧立柜 | `Assets/_Game/Content/Prefabs/AtelierModules/室内家具/PF_左侧旧立柜.prefab` | 5 | 0 |
| 独立夜间画室/室内家具/细木作画架 | `Assets/_Game/Content/Prefabs/AtelierModules/室内家具/PF_细木作画架.prefab` | 19 | 0 |
| 独立夜间画室/室内家具/Quixel 厚木工作台 | `Assets/_Game/Content/Prefabs/AtelierModules/室内家具/PF_Quixel 厚木工作台.prefab` | 3 | 0 |
| 独立夜间画室/窗外山谷/近景枝叶与坡地 | `Assets/_Game/Content/Prefabs/AtelierModules/窗外山谷/PF_近景枝叶与坡地.prefab` | 8 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋/近坡石脚小屋 | `Assets/_Game/Content/Prefabs/AtelierModules/村落房屋/PF_近坡石脚小屋.prefab` | 241 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋/中坡窄屋 | `Assets/_Game/Content/Prefabs/AtelierModules/村落房屋/PF_中坡窄屋.prefab` | 241 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋/远坡旧屋 | `Assets/_Game/Content/Prefabs/AtelierModules/村落房屋/PF_远坡旧屋.prefab` | 241 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋/山坡树_0 | `Assets/_Game/Content/Prefabs/AtelierModules/村落房屋/PF_山坡树_0.prefab` | 3 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋/山坡树_1 | `Assets/_Game/Content/Prefabs/AtelierModules/村落房屋/PF_山坡树_1.prefab` | 3 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋/山坡树_2 | `Assets/_Game/Content/Prefabs/AtelierModules/村落房屋/PF_山坡树_2.prefab` | 3 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋/山坡树_3 | `Assets/_Game/Content/Prefabs/AtelierModules/村落房屋/PF_山坡树_3.prefab` | 3 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋/山坡树_4 | `Assets/_Game/Content/Prefabs/AtelierModules/村落房屋/PF_山坡树_4.prefab` | 3 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋/山坡树_5 | `Assets/_Game/Content/Prefabs/AtelierModules/村落房屋/PF_山坡树_5.prefab` | 3 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋/山坡树_6 | `Assets/_Game/Content/Prefabs/AtelierModules/村落房屋/PF_山坡树_6.prefab` | 3 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋/山坡树_7 | `Assets/_Game/Content/Prefabs/AtelierModules/村落房屋/PF_山坡树_7.prefab` | 3 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋/山坡树_8 | `Assets/_Game/Content/Prefabs/AtelierModules/村落房屋/PF_山坡树_8.prefab` | 3 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋/山坡树_9 | `Assets/_Game/Content/Prefabs/AtelierModules/村落房屋/PF_山坡树_9.prefab` | 3 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋/山坡树_10 | `Assets/_Game/Content/Prefabs/AtelierModules/村落房屋/PF_山坡树_10.prefab` | 3 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋/山坡树_11 | `Assets/_Game/Content/Prefabs/AtelierModules/村落房屋/PF_山坡树_11.prefab` | 3 | 0 |
| 独立夜间画室/窗外山谷/中景错落小屋 | `Assets/_Game/Content/Prefabs/AtelierModules/窗外山谷/PF_中景错落小屋.prefab` | 760 | 0 |
| 独立夜间画室/窗外山谷/远山轮廓 | `Assets/_Game/Content/Prefabs/AtelierModules/窗外山谷/PF_远山轮廓.prefab` | 4 | 0 |
| 独立夜间画室/窗外山谷/返工_自然坡地 | `Assets/_Game/Content/Prefabs/AtelierModules/窗外山谷/PF_返工_自然坡地.prefab` | 197 | 0 |
| 独立夜间画室/窗外山谷/返工_完整旧屋 | `Assets/_Game/Content/Prefabs/AtelierModules/窗外山谷/PF_返工_完整旧屋.prefab` | 10 | 0 |
| 独立夜间画室/窗外山谷/返工_村落土路 | `Assets/_Game/Content/Prefabs/AtelierModules/窗外山谷/PF_返工_村落土路.prefab` | 5 | 0 |
| 独立夜间画室/原创风景习作/左墙参考习作_0 | `Assets/_Game/Content/Prefabs/AtelierModules/原创风景习作/PF_左墙参考习作_0.prefab` | 6 | 0 |
| 独立夜间画室/原创风景习作/左墙参考习作_1 | `Assets/_Game/Content/Prefabs/AtelierModules/原创风景习作/PF_左墙参考习作_1.prefab` | 6 | 0 |
| 独立夜间画室/原创风景习作/背墙构图小稿 | `Assets/_Game/Content/Prefabs/AtelierModules/原创风景习作/PF_背墙构图小稿.prefab` | 6 | 0 |
| 独立夜间画室/返工_扫描工作簇 | `Assets/_Game/Content/Prefabs/AtelierModules/表现工作簇/PF_返工_扫描工作簇.prefab` | 101 | 0 |
| 独立夜间画室/返工_辅助工作簇 | `Assets/_Game/Content/Prefabs/AtelierModules/表现工作簇/PF_返工_辅助工作簇.prefab` | 24 | 1 |
| 独立夜间画室/桌面与创作痕迹 | `Assets/_Game/Content/Prefabs/AtelierModules/表现工作簇/PF_桌面与创作痕迹.prefab` | 59 | 0 |
| 独立夜间画室/照明 | `Assets/_Game/Content/Prefabs/AtelierModules/表现工作簇/PF_照明.prefab` | 9 | 0 |
| 独立夜间画室/持续流动的程序化星夜 | `Assets/_Game/Content/Prefabs/AtelierModules/表现工作簇/PF_持续流动的程序化星夜.prefab` | 2 | 2 |
| 新画室玩法/独立洗笔工作簇 | `Assets/_Game/Content/Prefabs/AtelierModules/交互工作簇/PF_独立洗笔工作簇.prefab` | 7 | 0 |
| 新画室玩法/点灯工作簇 | `Assets/_Game/Content/Prefabs/AtelierModules/交互工作簇/PF_点灯工作簇.prefab` | 56 | 2 |
| 新画室玩法/拉帘工作簇 | `Assets/_Game/Content/Prefabs/AtelierModules/交互工作簇/PF_拉帘工作簇.prefab` | 41 | 4 |
| 新画室玩法/湿色与整体擦除工作簇 | `Assets/_Game/Content/Prefabs/AtelierModules/交互工作簇/PF_湿色与整体擦除工作簇.prefab` | 11 | 20 |
| 新画室玩法/Logic_室内遮挡 | `Assets/_Game/Content/Prefabs/AtelierModules/交互工作簇/PF_Logic_室内遮挡.prefab` | 158 | 0 |
| 新画室玩法/Visual_交稿便条 | `Assets/_Game/Content/Prefabs/AtelierModules/交互工作簇/PF_Visual_交稿便条.prefab` | 3 | 0 |
| 新画室玩法/EventSystem | `Assets/_Game/Content/Prefabs/AtelierModules/交互工作簇/PF_EventSystem.prefab` | 1 | 0 |
| 新画室玩法/画室纸面与字幕/未读笔记提醒 | `Assets/_Game/Content/Prefabs/AtelierModules/UI/PF_未读笔记提醒.prefab` | 2 | 0 |
| 新画室玩法/画室纸面与字幕/短暂新发现 | `Assets/_Game/Content/Prefabs/AtelierModules/UI/PF_短暂新发现.prefab` | 2 | 0 |
| 新画室玩法/画室纸面与字幕/世界字幕 | `Assets/_Game/Content/Prefabs/AtelierModules/UI/PF_世界字幕.prefab` | 1 | 0 |
| 新画室玩法/画室纸面与字幕/旁白区域 | `Assets/_Game/Content/Prefabs/AtelierModules/UI/PF_旁白区域.prefab` | 2 | 0 |
| 新画室玩法/画室纸面与字幕/悬停物体名 | `Assets/_Game/Content/Prefabs/AtelierModules/UI/PF_悬停物体名.prefab` | 1 | 0 |
| 新画室玩法/画室纸面与字幕/便签阅读 | `Assets/_Game/Content/Prefabs/AtelierModules/UI/PF_便签阅读.prefab` | 4 | 0 |
| 新画室玩法/画室纸面与字幕/调查与动作 | `Assets/_Game/Content/Prefabs/AtelierModules/UI/PF_调查与动作.prefab` | 4 | 0 |
| 新画室玩法/画室纸面与字幕/返回画室 | `Assets/_Game/Content/Prefabs/AtelierModules/UI/PF_返回画室.prefab` | 2 | 0 |

## 外部绑定明细

### PF_返工_辅助工作簇

- `独立夜间画室/返工_辅助工作簇/辅助工作台上的小油灯.Surfaces.Array.data[1] -> 新画室玩法/点灯工作簇/Visual_裸露棉灯芯`

### PF_持续流动的程序化星夜

- `独立夜间画室/持续流动的程序化星夜.FollowCamera -> 独立夜间画室/画室主镜头`
- `独立夜间画室/持续流动的程序化星夜/星夜参数载体_实际使用固定画幅.OnlyCamera -> 独立夜间画室/画室主镜头`

### PF_点灯工作簇

- `新画室玩法/点灯工作簇.LampLight -> 独立夜间画室/照明/辅助油灯的局部暖光`
- `新画室玩法/点灯工作簇/Logic_左侧油灯.HoverVisual -> 独立夜间画室/返工_辅助工作簇/辅助工作台上的小油灯`

### PF_拉帘工作簇

- `新画室玩法/拉帘工作簇.Cloth.Array.data[0] -> 独立夜间画室/建筑与木作/旧窗与亚麻帘/亚麻帘褶_0`
- `新画室玩法/拉帘工作簇.Cloth.Array.data[1] -> 独立夜间画室/建筑与木作/旧窗与亚麻帘/亚麻帘褶_1`
- `新画室玩法/拉帘工作簇/Logic_窗帘_0.HoverVisual -> 独立夜间画室/建筑与木作/旧窗与亚麻帘/亚麻帘褶_0`
- `新画室玩法/拉帘工作簇/Logic_窗帘_1.HoverVisual -> 独立夜间画室/建筑与木作/旧窗与亚麻帘/亚麻帘褶_1`

### PF_湿色与整体擦除工作簇

- `新画室玩法/湿色与整体擦除工作簇.ArtRoot -> 独立夜间画室`
- `新画室玩法/湿色与整体擦除工作簇.GameplayRoot -> 新画室玩法`
- `新画室玩法/湿色与整体擦除工作簇.RoofSurfaces.Array.data[0] -> 独立夜间画室/建筑与木作/开局完整屋顶`
- `新画室玩法/湿色与整体擦除工作簇.RoofSurfaces.Array.data[1] -> 独立夜间画室/建筑与木作/有厚度的擦开屋顶`
- `新画室玩法/湿色与整体擦除工作簇.FrameSurfaces.Array.data[0] -> 独立夜间画室/建筑与木作/旧窗与亚麻帘/扫描旧木窗_0/Old_Wooden_Window_vmfwcabdw_Mid`
- `新画室玩法/湿色与整体擦除工作簇.FrameSurfaces.Array.data[1] -> 独立夜间画室/建筑与木作/旧窗与亚麻帘/厚实窗台`
- `新画室玩法/湿色与整体擦除工作簇.FrameSurfaces.Array.data[2] -> 独立夜间画室/建筑与木作/旧窗与亚麻帘/旧木窗楣`
- `新画室玩法/湿色与整体擦除工作簇.FrameSurfaces.Array.data[3] -> 独立夜间画室/建筑与木作/旧窗与亚麻帘/宽窗细木分格/中央分格木`
- `新画室玩法/湿色与整体擦除工作簇.FrameSurfaces.Array.data[4] -> 独立夜间画室/建筑与木作/旧窗与亚麻帘/宽窗细木分格/水平分格木`
- `新画室玩法/湿色与整体擦除工作簇.Sky -> 独立夜间画室/持续流动的程序化星夜`
- `新画室玩法/湿色与整体擦除工作簇.LocalLights.Array.data[0] -> 独立夜间画室/照明/从窗洞进入的冷月光`
- `新画室玩法/湿色与整体擦除工作簇.LocalLights.Array.data[1] -> 独立夜间画室/照明/窗台与帘褶的蓝色反弹`
- `新画室玩法/湿色与整体擦除工作簇.LocalLights.Array.data[2] -> 独立夜间画室/照明/桌灯向下的暖工作区`
- `新画室玩法/湿色与整体擦除工作簇.LocalLights.Array.data[3] -> 独立夜间画室/照明/夜间微弱环境方向光`
- `新画室玩法/湿色与整体擦除工作簇.LocalLights.Array.data[4] -> 独立夜间画室/照明/桌灯在画室中的漫反弹`
- `新画室玩法/湿色与整体擦除工作簇.LocalLights.Array.data[5] -> 独立夜间画室/照明/桌灯烘焙柔光`
- `新画室玩法/湿色与整体擦除工作簇.LocalLights.Array.data[6] -> 独立夜间画室/照明/油灯向墙面的柔和散射`
- `新画室玩法/湿色与整体擦除工作簇.LocalLights.Array.data[7] -> 独立夜间画室/照明/辅助油灯的局部暖光`
- `新画室玩法/湿色与整体擦除工作簇.LocalLights.Array.data[8] -> 新画室玩法/点灯工作簇/Visual_手持火柴头/火柴接触小光`
- `新画室玩法/湿色与整体擦除工作簇.LocalLights.Array.data[9] -> 新画室玩法/点灯工作簇/窗边反射落到左侧案面`

## 可复现工具与验证

迁移器：`Tools/Editor/AtelierPrefabModularization.cs`，只由Pipeline run_script临时编译，不进入游戏运行时。Audit只读审计；Migrate拒绝未保存场景、运行中编辑和覆盖已有目标。先保存精确原场景恢复点，发生业务数据差异则恢复原场景并删除本次新建Prefab。已完成的迁移不应重复运行。

```bash
unity command run_script --file Tools/Editor/AtelierPrefabModularization.cs --entry AtelierPrefabModularization.Audit
```

运行复查工具：`Tools/Editor/AtelierPrefabRuntimeReview.cs`，启动当前场景、检查关键引用并开合笔记，捕获一次世界主镜头；不模拟真人鼠标，不跑归档旧主线套件。本轮实际结果：脚本编译无诊断，现有会话启动成功，关键引用有效，笔记本开合成功，19个交互目标存在，Metal离屏主镜头可见，运行期收集到0条Error/Exception/Assert。自动调用洗笔近景入口取得参考截图，不是玩家鼠标验收。结果见参考资料/Prefab运行检查.json。

机读迁移报告已复制到参考资料/Prefab迁移报告.json。原始组件快照和恢复点原在本机忽略目录`Logs/FlowRedesign/Prefab/`，现已随临时文件移出项目，恢复位置见参考资料/临时文件清理记录.json，不把Unity含身份参数的原始日志作为文档附件。

Unity在本轮打开/保存时自动重写了共享LightingData的序列化文件，已在进程退出后精确恢复到任务开始时的仓库版本；该文件最终不列入改动。没有重新烘焙光照。最终Git核对以此恢复后的状态为准。拆分已在9272c09提交并推送；本轮文档清理没有重新运行或改写Unity场景。
