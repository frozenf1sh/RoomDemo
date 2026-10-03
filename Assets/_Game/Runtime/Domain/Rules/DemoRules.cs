// =============================================================================
// 文件：DemoRules.cs
// 职责：集中配置试玩版的七个碎片、五个发现、三个配方和八个反应。
// 边界：中文名称与观察文字可修改，稳定 ID 不随语言变化。
// =============================================================================
using System;
namespace Game.Domain
{
    /// <summary>试玩版集中内容表，新增内容不要求修改引擎。</summary>
    public static class DemoRules
    {
        // 英文 ID 是与场景绑定的稳定协议；显示名在下面单独使用中文。
        public static readonly ObjectId Plant = new ObjectId("plant"), Book = new ObjectId("book"), Window = new ObjectId("window"), Painting = new ObjectId("painting"), Paper = new ObjectId("paper");
        public static readonly ConceptId Growth = new ConceptId("growth"), Far = new ConceptId("far"), Sea = new ConceptId("sea");
        public static readonly FactId PlantGrown = new FactId("plant.grown"), BookFallen = new FactId("book.fallen"), BookInspectable = new FactId("book.inspectable"),
            WindowFar = new FactId("window.far"), WindEntering = new FactId("wind.entering"), PaperRevealed = new FactId("paper.revealed"), PaintingSea = new FactId("painting.sea"),
            WaterPresent = new FactId("water.present"), PlantAquatic = new FactId("plant.aquatic"), WindowOcean = new FactId("window.ocean"), Complete = new FactId("vertical_slice.complete");
        /// <summary>把稳定字符串转换为统一碎片 ID。</summary>
        public static ClueId Clue(string value) => new ClueId(value);
        private static readonly FactId[] None = Array.Empty<FactId>();
        /// <summary>创建本关所有内容定义；配方与反应均通过纯数据表达。</summary>
        public static RuleSet Create() => new RuleSet(
            new[] {
                // 玩家可收集的物体与概念都使用 ClueId，不强制二元结构。
                new ClueDefinition(Clue("plant"), "盆栽", ClueCategory.Object), new ClueDefinition(Clue("book"), "书", ClueCategory.Object),
                new ClueDefinition(Clue("window"), "窗户", ClueCategory.Object), new ClueDefinition(Clue("painting"), "画", ClueCategory.Object),
                new ClueDefinition(Clue("growth"), "生长", ClueCategory.Concept), new ClueDefinition(Clue("far"), "远方", ClueCategory.Concept), new ClueDefinition(Clue("sea"), "海", ClueCategory.Concept)
            },
            new[] {
                // 调查条件决定新探索机会：书落下、纸张显露后才开放核心线索。
                new DiscoveryRule("inspect.plant", Plant, "盆栽", "小盆栽向书架倾斜，新叶尚未完全舒展。\n生长，是成为更多的可能。已收集：盆栽、生长。", None, new[] { Clue("plant"), Clue("growth") }),
                // 描述可以引导观察其他物体，但不能代替玩家点击并授予那个物体。
                new DiscoveryRule("inspect.book", Book, "书", "落下的旅行书翻开，描绘着没有边界的地平线。\n远方，是房间容不下的距离。铅笔箭头指向窗户。已收集：书、远方。请亲自观察窗户。", new[] { BookInspectable }, new[] { Clue("book"), Clue("far") }, "书卡在书架上方，够不到。或许有什么能把它碰下来。"),
                new DiscoveryRule("inspect.window", Window, "窗户", "玻璃朦胧。房间止步于此，景色却未必如此。\n已收集：窗户。什么意义能打开它？", None, new[] { Clue("window") }),
                new DiscoveryRule("inspect.painting", Painting, "画", "一幅干燥而寂静的画，色带被木框圈住。\n已收集：画。画面下沿似乎还没有完成。", None, new[] { Clue("painting") }),
                new DiscoveryRule("inspect.paper", Paper, "纸张", "风吹出了画框后的习作：潮汐、盐，以及不停流动的辽阔水面。\n海，是不愿被边界束缚的水。已收集：海。请亲自观察画。", new[] { PaperRevealed }, new[] { Clue("sea") }, "纸张压在画框后面，现在还看不清。")
            },
            new[] {
                // 玩家只建立三个解释；后续风、水和跨物体改变均交给自动反应。
                new SemanticRecipe("recipe.plant.growth", new[] { Clue("plant"), Clue("growth") }, None, new[] { PlantGrown }),
                new SemanticRecipe("recipe.window.far", new[] { Clue("window"), Clue("far") }, None, new[] { WindowFar }),
                new SemanticRecipe("recipe.painting.sea", new[] { Clue("painting"), Clue("sea") }, None, new[] { PaintingSea })
            },
            new[] {
                // 反应可链式产出事实，引擎会全量扫描到稳定，无需依赖此处的顺序。
                new ReactionRule("plant-knocks-book", new[] { PlantGrown }, new[] { BookFallen }),
                new ReactionRule("book-becomes-inspectable", new[] { BookFallen }, new[] { BookInspectable }),
                new ReactionRule("wind-enters-room", new[] { PlantGrown, WindowFar }, new[] { WindEntering }),
                new ReactionRule("wind-reveals-paper", new[] { WindEntering }, new[] { PaperRevealed }),
                new ReactionRule("painting-leaks-water", new[] { PaintingSea }, new[] { WaterPresent }),
                // 海水联系已经成立的旧解释，产生玩家没有再次指定的物体变化。
                new ReactionRule("plant-becomes-aquatic", new[] { WaterPresent, PlantGrown }, new[] { PlantAquatic }),
                new ReactionRule("window-becomes-ocean", new[] { WaterPresent, WindowFar }, new[] { WindowOcean }),
                new ReactionRule("room-remembers", new[] { PlantAquatic, WindowOcean, PaintingSea }, new[] { Complete })
            });
    }
}
