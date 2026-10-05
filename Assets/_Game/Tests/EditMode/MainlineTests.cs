// =============================================================================
// 文件：MainlineTests.cs
// 职责：验证正式三天主线和两个独立世界解释，防止线索自动替玩家授予物体。
// 边界：纯领域会话测试；不宣称测试已经证明真人理解或演出品质。
// =============================================================================
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    /// <summary>内容依赖与物体亲点约束的回归。</summary>
    public sealed class MainlineTests
    {
        [Test] public void FreshGameReachesThreeDaysAndSeparateWorldStages()
        {
            var s = new GameSession(MainlineRules.Create());
            s.Inspect(DemoRules.Plant); s.ObserveDetail(DemoRules.Plant, "sprout");
            Assert.That(s.State.Knows(DemoRules.Clue("growth")), Is.False);
            s.Inspect(DemoRules.Book); s.ObserveDetail(DemoRules.Book, "toward_light");
            s.Combine(new[] { ExplorationRules.Sprout, ExplorationRules.TowardLight });
            s.Combine(new[] { DemoRules.Clue("growth"), DemoRules.Clue("plant") });
            Assert.That(s.CanAdvanceDay, Is.True); s.AdvanceDay();
            Assert.That(s.State.DayIndex, Is.EqualTo(2));
            s.ObserveDetail(DemoRules.Book, "book_far");
            Assert.That(s.State.Knows(DemoRules.Clue("window")), Is.False);
            s.Inspect(DemoRules.Window); s.Combine(new[] { DemoRules.Clue("far"), DemoRules.Clue("window") });
            s.Inspect(DemoRules.Paper);
            Assert.That(s.State.Knows(DemoRules.Clue("painting")), Is.False);
            s.Inspect(DemoRules.Painting); s.Combine(new[] { DemoRules.Clue("sea"), DemoRules.Clue("painting") });
            Assert.That(s.State.HasFact(MainlineRules.NightArrived), Is.False);
            s.AdvanceDay(); Assert.That(s.State.DayIndex, Is.EqualTo(3));
            s.Inspect(MainlineRules.Ceiling); s.ObserveDetail(MainlineRules.Ceiling, "night");
            s.ObserveDetail(DemoRules.Window, "boundless");
            s.Combine(new[] { MainlineRules.Night, MainlineRules.Boundless });
            s.Combine(new[] { MainlineRules.Starry, DemoRules.Clue("ceiling") });
            Assert.That(s.State.HasFact(MainlineRules.CeilingStarry), Is.True);
            Assert.That(s.State.HasFact(MainlineRules.WorldForest), Is.False);
            Assert.That(s.State.Knows(MainlineRules.TreeShadow), Is.False);
            s.ObserveDetail(DemoRules.Window, "tree_shadow");
            s.Inspect(MainlineRules.Room); s.ObserveDetail(MainlineRules.Room, "wind_sound");
            s.Combine(new[] { MainlineRules.TreeShadow, MainlineRules.WindSound });
            s.Combine(new[] { MainlineRules.Forest, DemoRules.Clue("room") });
            Assert.That(s.State.HasFact(MainlineRules.Complete), Is.True);
        }
        [Test] public void PreviewIsReadOnlyAndGatedByKnownInputsAndWorldConditions()
        {
            var s = new GameSession(MainlineRules.Create());
            var input = new[] { MainlineRules.Night, MainlineRules.Boundless };
            Assert.That(s.PreviewCombination(input), Is.Null);
            s.DebugGrantAllClues(); Assert.That(s.PreviewCombination(input), Is.Null);
            s.DebugAddFact(MainlineRules.NightArrived);
            Assert.That(s.PreviewCombination(input).Id, Is.EqualTo("recipe.insight.starry"));
            Assert.That(s.State.CompletedRecipes, Is.Empty);
            s.Combine(input); Assert.That(s.PreviewCombination(input), Is.Null);
        }
    }
}
