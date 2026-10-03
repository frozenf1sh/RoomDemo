// =============================================================================
// 文件：DiscoveryTests.cs
// 职责：验证可见、可调查、已知三种状态的区别。
// 边界：通过领域事实控制新线索机会，不依赖 GameObject 激活状态。
// =============================================================================
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    /// <summary>验证可见、可调查、已知三种状态的区别。</summary>
    public sealed class DiscoveryTests
    {
        /// <summary>所有正式发现规则只授予当前点击物体的物体碎片，防止以后再次跨物体授予。</summary>
        [Test] public void ObjectCluesAlwaysRequireInspectingTheirOwnSource()
        {
            var rules = DemoRules.Create();
            // 约束整个内容表，而非仅检查目前出问题的书和纸张。
            foreach (var discovery in rules.Discoveries)
                foreach (var clue in discovery.Grants.Where(id => rules.Clues.Single(x => x.Id == id).Category == ClueCategory.Object))
                    Assert.That(clue.Value, Is.EqualTo(discovery.Source.Value), $"发现规则 {discovery.Id} 不得代替玩家授予其他物体");
        }
        /// <summary>书提供远方但不提供窗户，未知窗户不能参与解释。</summary>
        [Test] public void BookDoesNotDiscoverWindowAndCombinationWaitsForItsClick()
        {
            var s = new GameSession(DemoRules.Create());
            s.Inspect(DemoRules.Plant);
            s.Combine(new[] { DemoRules.Clue("plant"), DemoRules.Clue("growth") });
            var discovered = s.Inspect(DemoRules.Book).OfType<ClueDiscovered>().Select(x => x.Clue);
            Assert.That(discovered, Is.EquivalentTo(new[] { DemoRules.Clue("book"), DemoRules.Clue("far") }));
            Assert.That(s.State.Knows(DemoRules.Window), Is.False);
            var pair = new[] { DemoRules.Clue("far"), DemoRules.Clue("window") };
            Assert.That(s.Combine(pair).Single(), Is.TypeOf<CombinationFailed>());
            Assert.That(s.State.HasFact(DemoRules.WindowFar), Is.False);
            s.Inspect(DemoRules.Window);
            Assert.That(s.Combine(pair).OfType<CombinationSucceeded>().Count(), Is.EqualTo(1));
        }
        /// <summary>已显露纸张只提供海，画也必须独立点击后才能组合。</summary>
        [Test] public void PaperDoesNotDiscoverPaintingAndCombinationWaitsForItsClick()
        {
            var s = new GameSession(DemoRules.Create());
            s.Inspect(DemoRules.Plant);
            s.Combine(new[] { DemoRules.Clue("plant"), DemoRules.Clue("growth") });
            s.Inspect(DemoRules.Book);
            s.Inspect(DemoRules.Window);
            s.Combine(new[] { DemoRules.Clue("far"), DemoRules.Clue("window") });
            Assert.That(s.Inspect(DemoRules.Paper).OfType<ClueDiscovered>().Select(x => x.Clue), Is.EqualTo(new[] { DemoRules.Clue("sea") }));
            Assert.That(s.State.Knows(DemoRules.Painting), Is.False);
            var pair = new[] { DemoRules.Clue("sea"), DemoRules.Clue("painting") };
            Assert.That(s.Combine(pair).Single(), Is.TypeOf<CombinationFailed>());
            Assert.That(s.State.HasFact(DemoRules.PaintingSea), Is.False);
            s.Inspect(DemoRules.Painting);
            Assert.That(s.Combine(pair).OfType<CombinationSucceeded>().Count(), Is.EqualTo(1));
            Assert.That(s.State.HasFact(DemoRules.Complete), Is.True);
        }
        /// <summary>可见不等于可调查，落下后仍需观察才成为已知线索。</summary>
        [Test] public void VisibleBookIsNotInspectableOrKnownBeforeItFalls()
        {
            var s = new GameSession(DemoRules.Create()); Assert.That(s.IsInspectable(DemoRules.Book), Is.False);
            s.Inspect(DemoRules.Book); Assert.That(s.State.Knows(DemoRules.Book), Is.False);
            s.Inspect(DemoRules.Plant); s.Combine(new[] { DemoRules.Clue("plant"), DemoRules.Clue("growth") });
            // 以下断言检查可观察结果；测试不依赖场景或规则实现的内部字段。
            Assert.That(s.State.HasFact(DemoRules.BookInspectable)); Assert.That(s.IsInspectable(DemoRules.Book));
            Assert.That(s.State.Knows(DemoRules.Book), Is.False); s.Inspect(DemoRules.Book); Assert.That(s.State.Knows(DemoRules.Far));
        }
        /// <summary>纸张显露前无授予，提前观察画不会获得海。</summary>
        [Test] public void HiddenPaperGrantsNothingAndPaintingCanBeObservedEarly()
        {
            var s = new GameSession(DemoRules.Create()); Assert.That(s.Inspect(DemoRules.Paper).Single(), Is.TypeOf<InspectionFailed>());
            Assert.That(s.State.KnownClues, Is.Empty); s.Inspect(DemoRules.Painting);
            Assert.That(s.State.Knows(DemoRules.Painting)); Assert.That(s.State.Knows(DemoRules.Sea), Is.False);
        }
    }
}
