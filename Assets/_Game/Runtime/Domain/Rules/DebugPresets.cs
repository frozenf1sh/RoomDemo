using System;
using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    public sealed class DebugPreset
    {
        public string Id { get; }
        public IReadOnlyList<ClueId> Known { get; }
        public IReadOnlyList<FactId> Facts { get; }
        public IReadOnlyList<string> CompletedRecipes { get; }
        public DebugPreset(string id, ClueId[] known, FactId[] facts, string[] recipes)
        { Id = id; Known = Array.AsReadOnly((ClueId[])known.Clone()); Facts = Array.AsReadOnly((FactId[])facts.Clone()); CompletedRecipes = Array.AsReadOnly((string[])recipes.Clone()); }
    }
    public static class DebugPresets
    {
        private static ClueId[] Clues(params string[] values) => values.Select(DemoRules.Clue).ToArray();
        public static readonly DebugPreset AfterPlant = new DebugPreset("After Plant", Clues("plant", "growth"), new[] { DemoRules.PlantGrown }, new[] { "recipe.plant.growth" });
        public static readonly DebugPreset AfterWindow = new DebugPreset("After Window", Clues("plant", "growth", "book", "far", "window"), new[] { DemoRules.PlantGrown, DemoRules.WindowFar }, new[] { "recipe.plant.growth", "recipe.window.far" });
        public static readonly DebugPreset BeforeSea = new DebugPreset("Before Sea", Clues("plant", "growth", "book", "far", "window", "painting", "sea"), new[] { DemoRules.PlantGrown, DemoRules.WindowFar }, new[] { "recipe.plant.growth", "recipe.window.far" });
        public static readonly DebugPreset Emergence = new DebugPreset("Emergence", BeforeSea.Known.ToArray(), new[] { DemoRules.PlantGrown, DemoRules.WindowFar, DemoRules.PaintingSea }, new[] { "recipe.plant.growth", "recipe.window.far", "recipe.painting.sea" });
        public static IReadOnlyList<DebugPreset> All { get; } = Array.AsReadOnly(new[] { AfterPlant, AfterWindow, BeforeSea, Emergence });
    }
}
