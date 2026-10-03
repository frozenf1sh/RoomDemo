using System;
namespace Game.Domain
{
    public static class DemoRules
    {
        public static readonly ObjectId Plant = new ObjectId("plant"), Book = new ObjectId("book"), Window = new ObjectId("window"), Painting = new ObjectId("painting"), Paper = new ObjectId("paper");
        public static readonly ConceptId Growth = new ConceptId("growth"), Far = new ConceptId("far"), Sea = new ConceptId("sea");
        public static readonly FactId PlantGrown = new FactId("plant.grown"), BookFallen = new FactId("book.fallen"), BookInspectable = new FactId("book.inspectable"),
            WindowFar = new FactId("window.far"), WindEntering = new FactId("wind.entering"), PaperRevealed = new FactId("paper.revealed"), PaintingSea = new FactId("painting.sea"),
            WaterPresent = new FactId("water.present"), PlantAquatic = new FactId("plant.aquatic"), WindowOcean = new FactId("window.ocean"), Complete = new FactId("vertical_slice.complete");
        public static ClueId Clue(string value) => new ClueId(value);
        private static readonly FactId[] None = Array.Empty<FactId>();
        public static RuleSet Create() => new RuleSet(
            new[] {
                new ClueDefinition(Clue("plant"), "Plant", ClueCategory.Object), new ClueDefinition(Clue("book"), "Book", ClueCategory.Object),
                new ClueDefinition(Clue("window"), "Window", ClueCategory.Object), new ClueDefinition(Clue("painting"), "Painting", ClueCategory.Object),
                new ClueDefinition(Clue("growth"), "Growth", ClueCategory.Concept), new ClueDefinition(Clue("far"), "Far", ClueCategory.Concept), new ClueDefinition(Clue("sea"), "Sea", ClueCategory.Concept)
            },
            new[] {
                new DiscoveryRule("inspect.plant", Plant, "Plant", "A small plant leans toward the shelf. Its newest leaf is still unfolding.\nGrowth: a promise of becoming more than it is. Collected Plant + Growth.", None, new[] { Clue("plant"), Clue("growth") }),
                new DiscoveryRule("inspect.book", Book, "Book", "The fallen travel book opens to a horizon without an edge.\nFar: distance that a room cannot contain. A pencilled arrow points to the Window. Collected Book + Far + Window.", new[] { BookInspectable }, new[] { Clue("book"), Clue("far"), Clue("window") }, "The book is wedged above the shelf. Something would have to dislodge it."),
                new DiscoveryRule("inspect.window", Window, "Window", "A pane of clouded glass. The room ends here, but perhaps the view does not.\nCollected Window. What meaning could open it?", None, new[] { Clue("window") }),
                new DiscoveryRule("inspect.painting", Painting, "Painting", "A dry, silent painting: bands of pigment held inside a wooden frame.\nCollected Painting. The lower edge feels strangely unfinished.", None, new[] { Clue("painting") }),
                new DiscoveryRule("inspect.paper", Paper, "Paper", "Wind has uncovered a study beneath the frame: tides, salt, an endless moving surface.\nSea: water that refuses its boundaries. Collected Sea + Painting.", new[] { PaperRevealed }, new[] { Clue("sea"), Clue("painting") }, "A sheet is pinned behind the frame. You cannot read it yet.")
            },
            new[] {
                new SemanticRecipe("recipe.plant.growth", new[] { Clue("plant"), Clue("growth") }, None, new[] { PlantGrown }),
                new SemanticRecipe("recipe.window.far", new[] { Clue("window"), Clue("far") }, None, new[] { WindowFar })
            },
            new[] {
                new ReactionRule("plant-knocks-book", new[] { PlantGrown }, new[] { BookFallen }),
                new ReactionRule("book-becomes-inspectable", new[] { BookFallen }, new[] { BookInspectable }),
                new ReactionRule("wind-enters-room", new[] { PlantGrown, WindowFar }, new[] { WindEntering })
            });
    }
}
