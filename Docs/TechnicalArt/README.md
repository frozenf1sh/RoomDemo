# 技术美术

主项目可接入的绘画渲染、局部影响、程序化天空、水面和擦开组件位于：

- 运行时代码：`Assets/_Game/Runtime/Unity/TechnicalArt/RealtimePainterly/`
- 生产 Prefab：`Assets/_Game/Content/TechnicalArt/Production/Prefabs/`
- 场景与资源生成器：`Assets/_Game/Editor/TechnicalArt/RealtimePainterly/`

添加到场景前先看[生产接入说明](Production/接入说明.md)。这些组件只消费表现指令，不读写 `GameState`。算法来源及静态星夜图的限制见[素材来源](素材来源.md)。
