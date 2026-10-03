using System;
namespace Game.Domain
{
    public static class DemoRules
    {
        public static readonly ObjectId Plant = new ObjectId("plant");
        public static readonly ObjectId Book = new ObjectId("book");
        public static readonly ObjectId Window = new ObjectId("window");
        public static readonly ConceptId Growth = new ConceptId("growth");
        public static readonly ConceptId Far = new ConceptId("far");
        public static readonly FactId PlantGrown = new FactId("plant.grown");
        public static readonly FactId BookFallen = new FactId("book.fallen");
        public static readonly FactId WindowFar = new FactId("window.far");
        public static readonly FactId WindEntering = new FactId("wind.entering");
        public static RuleSet Create() => new RuleSet(
            new[] {
                new ObjectDefinition(Plant, "Plant", "New leaves reach upward. You collect the idea of Growth.", Array.Empty<FactId>(), new[] { Growth }, Array.Empty<ObjectId>()),
                new ObjectDefinition(Book, "Book", "A fallen travel book describes Far horizons beyond the Window.", new[] { BookFallen }, new[] { Far }, new[] { Window }),
                new ObjectDefinition(Window, "Window", "A quiet window. What could exist beyond this room?", Array.Empty<FactId>(), Array.Empty<ConceptId>(), Array.Empty<ObjectId>())
            },
            new[] { new ConceptDefinition(Growth, "Growth"), new ConceptDefinition(Far, "Far") },
            new[] {
                new InterpretationRule(Growth, Plant, Array.Empty<FactId>(), new[] { PlantGrown }),
                new InterpretationRule(Far, Window, Array.Empty<FactId>(), new[] { WindowFar })
            },
            new[] {
                new ReactionRule("plant-knocks-book", new[] { PlantGrown }, new[] { BookFallen }),
                new ReactionRule("wind-enters-room", new[] { PlantGrown, WindowFar }, new[] { WindEntering })
            });
    }
}
