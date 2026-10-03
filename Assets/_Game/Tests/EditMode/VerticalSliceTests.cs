using System;
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    public sealed class VerticalSliceTests
    {
        private static void Pair(GameSession s, string a, string b) => s.Combine(new[] { DemoRules.Clue(a), DemoRules.Clue(b) });
        private static void BeforeSea(GameSession s)
        {
            s.Inspect(DemoRules.Plant); Pair(s, "plant", "growth"); s.Inspect(DemoRules.Book); Pair(s, "far", "window");
        }
        [Test] public void CompletePlaythroughNeedsOnlyThreePlayerRecipes()
        {
            var s = new GameSession(DemoRules.Create()); BeforeSea(s);
            Assert.That(s.State.HasFact(DemoRules.PaperRevealed)); Assert.That(s.State.HasFact(DemoRules.WaterPresent), Is.False);
            s.Inspect(DemoRules.Paper); Assert.That(s.State.Knows(DemoRules.Sea)); Assert.That(s.State.Knows(DemoRules.Painting));
            var events = s.Combine(new[] { DemoRules.Clue("painting"), DemoRules.Clue("sea") });
            Assert.That(events.OfType<CombinationSucceeded>().Single().RecipeId, Is.EqualTo("recipe.painting.sea"));
            Assert.That(events.OfType<ReactionTriggered>().Select(x => x.Id), Is.EquivalentTo(new[] { "painting-leaks-water", "plant-becomes-aquatic", "window-becomes-ocean", "room-remembers" }));
            Assert.That(s.State.Facts.Count, Is.EqualTo(11)); Assert.That(s.State.HasFact(DemoRules.Complete));
            Assert.That(s.State.CompletedRecipes.Count, Is.EqualTo(3)); Assert.That(s.State.FiredReactions.Count, Is.EqualTo(8));
        }
        [Test] public void PreviouslyKnownPaintingIsNotRediscoveredByPaper()
        {
            var s = new GameSession(DemoRules.Create()); s.Inspect(DemoRules.Painting); BeforeSea(s);
            Assert.That(s.Inspect(DemoRules.Paper).OfType<ClueDiscovered>().Select(x => x.Clue), Is.EqualTo(new[] { DemoRules.Clue("sea") }));
        }
        [Test] public void SeaDoesNotInventAbsentOlderInterpretations()
        {
            var d = DemoRules.Create(); var grant = new DiscoveryRule("test", DemoRules.Paper, "test", "", Array.Empty<FactId>(), d.Clues.Select(x => x.Id).ToArray());
            var s = new GameSession(new RuleSet(d.Clues.ToArray(), new[] { grant }, d.Recipes.ToArray(), d.Reactions.ToArray()));
            s.Inspect(DemoRules.Paper); Pair(s, "sea", "painting");
            Assert.That(s.State.HasFact(DemoRules.WaterPresent)); Assert.That(s.State.HasFact(DemoRules.PlantAquatic), Is.False); Assert.That(s.State.HasFact(DemoRules.WindowOcean), Is.False);
            Pair(s, "plant", "growth"); Assert.That(s.State.HasFact(DemoRules.PlantAquatic)); Assert.That(s.State.HasFact(DemoRules.Complete), Is.False);
            Pair(s, "far", "window"); Assert.That(s.State.HasFact(DemoRules.Complete));
        }
        [Test] public void ShuffledReactionDefinitionsHaveIdenticalClosure()
        {
            var d = DemoRules.Create(); var normal = new GameSession(d);
            var shuffled = new GameSession(new RuleSet(d.Clues.ToArray(), d.Discoveries.ToArray(), d.Recipes.ToArray(), d.Reactions.OrderBy(x => x.Id).Reverse().ToArray()));
            foreach (var s in new[] { normal, shuffled }) { BeforeSea(s); s.Inspect(DemoRules.Paper); Pair(s, "painting", "sea"); }
            Assert.That(shuffled.State.Facts, Is.EquivalentTo(normal.State.Facts)); Assert.That(shuffled.State.FiredReactions, Is.EquivalentTo(normal.State.FiredReactions));
        }
        [Test] public void TwoNodeCycleTerminatesWithUniqueFactsAndReactions()
        {
            var d = DemoRules.Create(); var a = new FactId("a"); var b = new FactId("b");
            var recipe = new SemanticRecipe("test", new[] { DemoRules.Clue("plant"), DemoRules.Clue("growth") }, Array.Empty<FactId>(), new[] { a });
            var s = new GameSession(new RuleSet(d.Clues.ToArray(), d.Discoveries.ToArray(), new[] { recipe }, new[] { new ReactionRule("b-a", new[] { b }, new[] { a }), new ReactionRule("a-b", new[] { a }, new[] { b }) }));
            s.Inspect(DemoRules.Plant); var e = s.Combine(recipe.Inputs);
            Assert.That(s.State.Facts.Count, Is.EqualTo(2)); Assert.That(e.OfType<FactAdded>().Count(), Is.EqualTo(2)); Assert.That(s.State.FiredReactions.Count, Is.EqualTo(2));
        }
    }
}
