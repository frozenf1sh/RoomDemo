// =============================================================================
// 文件：DomainTests.cs
// 职责：回归验证第一版观察、解释、反应和只读状态协议。
// 边界：纯逻辑测试不加载 Scene，证明场景损坏不影响规则验收。
// =============================================================================
using System;
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    /// <summary>回归验证第一版观察、解释、反应和只读状态协议。</summary>
    public sealed class DomainTests
    {
        private GameSession session;
        /// <summary>每个领域测试创建全新的会话，避免状态相互污染。</summary>
        [SetUp] public void Setup() { session = new GameSession(DemoRules.Create()); }
        /// <summary>通过真实观察与兼容解释建立植物生长。</summary>
        private void Grow() { session.Inspect(DemoRules.Plant); session.Interpret(DemoRules.Growth, DemoRules.Plant); }
        /// <summary>生长解释产生植物生长事实。</summary>
        [Test] public void GrowthPlantProducesPlantGrown() { Grow(); Assert.That(session.State.HasFact(DemoRules.PlantGrown)); }
        /// <summary>植物生长后书落下属于自动反应。</summary>
        [Test] public void PlantGrownAutomaticallyDropsBook() { Grow(); Assert.That(session.State.HasFact(DemoRules.BookFallen)); }
        /// <summary>旧植物状态与新窗景共同触发风及纸张显露。</summary>
        [Test] public void TwoExistingStatesAutomaticallyProduceWind()
        { Grow(); session.Inspect(DemoRules.Book); session.Inspect(DemoRules.Window); var e = session.Interpret(DemoRules.Far, DemoRules.Window); Assert.That(session.State.HasFact(DemoRules.WindEntering)); Assert.That(e.OfType<ReactionTriggered>().Select(x => x.Id), Is.EquivalentTo(new[] { "wind-enters-room", "wind-reveals-paper" })); }
        /// <summary>重复解释不会重复触发反应。</summary>
        [Test] public void ReactionsFireOnlyOnce()
        { Grow(); var e = session.Interpret(DemoRules.Growth, DemoRules.Plant); Assert.That(e.OfType<ReactionTriggered>(), Is.Empty); Assert.That(session.State.FiredReactions.Count, Is.EqualTo(2)); }
        /// <summary>非法二元解释不能产生任何事实。</summary>
        [Test] public void InvalidInterpretationCannotChangeFacts()
        { session.Inspect(DemoRules.Plant); session.Inspect(DemoRules.Window); var e = session.Interpret(DemoRules.Growth, DemoRules.Window); Assert.That(e.Single(), Is.TypeOf<InterpretationFailed>()); Assert.That(session.State.Facts, Is.Empty); }
        /// <summary>未知碎片不能参与合法解释。</summary>
        [Test] public void UndiscoveredConceptOrObjectCannotBeInterpreted()
        { Assert.That(session.Interpret(DemoRules.Growth, DemoRules.Plant).Single(), Is.TypeOf<InterpretationFailed>()); Assert.That(session.State.Facts, Is.Empty); }
        /// <summary>书的核心线索被事实门槛保护。</summary>
        [Test] public void BookIsGatedUntilItFalls()
        { Assert.That(session.Inspect(DemoRules.Book).Single(), Is.TypeOf<InspectionFailed>()); Assert.That(session.State.KnownConcepts, Is.Empty); Grow(); session.Inspect(DemoRules.Book); Assert.That(session.State.Knows(DemoRules.Far)); Assert.That(session.State.Knows(DemoRules.Window), Is.False); }
        /// <summary>重复观察不重复发放发现事件。</summary>
        [Test] public void RepeatedInspectionDoesNotRepeatDiscoveryEvents()
        { session.Inspect(DemoRules.Plant); var e = session.Inspect(DemoRules.Plant); Assert.That(e.OfType<ObjectDiscovered>(), Is.Empty); Assert.That(e.OfType<ConceptDiscovered>(), Is.Empty); }
        /// <summary>事件顺序从命令结果到事实与自动反应。</summary>
        [Test] public void EventsAreOrderedFromCommandThroughReaction()
        { session.Inspect(DemoRules.Plant); var e = session.Interpret(DemoRules.Growth, DemoRules.Plant); Assert.That(e.Select(x => x.GetType()), Is.EqualTo(new[] { typeof(InterpretationSucceeded), typeof(FactAdded), typeof(ReactionTriggered), typeof(FactAdded), typeof(ReactionTriggered), typeof(FactAdded) })); }
        /// <summary>逆序规则与循环仍能到达稳定且去重的闭包。</summary>
        [Test] public void ReverseOrderedChainAndCycleReachStableStateWithoutDuplicateFacts()
        {
            var a = new FactId("a"); var b = new FactId("b"); var c = new FactId("c"); var demo = DemoRules.Create();
            var rules = new RuleSet(demo.Objects.ToArray(), demo.Concepts.ToArray(), new[] { new InterpretationRule(DemoRules.Growth, DemoRules.Plant, Array.Empty<FactId>(), new[] { a }) },
                new[] { new ReactionRule("second", new[] { b }, new[] { c }), new ReactionRule("first", new[] { a }, new[] { b }), new ReactionRule("cycle", new[] { c }, new[] { a }) });
            var s = new GameSession(rules); s.Inspect(DemoRules.Plant); var events = s.Interpret(DemoRules.Growth, DemoRules.Plant);
            // 以下断言检查可观察结果；测试不依赖场景或规则实现的内部字段。
            Assert.That(s.State.Facts.Count, Is.EqualTo(3)); Assert.That(s.State.FiredReactions.Count, Is.EqualTo(3)); Assert.That(events.OfType<FactAdded>().Count(), Is.EqualTo(3));
        }
        /// <summary>未满足的前置事实阻止解释。</summary>
        [Test] public void MissingInterpretationPrerequisiteDoesNotApply()
        {
            var demo = DemoRules.Create(); var rules = new RuleSet(demo.Objects.ToArray(), demo.Concepts.ToArray(), new[] { new InterpretationRule(DemoRules.Growth, DemoRules.Plant, new[] { DemoRules.WindowFar }, new[] { DemoRules.PlantGrown }) }, Array.Empty<ReactionRule>());
            var s = new GameSession(rules); s.Inspect(DemoRules.Plant); Assert.That(s.Interpret(DemoRules.Growth, DemoRules.Plant).Single(), Is.TypeOf<InterpretationFailed>()); Assert.That(s.State.Facts, Is.Empty);
        }
        /// <summary>发布事件与命令返回事件保持同一顺序及内容。</summary>
        [Test] public void ApplicationPublishesExactlyReturnedEvents()
        { var received = new System.Collections.Generic.List<DomainEvent>(); session.EventRaised += received.Add; var e = session.Inspect(DemoRules.Plant); Assert.That(received, Is.EqualTo(e)); }
        /// <summary>内容装载时拒绝重复反应 ID。</summary>
        [Test] public void DuplicateReactionIdsAreRejected()
        { var d = DemoRules.Create(); Assert.Throws<ArgumentException>(() => new RuleSet(d.Objects.ToArray(), d.Concepts.ToArray(), d.Interpretations.ToArray(), new[] { d.Reactions[0], d.Reactions[0] })); }
        /// <summary>只读快照不能被外部清空。</summary>
        [Test] public void StateCollectionsCannotBeMutatedExternally()
        { session.Inspect(DemoRules.Plant); var snapshot = (System.Collections.Generic.ICollection<ObjectId>)session.State.KnownObjects; Assert.Throws<NotSupportedException>(() => snapshot.Clear()); Assert.That(session.State.Knows(DemoRules.Plant)); }
    }
}
