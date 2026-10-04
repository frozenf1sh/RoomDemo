# 第三方资源与许可

项目自有几何、演出 Shader、程序纹理和占位音频由本地制作。第五阶段另外引入免费模型及木地板、墙面、织物纹理；第三方资产按实际文件登记，并与游戏逻辑分离。

## 中文字体

| 项目 | 内容 |
| --- | --- |
| 资源名称 | Noto Sans CJK SC Regular（简体中文常规字重） |
| 来源 | [Noto 官方字体仓库](https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf) |
| 许可 | SIL 开放字体许可 1.1（SIL Open Font License，简称 OFL） |
| 许可原文 | [官方许可](https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE)，本地保存为 `NotoSans/OFL.txt` |
| 下载日期 | 2026-10-04 |
| 文件 | `NotoSans/NotoSansCJKsc-Regular.otf` |
| 文件大小 | 16,437,364 字节（约 15.7 MiB，以实际文件为准） |
| SHA-256 | `2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b` |

字体保持原始文件，可随软件嵌入与分发。构建脚本会将许可原文复制到试玩输出目录。
`OFL.txt` 是第三方法律文本，保留其官方原文；本说明使用中文，不替代原始许可。

## 项目原创资源

以下资源由 `DemoSceneBuilder` 在本地生成：

- `Wind.wav`：固定随机种子的滤波噪声，由 `CreateWindClip` 生成。
- `Grow.wav`、`Fall.wav`、`Sea.wav`、`Water.wav`、`Ambience.wav`：固定噪声与简单音调合成的占位音效，由 `Sound` 生成。
- `SeaSurface.shader`：数学波浪与泡沫颜色动画，没有外部贴图或流体模拟。
- 核心物体、背景几何与材料：使用 Unity 基础几何体生成。

上述原创资源首次生成于 2026-10-04，无下载来源。

第三阶段新增原创资源：

- `PaintingSea.shader`：颜料、原野到海平线的连续过渡与动态浪纹，纯数学图案。
- `WetSurface.shader`：局部薄湿痕的暗色、表面细纹和视角反光，没有实时反射。
- `WoodGrain.png`、`CanvasGrain.png`、`NotebookPaper.png`：编辑器生成的低对比木纹、表面颗粒和纸页。
- `Leaf.asset`：编辑器生成的双面弯曲叶片。
- `Audio/Benchmark/`：由 `AudioArtPass` 生成的纸页、墨迹、擦划、异常、落滴、水、风、海、植物与房间氛围占位音。没有外部下载音频；后续录制音效替换时重新登记来源。


## 后续资源登记

引入外部资源前，确认并记录以下信息：

| 资源名称 | 来源链接 | 许可 | 下载日期 | 文件与大小 |
| --- | --- | --- | --- | --- |

## Kenney 水生植物视觉原型

- 资产名：Furniture Kit / pottedPlant。
- 来源：Kenney；[资产页面](https://kenney.nl/assets/furniture-kit)。2026-10-04 已重新核实页面明确标注 CC0。
- 许可：Creative Commons Zero（CC0），原文保留在 `Kenney/FurnitureKit/License.txt`。
- 下载日期：2026-10-04。
- 格式与大小：FBX，23,744 字节。官方压缩包约 4.9 MiB，仅导入这一个模型与许可证。
- 使用位置：盆栽水生化附加叶片的视觉原型；材质由项目统一提供，模型不承担交互逻辑。
- 比较记录：Poly Haven 的 Potted Plant 01 当前页面默认下载约 264 MB、96K 三角形；本次采用轻量统一风格方案。

## 第四阶段工作室装饰

2026-10-04 重新核查 [Kenney Furniture Kit 官方页](https://kenney.nl/assets/furniture-kit)，页面明确标注 CC0。复用官方约 4.9 MiB 压缩包，仅提取以下 FBX；许可原文继续使用 `Kenney/FurnitureKit/License.txt`。

| 资产名 | 文件大小 | 使用位置 |
| --- | --- | --- |
| books | 19,229 字节 | 书架与桌面书堆，无游戏逻辑 |
| cardboardBoxClosed | 17,427 字节 | 工作室旧纸箱，无游戏逻辑 |
| cardboardBoxOpen | 17,511 字节 | 草稿收纳箱，无游戏逻辑 |
| lampRoundFloor | 21,092 字节 | 右侧落地灯装饰，无游戏逻辑 |

模型通过自有 Wrapper Prefab 使用，原材质替换为项目统一材料。近景书本、盆栽新芽、跨日余音为本地原创占位内容。

## 第四阶段星空外景

来源：[Kenney Nature Kit 官方页](https://kenney.nl/assets/nature-kit)，2026-10-04 逐资产核对；同一官方压缩包明确采用 CC0，许可原文在 `Kenney/NatureKit/License.txt`。下载包为 10,537,521 字节，只导入下列三个 FBX。

| 资产名 | 文件大小 | 使用位置 |
| --- | --- | --- |
| tree_pineRoundA | 41,580 字节 | 星空外景中的松树，无交互逻辑 |
| rock_largeA | 25,590 字节 | 外景岩石，无交互逻辑 |
| campfire_logs | 28,565 字节 | 自有篝火 Prefab 的木柴视觉层 |

所有模型经 Wrapper 与统一项目材质使用。星空图案、草地几何、火焰运动、光照和占位声音均为项目原创；没有 AI 风格转换、运行时世界生成或流体模拟。

## 第五阶段新增模型与表面纹理

2026-10-05 通过 Chrome 重新查看 Kenney 的两套官方资产页面，均明确标注 CC0。复用此前下载的官方压缩包，按需提取 **47 个新增 FBX**（家具 17 个、自然 30 个）；此前登记的 8 个模型不重复列出。以下大小为仓库中实际文件字节数，所有新增文件的提取／导入日期为 2026-10-05。原始许可证分别保留在 `Kenney/FurnitureKit/License.txt` 与 `Kenney/NatureKit/License.txt`。

“装饰库”表示已导入的视觉候选，不表示每个候选都必须摆入场景；模型均不承载 Clue、Recipe 或交互规则。

### 工作室家具

文件目录：`Kenney/FurnitureKit/`。

| 文件 | 实际大小 | 官方来源 | 许可 | 使用位置／类别 |
| --- | ---: | --- | --- | --- |
| `bookcaseClosed.fbx` | 44,542 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：封闭书柜 |
| `bookcaseOpen.fbx` | 38,100 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：开放书架 |
| `bookcaseOpenLow.fbx` | 26,186 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：矮书架 |
| `cabinetBedDrawer.fbx` | 36,228 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：抽屉收纳柜 |
| `chair.fbx` | 25,130 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：木椅 |
| `chairRounded.fbx` | 38,436 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：圆背椅 |
| `coatRackStanding.fbx` | 33,130 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：立式衣架 |
| `desk.fbx` | 34,207 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：书桌 |
| `deskCorner.fbx` | 41,311 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：转角桌 |
| `lampRoundTable.fbx` | 21,261 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：圆形桌灯 |
| `lampSquareTable.fbx` | 18,047 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：方形桌灯 |
| `lampWall.fbx` | 16,993 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：壁灯 |
| `rugDoormat.fbx` | 40,504 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：门垫 |
| `rugRectangle.fbx` | 13,510 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：地毯 |
| `sideTable.fbx` | 22,455 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：边桌 |
| `table.fbx` | 24,396 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：桌子 |
| `tableRound.fbx` | 19,706 字节 | [Kenney 官方页](https://kenney.nl/assets/furniture-kit) | CC0 | 工作室装饰库：圆桌 |

### 树林与自然环境

文件目录：`Kenney/NatureKit/`。

| 文件 | 实际大小 | 官方来源 | 许可 | 使用位置／类别 |
| --- | ---: | --- | --- | --- |
| `campfire_stones.fbx` | 54,276 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：篝火石圈 |
| `flower_redA.fbx` | 23,315 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：野花 |
| `flower_yellowA.fbx` | 23,492 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：野花 |
| `grass.fbx` | 35,455 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：风动草丛 |
| `grass_large.fbx` | 52,888 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：风动草丛 |
| `grass_leafs.fbx` | 18,669 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：风动长叶草 |
| `grass_leafsLarge.fbx` | 42,404 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：风动长叶草 |
| `log.fbx` | 40,987 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：倒木 |
| `log_stack.fbx` | 33,923 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 自然装饰库：木柴堆 |
| `mushroom_red.fbx` | 20,798 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 自然装饰库：蘑菇 |
| `mushroom_tan.fbx` | 19,960 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 自然装饰库：蘑菇 |
| `plant_bush.fbx` | 16,968 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 自然装饰库：灌木 |
| `plant_bushLarge.fbx` | 23,496 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 自然装饰库：大灌木 |
| `plant_bushSmall.fbx` | 14,333 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：低矮灌木 |
| `plant_flatShort.fbx` | 15,878 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 自然装饰库：短叶植物 |
| `plant_flatTall.fbx` | 15,166 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 自然装饰库：高叶植物 |
| `rock_largeB.fbx` | 26,602 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：岩石 |
| `rock_largeC.fbx` | 24,019 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：岩石 |
| `rock_smallA.fbx` | 13,954 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：碎石 |
| `rock_smallB.fbx` | 14,902 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：碎石 |
| `rock_smallC.fbx` | 14,036 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 自然装饰库：碎石 |
| `stone_largeA.fbx` | 25,231 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 自然装饰库：石头 |
| `stone_smallA.fbx` | 13,653 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 自然装饰库：小石头 |
| `tree_detailed.fbx` | 82,459 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：阔叶树 |
| `tree_oak.fbx` | 45,836 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：橡树 |
| `tree_pineRoundB.fbx` | 60,112 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：松树 |
| `tree_pineRoundC.fbx` | 44,694 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：松树 |
| `tree_pineRoundD.fbx` | 42,452 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：松树 |
| `tree_pineTallA.fbx` | 22,995 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：高松树 |
| `tree_small.fbx` | 21,554 字节 | [Kenney 官方页](https://kenney.nl/assets/nature-kit) | CC0 | 星空外景：矮树 |

### Poly Haven 表面纹理

2026-10-05 通过 Chrome 查看以下三个具体资产页，均显示 CC0，并指向 [Poly Haven 官方许可](https://polyhaven.com/license)。官方说明允许商业使用、修改和分发。本轮仅下载 **1K JPG 的漫反射与 OpenGL 法线**，不用大型模型或完整高分辨率贴图包。下载／导入日期均为 2026-10-05。

| 资产与文件 | 实际大小 | 官方来源 | 许可 | 使用位置 |
| --- | ---: | --- | --- | --- |
| Old Wood Floor／`PolyHaven/OldWoodFloor/Diffuse.jpg`（漫反射） | 755,937 字节 | [资产页](https://polyhaven.com/a/old_wood_floor) | CC0 | 工作室木地板与星空后保留木台 |
| Old Wood Floor／`PolyHaven/OldWoodFloor/nor_gl.jpg`（OpenGL 法线） | 714,487 字节 | [资产页](https://polyhaven.com/a/old_wood_floor) | CC0 | 工作室木地板与星空后保留木台 |
| Painted Plaster Wall／`PolyHaven/PaintedPlasterWall/Diffuse.jpg`（漫反射） | 686,726 字节 | [资产页](https://polyhaven.com/a/painted_plaster_wall) | CC0 | 工作室墙面与独立天花板 |
| Painted Plaster Wall／`PolyHaven/PaintedPlasterWall/nor_gl.jpg`（OpenGL 法线） | 900,461 字节 | [资产页](https://polyhaven.com/a/painted_plaster_wall) | CC0 | 工作室墙面与独立天花板 |
| Rough Linen／`PolyHaven/RoughLinen/Diffuse.jpg`（漫反射） | 835,801 字节 | [资产页](https://polyhaven.com/a/rough_linen) | CC0 | 画布、帘布及布料表面 |
| Rough Linen／`PolyHaven/RoughLinen/nor_gl.jpg`（OpenGL 法线） | 916,843 字节 | [资产页](https://polyhaven.com/a/rough_linen) | CC0 | 画布、帘布及布料表面 |

### 本轮新增原创表现

第五阶段专用溶解、植物分段显影、梦境植物、曲面书页、文字墨迹、风动自然表面、银河与月轮 Shader；书页／枝条／叶片／外景地面网格；旧织毯、纸纤维与研究稿纹样；灰尘、灵感、星光碎屑、火舌、余烬与烟的粒子配置，均为本项目原创制作，不含外部下载图案或 AI 风格转换。上述内容无第三方资产许可要求；实际使用的 Kenney 木柴、石圈、自然模型与 Poly Haven 表面纹理仍按本节 CC0 条目登记。`_Game/Content/Art/Studio/Models/Surface_*.asset` 是保留原始几何、重新投影 UV 的 Kenney 模型衍生文件，仍按相应 CC0 来源登记。
