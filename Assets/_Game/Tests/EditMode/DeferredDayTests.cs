// =============================================================================
// 文件：DeferredDayTests.cs
// 职责：验证显式日序、一次性延迟与次日闭包，以及成熟状态直接恢复。
// 边界：没有真实时钟；黑屏、声音和植物尺度不参与事实判定。
// =============================================================================
using System;
using System.Linq;
using Game.Application;
using NUnit.Framework;
namespace Game.Domain.Tests
{
    public sealed class DeferredDayTests
    {
        [Test] public void GrowthWaitsForNextDayThenBookFallsExactlyOnce()
        {
            var game = GameSession.FromPreset(ExplorationRules.Create(), ExplorationPresets.GrowthUnlocked);
            var initial = game.Combine(new[] { DemoRules.Clue("plant"), DemoRules.Clue("growth") });
            Assert.That(game.State.DayIndex, Is.EqualTo(1)); Assert.That(game.State.HasFact(DemoRules.BookFallen), Is.False);
            Assert.That(initial.OfType<ReactionScheduled>().Single().DueDay, Is.EqualTo(2));
            Assert.That(game.CanAdvanceDay, Is.True);
            var next = game.AdvanceDay(); Assert.That(game.State.DayIndex, Is.EqualTo(2));
            Assert.That(game.State.HasFact(ExplorationRules.PlantOvergrown) && game.State.HasFact(DemoRules.BookFallen), Is.True);
            Assert.That(next.OfType<FactAdded>().Count(x => x.Fact == DemoRules.BookFallen), Is.EqualTo(1));
            Assert.That(game.AdvanceDay().OfType<FactAdded>(), Is.Empty); Assert.That(game.State.PendingDeferredEffects, Is.Empty);
        }
        [Test] public void EndingDayBeforeAnyInterpretationFailsWithoutAdvancing()
        {
            var game = new GameSession(ExplorationRules.Create());
            Assert.That(game.AdvanceDay().Single(), Is.TypeOf<DayAdvanceFailed>()); Assert.That(game.State.DayIndex, Is.EqualTo(1));
        }
        [Test] public void RestoreDayTwoDoesNotRescheduleAlreadyAppliedGrowth()
        {
            var game = GameSession.FromPreset(ExplorationRules.Create(), ExplorationPresets.AfterGrowthDay);
            Assert.That(game.State.DayIndex, Is.EqualTo(2)); Assert.That(game.State.HasFact(DemoRules.BookFallen), Is.True);
            Assert.That(game.State.PendingDeferredEffects, Is.Empty); Assert.That(game.CanAdvanceDay, Is.False);
            Assert.That(game.State.HasCompleted("recipe.insight.growth"), Is.True);
        }
        [Test] public void MultiDayEffectIsNotAppliedEarlyAndImmediateClosureStillRuns()
        {
            var a = new FactId("a"); var b = new FactId("b"); var c = new FactId("c");
            var game = new GameSession(new RuleSet(Array.Empty<ClueDefinition>(), Array.Empty<DiscoveryRule>(), Array.Empty<SemanticRecipe>(), new[] {
                // 反应定义顺序刻意颠倒，次日仍应通过正常闭包得到 C。
                new ReactionRule("b-c", new[] { b }, new[] { c }), new ReactionRule("a-b", new[] { a }, new[] { b }, 2) }));
            game.DebugAddFact(a); game.AdvanceDay(); Assert.That(game.State.HasFact(b), Is.False);
            game.AdvanceDay(); Assert.That(game.State.DayIndex, Is.EqualTo(3)); Assert.That(game.State.HasFact(c), Is.True);
            Assert.That(game.State.PendingDeferredEffects, Is.Empty);
        }
        [Test] public void FullExplorationDayAndSeaChainRemainsReachableWithoutAutoGrantingObjects()
        {
            var game = new GameSession(ExplorationRules.Create());
            game.Inspect(DemoRules.Plant); game.ObserveDetail(DemoRules.Plant, "sprout"); game.Inspect(DemoRules.Book); game.ObserveDetail(DemoRules.Book, "toward_light");
            game.Combine(new[] { ExplorationRules.Sprout, ExplorationRules.TowardLight }); game.Combine(new[] { DemoRules.Clue("plant"), DemoRules.Clue("growth") }); game.AdvanceDay();
            game.ObserveDetail(DemoRules.Book, "book_far"); Assert.That(game.State.Knows(DemoRules.Window), Is.False);
            game.Inspect(DemoRules.Window); game.Combine(new[] { DemoRules.Clue("far"), DemoRules.Clue("window") }); game.Inspect(DemoRules.Paper);
            Assert.That(game.State.Knows(DemoRules.Painting), Is.False); game.Inspect(DemoRules.Painting); game.Combine(new[] { DemoRules.Clue("sea"), DemoRules.Clue("painting") });
            Assert.That(game.State.HasFact(DemoRules.Complete), Is.True); Assert.That(game.State.CompletedRecipes.Count, Is.EqualTo(4));
        }
    }
}
