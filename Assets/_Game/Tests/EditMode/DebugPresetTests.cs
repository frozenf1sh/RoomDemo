using System;
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    public sealed class DebugPresetTests
    {
        [Test] public void BeforeSeaCanCompleteThroughTheRealRecipe()
        {
            var s = GameSession.FromPreset(DemoRules.Create(), DebugPresets.BeforeSea);
            Assert.That(s.State.Facts.Count, Is.EqualTo(6)); Assert.That(s.State.HasFact(DemoRules.PaintingSea), Is.False);
            Assert.That(s.State.CompletedRecipes.Count, Is.EqualTo(2)); Assert.That(s.State.FiredReactions.Count, Is.EqualTo(4));
            s.Combine(new[] { DemoRules.Clue("sea"), DemoRules.Clue("painting") }); Assert.That(s.State.HasFact(DemoRules.Complete));
        }
        [Test] public void EmergencePresetRunsClosureAndAgreesWithRecipeHistory()
        {
            var s = GameSession.FromPreset(DemoRules.Create(), DebugPresets.Emergence);
            Assert.That(s.State.Facts.Count, Is.EqualTo(11)); Assert.That(s.State.CompletedRecipes.Count, Is.EqualTo(3)); Assert.That(s.State.FiredReactions.Count, Is.EqualTo(8));
            Assert.That(s.DebugAddFact(DemoRules.PaintingSea), Is.Empty);
        }
        [Test] public void GrantAllOnlyAddsCluesWithoutSolvingTheRoom()
        {
            var s = new GameSession(DemoRules.Create()); s.DebugGrantAllClues(); Assert.That(s.State.KnownClues.Count, Is.EqualTo(7));
            Assert.That(s.State.Facts, Is.Empty); Assert.That(s.State.CompletedRecipes, Is.Empty); Assert.That(s.DebugGrantAllClues(), Is.Empty);
            Assert.That(s.Combine(new[] { DemoRules.Clue("plant"), DemoRules.Clue("growth"), DemoRules.Clue("sea") }).Single(), Is.TypeOf<CombinationFailed>());
        }
        [Test] public void AddingFactEvaluatesClosureAndRejectsTyposBeforeMutation()
        {
            var s = new GameSession(DemoRules.Create()); Assert.Throws<ArgumentException>(() => s.DebugAddFact(new FactId("plant.gorwn"))); Assert.That(s.State.Facts, Is.Empty);
            s.DebugAddFact(DemoRules.PlantGrown); Assert.That(s.State.HasFact(DemoRules.BookInspectable)); Assert.That(s.State.FiredReactions.Count, Is.EqualTo(2));
        }
    }
}
