// =============================================================================
// 文件：AtelierCollectionCatalog.cs
// 职责：定义画室可收藏物的稳定身份、展示名和获得条件。
// 边界：只读取既有世界碎片；不引用 Unity，也不直接授予任何物体或事实。
// =============================================================================
using System;
using System.Collections.Generic;

namespace Game.Domain
{
    /// <summary>一条可收藏物定义；显示资源由 Unity 表现层按稳定 ID 提供。</summary>
    public sealed class AtelierCollectionDefinition
    {
        /// <summary>用于 UI 与资源映射的稳定英文 ID。</summary>
        public string Id { get; }
        /// <summary>面向玩家的简体中文名称。</summary>
        public string DisplayName { get; }
        /// <summary>面向玩家的简体中文介绍。</summary>
        public string Description { get; }
        /// <summary>必须由玩家亲自观察后才能入藏的碎片。</summary>
        public ClueId RequiredClue { get; }

        /// <summary>构造只读藏品定义。</summary>
        public AtelierCollectionDefinition(string id, string displayName, string description, ClueId requiredClue)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            RequiredClue = requiredClue;
        }
    }

    /// <summary>新画室的藏品目录；后续藏品只追加定义，分页仍由 UI 统一处理。</summary>
    public static class AtelierCollectionCatalog
    {
        /// <summary>当前全部藏品，顺序即藏品簿内的展示顺序。</summary>
        public static readonly IReadOnlyList<AtelierCollectionDefinition> Definitions = Array.AsReadOnly(new[]
        {
            // 画笔必须来自玩家实际点击杯沿实体后的既有观察，笔记和规则不会替玩家入藏。
            new AtelierCollectionDefinition(
                "atelier.collection.brush",
                "旧画笔",
                "看起来有些年月头的画笔，但却是某人的至爱之物",
                new ClueId(AtelierRules.Brush.Value))
        });
    }
}
