// =============================================================================
// 文件：MainlineRules.cs
// 职责：配置三天主线、星空屋顶、森林和最后营火的有限内容。
// 边界：只有线索、配方与反应数据；不包含演出时长、声音或场景操作。
// =============================================================================
using System;
using System.Linq;
namespace Game.Domain
{
    /// <summary>正式主线内容；历史探索表继续作为旧演出回归样本。</summary>
    public static class MainlineRules
    {
        public static readonly ObjectId Ceiling = new ObjectId("ceiling"), Room = new ObjectId("room");
        public static readonly ClueId Night = DemoRules.Clue("night"), Boundless = DemoRules.Clue("boundless"),
            Starry = DemoRules.Clue("starry"), TreeShadow = DemoRules.Clue("tree_shadow"),
            WindSound = DemoRules.Clue("wind_sound"), Forest = DemoRules.Clue("forest");
        public static readonly FactId NightArrived = new FactId("night.arrived"), CeilingStarry = new FactId("ceiling.starry"),
            WorldForest = new FactId("world.forest"), CampfirePresent = new FactId("campfire.present"), Complete = new FactId("mainline.complete");

        /// <summary>追加内容而不修改规则引擎；物体只能通过对应来源的亲点发现。</summary>
        public static RuleSet Create()
        {
            var prior = ExplorationRules.Create();
            var none = Array.Empty<FactId>();
            var clues = prior.Clues.Concat(new[] {
                new ClueDefinition(DemoRules.Clue("ceiling"), "天花板", ClueCategory.Object),
                new ClueDefinition(DemoRules.Clue("room"), "房间", ClueCategory.Object),
                new ClueDefinition(Night, "夜色", ClueCategory.Observation),
                new ClueDefinition(Boundless, "无边", ClueCategory.Observation),
                new ClueDefinition(Starry, "星空", ClueCategory.Concept),
                new ClueDefinition(TreeShadow, "树影", ClueCategory.Observation),
                new ClueDefinition(WindSound, "风声", ClueCategory.Observation),
                new ClueDefinition(Forest, "森林", ClueCategory.Concept) });
            var discoveries = prior.Discoveries.Concat(new[] {
                new DiscoveryRule("inspect.ceiling", Ceiling, "天花板", "夜色在头顶停住，像一幅没有画完的天空。", new[] { NightArrived }, new[] { DemoRules.Clue("ceiling") }, "天花板还只是天花板。"),
                new DiscoveryRule("inspect.room", Room, "房间", "脚下还是熟悉的木地板。风却像来自很远的树间。", new[] { CeilingStarry }, new[] { DemoRules.Clue("room") }, "房间的边界仍很安静。") });
            var observations = prior.Observations.Concat(new[] {
                new ObservationDiscovery(Ceiling, "night", "夜色落在天花板上，屋内的光也安静下来。", new[] { NightArrived }, new[] { Night }),
                new ObservationDiscovery(DemoRules.Window, "boundless", "海平线之外，已经找不到尽头。无边。", new[] { NightArrived }, new[] { Boundless }),
                new ObservationDiscovery(DemoRules.Window, "tree_shadow", "星光下，窗外浮起重重树影。", new[] { CeilingStarry }, new[] { TreeShadow }),
                new ObservationDiscovery(Room, "wind_sound", "风穿过地板和墙的缝隙，带来树叶摩擦的声音。", new[] { CeilingStarry }, new[] { WindSound }) });
            var recipes = prior.Recipes.Concat(new[] {
                new SemanticRecipe("recipe.insight.starry", new[] { Night, Boundless }, new[] { NightArrived }, new[] { Starry }, none),
                new SemanticRecipe("recipe.ceiling.starry", new[] { Starry, DemoRules.Clue("ceiling") }, new[] { NightArrived }, new[] { CeilingStarry }),
                new SemanticRecipe("recipe.insight.forest", new[] { TreeShadow, WindSound }, new[] { CeilingStarry }, new[] { Forest }, none),
                new SemanticRecipe("recipe.room.forest", new[] { Forest, DemoRules.Clue("room") }, new[] { CeilingStarry }, new[] { WorldForest }) });
            var reactions = prior.Reactions.Concat(new[] {
                // 海画留下跨日效果，玩家明确结束第二天后才有第三天的夜色观察。
                new ReactionRule("sea-remembers-next-night", new[] { DemoRules.PaintingSea }, new[] { NightArrived }, 1),
                // 最后营火由世界自行接受森林解释；表现导演在树林显现之后才点燃它。
                new ReactionRule("forest-shelters-fire", new[] { WorldForest }, new[] { CampfirePresent }),
                new ReactionRule("mainline-remembers", new[] { CeilingStarry, WorldForest, CampfirePresent }, new[] { Complete }) });
            return new RuleSet(clues.ToArray(), discoveries.ToArray(), recipes.ToArray(), reactions.ToArray(), observations.ToArray(), prior.DeclaredFacts.ToArray());
        }
    }
}
