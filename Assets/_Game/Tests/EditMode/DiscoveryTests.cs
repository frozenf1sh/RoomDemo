using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    public sealed class DiscoveryTests
    {
        [Test] public void VisibleBookIsNotInspectableOrKnownBeforeItFalls()
        {
            var s = new GameSession(DemoRules.Create()); Assert.That(s.IsInspectable(DemoRules.Book), Is.False);
            s.Inspect(DemoRules.Book); Assert.That(s.State.Knows(DemoRules.Book), Is.False);
            s.Inspect(DemoRules.Plant); s.Combine(new[] { DemoRules.Clue("plant"), DemoRules.Clue("growth") });
            Assert.That(s.State.HasFact(DemoRules.BookInspectable)); Assert.That(s.IsInspectable(DemoRules.Book));
            Assert.That(s.State.Knows(DemoRules.Book), Is.False); s.Inspect(DemoRules.Book); Assert.That(s.State.Knows(DemoRules.Far));
        }
        [Test] public void HiddenPaperGrantsNothingAndPaintingCanBeObservedEarly()
        {
            var s = new GameSession(DemoRules.Create()); Assert.That(s.Inspect(DemoRules.Paper).Single(), Is.TypeOf<InspectionFailed>());
            Assert.That(s.State.KnownClues, Is.Empty); s.Inspect(DemoRules.Painting);
            Assert.That(s.State.Knows(DemoRules.Painting)); Assert.That(s.State.Knows(DemoRules.Sea), Is.False);
        }
    }
}
