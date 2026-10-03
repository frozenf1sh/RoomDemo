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
