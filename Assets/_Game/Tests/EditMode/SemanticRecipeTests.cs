using System;
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    public sealed class SemanticRecipeTests
    {
        private static ClueId C(string value) => new ClueId(value);
        [TestCase(false)] [TestCase(true)] public void PairIsUnorderedAndRecorded(bool reverse)
        {
            var s = new GameSession(DemoRules.Create()); s.Inspect(DemoRules.Plant);
            var pair = new[] { C("plant"), C("growth") }; if (reverse) Array.Reverse(pair);
            Assert.That(s.Combine(pair).First(), Is.TypeOf<CombinationSucceeded>());
            Assert.That(s.State.HasFact(DemoRules.PlantGrown)); Assert.That(s.State.CompletedRecipes.Count, Is.EqualTo(1));
        }
        [Test] public void ExtraOrDuplicateInputCannotMatchPair()
        {
            var s = new GameSession(DemoRules.Create()); s.Inspect(DemoRules.Plant); s.Inspect(DemoRules.Window);
            Assert.That(s.Combine(new[] { C("plant"), C("growth"), C("window") }).Single(), Is.TypeOf<CombinationFailed>());
            Assert.That(s.Combine(new[] { C("plant"), C("growth"), C("growth") }).Single(), Is.TypeOf<CombinationFailed>());
            Assert.That(s.State.Facts, Is.Empty); Assert.That(s.State.CompletedRecipes, Is.Empty); Assert.That(s.State.KnownClues.Count, Is.EqualTo(3));
        }
        [Test] public void ThreeInputRecipeNeedsOnlyContentData()
        {
            var clues = new[] { new ClueDefinition(C("mirror"), "Mirror", ClueCategory.Object), new ClueDefinition(C("reflection"), "Reflection", ClueCategory.Concept), new ClueDefinition(C("now"), "Now", ClueCategory.Modifier) };
            var inputs = clues.Select(x => x.Id).ToArray(); var fact = new FactId("mirror.other_side");
            var rules = new RuleSet(clues, new[] { new DiscoveryRule("mirror", new ObjectId("mirror"), "Mirror", "", Array.Empty<FactId>(), inputs) },
                new[] { new SemanticRecipe("mirror.reflection.now", inputs, Array.Empty<FactId>(), new[] { fact }) }, Array.Empty<ReactionRule>());
            var s = new GameSession(rules); s.Inspect(new ObjectId("mirror"));
            Assert.That(s.Combine(inputs.Take(2).ToArray()).Single(), Is.TypeOf<CombinationFailed>());
            s.Combine(inputs.Reverse().ToArray()); Assert.That(s.State.HasFact(fact));
        }
        [Test] public void SameInputsCannotSelectTwoRecipes()
        {
            var d = DemoRules.Create(); var first = d.Recipes[0];
            Assert.Throws<ArgumentException>(() => new RuleSet(d.Clues.ToArray(), d.Discoveries.ToArray(), new[] { first,
                new SemanticRecipe("ambiguous", first.Inputs.Reverse().ToArray(), first.Requires, first.Produces) }, d.Reactions.ToArray()));
        }
        [Test] public void RepeatedRecipeDoesNotEmitFactsOrConsumeClues()
        {
            var s = new GameSession(DemoRules.Create()); s.Inspect(DemoRules.Plant); var inputs = new[] { C("plant"), C("growth") };
            s.Combine(inputs); var known = s.State.KnownClues.ToArray(); var facts = s.State.Facts.ToArray();
            Assert.That(s.Combine(inputs).Single(), Is.TypeOf<CombinationFailed>());
            Assert.That(s.State.KnownClues, Is.EquivalentTo(known)); Assert.That(s.State.Facts, Is.EquivalentTo(facts));
        }
    }
}
