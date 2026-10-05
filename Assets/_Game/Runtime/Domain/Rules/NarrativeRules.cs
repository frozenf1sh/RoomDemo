// =============================================================================
// 文件：NarrativeRules.cs
// 职责：把同一套观察材料接成画布路线和现实路线，不创建第二套解谜引擎。
// 边界：只有纯内容、成功倾向和条件；不规定旁白、Shader 或演出时长。
// =============================================================================
using System;
using System.Linq;
namespace Game.Domain
{
    /// <summary>第七阶段正式内容。画布不是唯一合法目标，选择完全通过解释表达。</summary>
    public static class NarrativeRules
    {
        /// <summary>画布内部的完成层、共同次日入口和普通完成结局。</summary>
        public static readonly FactId CanvasGrowth = new FactId("painting.growth"), CanvasFar = new FactId("painting.far"),
            CanvasStarry = new FactId("painting.starry"), DayTwo = new FactId("studio.day_two"), BanalComplete = new FactId("ending.canvas_complete");

        /// <summary>复用主线资料，只在内容附近声明两个目标与叙事门槛。</summary>
        public static RuleSet Create()
        {
            var old = MainlineRules.Create();
            var none = Array.Empty<FactId>();
            var clues = old.Clues.Select(c => c.Id == DemoRules.Clue("room")
                ? new ClueDefinition(c.Id, c.DisplayName, c.Category, quietDiscovery: true) : c).ToArray();
            var discoveries = old.Discoveries.Select(d => d.Source == MainlineRules.Room
                ? new DiscoveryRule(d.Id, d.Source, d.DisplayName, "边界松动了。原来房间本身，也是一件可以被看见的东西。", d.Requires, d.Grants, "还只能看见房间里的东西。", minimumDeviations: 2)
                : d.Source == DemoRules.Painting
                ? new DiscoveryRule(d.Id, d.Source, d.DisplayName, "画布还空着。木框在等一个被放进来的想法。", none, d.Grants) : d).ToArray();
            // 服从者不需要先改变盆栽才能继续读书；次日书页是两条路线共有的探索入口。
            var observations = old.Observations.Select(o => o.DetailId == "book_far"
                ? new ObservationDiscovery(o.Source, o.DetailId, "页边的地平线伸向远处。远方。", new[] { DayTwo }, o.Grants.ToArray(), "明天，再看这条地平线。") : o).ToArray();
            var recipes = old.Recipes.Select(r => new SemanticRecipe(r.Id, r.Inputs, r.Requires, r.ProducesClues, r.ProducesFacts,
                r.Id == "recipe.plant.growth" || r.Id == "recipe.window.far" || r.Id == "recipe.ceiling.starry" || r.Id == "recipe.room.forest"
                    ? NarrativeDisposition.Deviant : r.Id == "recipe.painting.sea" ? NarrativeDisposition.Compliant : NarrativeDisposition.Neutral))
                .Concat(new[] {
                    new SemanticRecipe("recipe.painting.growth", new[] { DemoRules.Clue("growth"), DemoRules.Clue("painting") }, none, new[] { CanvasGrowth }, NarrativeDisposition.Compliant),
                    new SemanticRecipe("recipe.painting.far", new[] { DemoRules.Clue("far"), DemoRules.Clue("painting") }, none, new[] { CanvasFar }, NarrativeDisposition.Compliant),
                    new SemanticRecipe("recipe.painting.starry", new[] { MainlineRules.Starry, DemoRules.Clue("painting") }, new[] { MainlineRules.NightArrived }, new[] { CanvasStarry }, NarrativeDisposition.Compliant)
                }).ToArray();
            // 海仍是交给画的解释。已经发生过偏离时，旧意义让它越过画框；收束路线则留在框内。
            var reactions = old.Reactions.Select(r => r.Id == "painting-leaks-water"
                ? new ReactionRule(r.Id, r.Requires.ToArray(), r.Produces.ToArray(), minimumDeviations: 1) : r)
                .Concat(new[] {
                    new ReactionRule("canvas-opens-next-day", new[] { CanvasGrowth }, new[] { DayTwo }, 1),
                    new ReactionRule("growth-opens-next-day", new[] { ExplorationRules.PlantStartsGrowing }, new[] { DayTwo }, 1),
                    new ReactionRule("canvas-distance-reveals-study", new[] { CanvasFar }, new[] { DemoRules.PaperRevealed }),
                    new ReactionRule("canvas-is-complete", new[] { CanvasGrowth, CanvasFar, DemoRules.PaintingSea, CanvasStarry }, new[] { BanalComplete }, requiresNoDeviation: true)
                }).ToArray();
            return new RuleSet(clues, discoveries, recipes, reactions, observations, old.DeclaredFacts.ToArray());
        }
    }
}
