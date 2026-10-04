// =============================================================================
// 文件：DebugPresets.cs
// 职责：以数据定义四个调试跳转状态。
// 边界：只存线索、种子事实与历史；完整事实由正式规则闭包推导。
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    /// <summary>不可变调试快照，只列种子数据与已建立的解释。</summary>
    public sealed class DebugPreset
    {
        /// <summary>内容的稳定身份，用于查找与去重。</summary>
        public string Id { get; }
        /// <summary>面向玩家或维护者的中文显示名称，不参与规则匹配。</summary>
        public string DisplayName { get; }
        /// <summary>预设授予的碎片快照。</summary>
        public IReadOnlyList<ClueId> Known { get; }
        /// <summary>用于正式闭包计算的种子事实，不是完整世界快照。</summary>
        public IReadOnlyList<FactId> Facts { get; }
        /// <summary>已建立的玩家配方 ID 快照，用于历史和重复提交校验。</summary>
        public IReadOnlyList<string> CompletedRecipes { get; }
        /// <summary>恢复的日序；不借用真实时间或存档系统。</summary>
        public int DayIndex { get; }
        /// <summary>复制预设数组，中文显示名与稳定预设 ID 分开。</summary>
        public DebugPreset(string id, ClueId[] known, FactId[] facts, string[] recipes, string displayName = null, int dayIndex = 1)
        { DayIndex = dayIndex; Id = id; DisplayName = displayName ?? id; Known = Array.AsReadOnly((ClueId[])known.Clone()); Facts = Array.AsReadOnly((FactId[])facts.Clone()); CompletedRecipes = Array.AsReadOnly((string[])recipes.Clone()); }
    }
    /// <summary>正式规则求值后的四个调试里程碑。</summary>
    public static class DebugPresets
    {
        // 预设保存稳定 ID，中文按钮名称不参与跳转状态计算。

        /// <summary>把预设中的稳定字符串列表转换为碎片数组。</summary>
        private static ClueId[] Clues(params string[] values) => values.Select(DemoRules.Clue).ToArray();
        // 种子只列玩家解释的主要输出，其余事实统一由反应闭包补全。

        /// <summary>盆栽解释之后；闭包自动补全书落下与调查许可。</summary>
        public static readonly DebugPreset AfterPlant = new DebugPreset("After Plant", Clues("plant", "growth"), new[] { DemoRules.PlantGrown }, new[] { "recipe.plant.growth" }, "盆栽生长后");
        /// <summary>远方解释之后；闭包自动补全风与纸张显露。</summary>
        public static readonly DebugPreset AfterWindow = new DebugPreset("After Window", Clues("plant", "growth", "book", "far", "window"), new[] { DemoRules.PlantGrown, DemoRules.WindowFar }, new[] { "recipe.plant.growth", "recipe.window.far" }, "窗户通向远方后");
        /// <summary>最后一次解释之前；线索齐备但尚无海画、水和涌现。</summary>
        public static readonly DebugPreset BeforeSea = new DebugPreset("Before Sea", Clues("plant", "growth", "book", "far", "window", "painting", "sea"), new[] { DemoRules.PlantGrown, DemoRules.WindowFar }, new[] { "recipe.plant.growth", "recipe.window.far" }, "海的解释之前");
        /// <summary>三次解释均成立；闭包自动得到完整水生化与海窗。</summary>
        public static readonly DebugPreset Emergence = new DebugPreset("Emergence", BeforeSea.Known.ToArray(), new[] { DemoRules.PlantGrown, DemoRules.WindowFar, DemoRules.PaintingSea }, new[] { "recipe.plant.growth", "recipe.window.far", "recipe.painting.sea" }, "涌现完成");
        /// <summary>按界面顺序排列的四个只读调试预设。</summary>
        public static IReadOnlyList<DebugPreset> All { get; } = Array.AsReadOnly(new[] { AfterPlant, AfterWindow, BeforeSea, Emergence });
    }
}
