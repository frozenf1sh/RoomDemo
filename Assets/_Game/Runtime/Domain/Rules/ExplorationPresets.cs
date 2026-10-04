// =============================================================================
// 文件：ExplorationPresets.cs
// 职责：定义探索调试里程碑，并兼容上一阶段海画预设的解释历史。
// 边界：预设是明确的开发例外，不影响玩家必须亲点物体和细节的要求。
// =============================================================================
using System;
using System.Linq;
namespace Game.Domain
{
    public static class ExplorationPresets
    {
        private static ClueId[] Clues(params string[] ids) => ids.Select(DemoRules.Clue).ToArray();
        public static readonly DebugPreset Start = new DebugPreset("Start", Array.Empty<ClueId>(), Array.Empty<FactId>(), Array.Empty<string>(), "初始探索");
        public static readonly DebugPreset BeforeGrowthInsight = new DebugPreset("BeforeGrowthInsight", Clues("plant", "book", "sprout", "toward_light"), Array.Empty<FactId>(), Array.Empty<string>(), "生长联想之前");
        public static readonly DebugPreset GrowthUnlocked = new DebugPreset("GrowthUnlocked", BeforeGrowthInsight.Known.Concat(Clues("growth")).ToArray(), Array.Empty<FactId>(), new[] { "recipe.insight.growth" }, "已形成生长");
        public static readonly DebugPreset BeforeDayAdvance = new DebugPreset("BeforeDayAdvance", GrowthUnlocked.Known.ToArray(), new[] { ExplorationRules.PlantStartsGrowing }, new[] { "recipe.insight.growth", "recipe.plant.growth" }, "结束今天之前");
        /// <summary>旧预设中的成熟盆栽仍有效；补齐新版解释的即时输出以保持历史一致。</summary>
        public static DebugPreset AdaptLegacy(DebugPreset preset)
        {
            // 新探索预设原样保留，只有明确的旧内容历史才补兼容种子。
            if (!preset.Facts.Contains(DemoRules.PlantGrown)) return preset;
            return new DebugPreset(preset.Id, preset.Known.ToArray(), preset.Facts.Concat(new[] { ExplorationRules.PlantStartsGrowing }).Distinct().ToArray(), preset.CompletedRecipes.ToArray(), preset.DisplayName);
        }
    }
}
