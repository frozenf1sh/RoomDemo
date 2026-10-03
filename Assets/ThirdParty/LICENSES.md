# 第三方资源与许可

房间的灰盒几何、材质、海面动画和占位音频均由项目本地生成。没有下载美术模型、贴图或音效包。

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

## 后续资源登记

引入外部资源前，确认并记录以下信息：

| 资源名称 | 来源链接 | 许可 | 下载日期 | 文件与大小 |
| --- | --- | --- | --- | --- |
