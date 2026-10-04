// =============================================================================
// 文件：ExplorationRules.cs
// 职责：第四阶段的具体内容增量；让生长来自两处亲点观察，而非房间点击奖励。
// 边界：复用统一引擎及旧海画内容；这里没有 Unity 演出时间、热点几何或材质。
// =============================================================================
using System;
using System.Linq;
namespace Game.Domain
{
    /// <summary>探索版本内容表；旧 MVP 表只作为明确的回归样本保留。</summary>
    public static class ExplorationRules
    {
        public static readonly ClueId Sprout = new ClueId("sprout"), TowardLight = new ClueId("toward_light");
        public static readonly FactId PlantStartsGrowing = new FactId("plant.starts_growing"), PlantOvergrown = new FactId("plant.overgrown"), WorldStarry = new FactId("world.starry");
        public static RuleSet Create()
        {
            var old = DemoRules.Create();
            // 只替换具体内容数据；匹配与闭包引擎不认识“生长”或“新芽”。
            var clues = old.Clues.Concat(new[] { new ClueDefinition(Sprout, "新芽", ClueCategory.Observation), new ClueDefinition(TowardLight, "向光", ClueCategory.Observation) }).ToArray();
            var discoveries = old.Discoveries.Select(x => x.Source == DemoRules.Plant
                ? new DiscoveryRule(x.Id, x.Source, x.DisplayName, "盆栽的土边似乎藏着一点新绿。拿近后换个角度看。", Array.Empty<FactId>(), new[] { DemoRules.Clue("plant") })
                : x.Source == DemoRules.Book
                ? new DiscoveryRule(x.Id, x.Source, x.DisplayName, "架边的旧书可以拿近翻看。书页中有些短句还没有读过。", Array.Empty<FactId>(), new[] { DemoRules.Clue("book") }) : x).ToArray();
            var recipes = old.Recipes.Select(x => x.Id == "recipe.plant.growth"
                ? new SemanticRecipe(x.Id, x.Inputs, x.Requires, new[] { PlantStartsGrowing }) : x)
                .Concat(new[] { new SemanticRecipe("recipe.insight.growth", new[] { Sprout, TowardLight }, Array.Empty<FactId>(), new[] { DemoRules.Clue("growth") }, Array.Empty<FactId>()) }).ToArray();
            var observations = new[] {
                new ObservationDiscovery(DemoRules.Plant, "sprout", "土边，一颗新芽。已经把这个细节记下。", Array.Empty<FactId>(), new[] { Sprout }),
                new ObservationDiscovery(DemoRules.Book, "toward_light", "“枝叶仍向光处伸去。”\n向光——把这两个字留在笔记里。", Array.Empty<FactId>(), new[] { TowardLight }),
                new ObservationDiscovery(DemoRules.Book, "book_far", "第二天，页边的地平线似乎伸到了纸外。远方。", new[] { DemoRules.BookFallen }, new[] { DemoRules.Clue("far") }, "页边的铅笔痕还很淡。也许明天再看，会有新的发现。") };
            return new RuleSet(clues, discoveries, recipes, old.Reactions.ToArray(), observations);
        }
    }
}
