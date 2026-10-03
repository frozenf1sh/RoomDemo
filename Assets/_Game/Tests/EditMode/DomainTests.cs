using System;
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    public sealed class DomainTests
    {
        private GameSession session;
        [SetUp] public void Setup() { session = new GameSession(DemoRules.Create()); }
        private void Grow() { session.Inspect(DemoRules.Plant); session.Interpret(DemoRules.Growth, DemoRules.Plant); }
        [Test] public void GrowthPlantProducesPlantGrown() { Grow(); Assert.That(session.State.HasFact(DemoRules.PlantGrown)); }
        [Test] public void PlantGrownAutomaticallyDropsBook() { Grow(); Assert.That(session.State.HasFact(DemoRules.BookFallen)); }
        [Test] public void TwoExistingStatesAutomaticallyProduceWind()
        { Grow(); session.Inspect(DemoRules.Book); var e = session.Interpret(DemoRules.Far, DemoRules.Window); Assert.That(session.State.HasFact(DemoRules.WindEntering)); Assert.That(e.OfType<ReactionTriggered>().Single().Id, Is.EqualTo("wind-enters-room")); }
        [Test] public void ReactionsFireOnlyOnce()
        { Grow(); var e = session.Interpret(DemoRules.Growth, DemoRules.Plant); Assert.That(e.OfType<ReactionTriggered>(), Is.Empty); Assert.That(session.State.FiredReactions.Count, Is.EqualTo(1)); }
        [Test] public void InvalidInterpretationCannotChangeFacts()
        { session.Inspect(DemoRules.Plant); session.Inspect(DemoRules.Window); var e = session.Interpret(DemoRules.Growth, DemoRules.Window); Assert.That(e.Single(), Is.TypeOf<InterpretationFailed>()); Assert.That(session.State.Facts, Is.Empty); }
        [Test] public void UndiscoveredConceptOrObjectCannotBeInterpreted()
        { Assert.That(session.Interpret(DemoRules.Growth, DemoRules.Plant).Single(), Is.TypeOf<InterpretationFailed>()); Assert.That(session.State.Facts, Is.Empty); }
        [Test] public void BookIsGatedUntilItFalls()
        { Assert.That(session.Inspect(DemoRules.Book).Single(), Is.TypeOf<InspectionFailed>()); Assert.That(session.State.KnownConcepts, Is.Empty); Grow(); session.Inspect(DemoRules.Book); Assert.That(session.State.Knows(DemoRules.Far)); Assert.That(session.State.Knows(DemoRules.Window)); }
        [Test] public void RepeatedInspectionDoesNotRepeatDiscoveryEvents()
        { session.Inspect(DemoRules.Plant); var e = session.Inspect(DemoRules.Plant); Assert.That(e.OfType<ObjectDiscovered>(), Is.Empty); Assert.That(e.OfType<ConceptDiscovered>(), Is.Empty); }
        [Test] public void EventsAreOrderedFromCommandThroughReaction()
        { session.Inspect(DemoRules.Plant); var e = session.Interpret(DemoRules.Growth, DemoRules.Plant); Assert.That(e.Select(x => x.GetType()), Is.EqualTo(new[] { typeof(InterpretationSucceeded), typeof(FactAdded), typeof(ReactionTriggered), typeof(FactAdded) })); }
        [Test] public void ReverseOrderedChainAndCycleReachStableStateWithoutDuplicateFacts()
        {
            var a = new FactId("a"); var b = new FactId("b"); var c = new FactId("c"); var demo = DemoRules.Create();
            var rules = new RuleSet(demo.Objects.ToArray(), demo.Concepts.ToArray(), new[] { new InterpretationRule(DemoRules.Growth, DemoRules.Plant, Array.Empty<FactId>(), new[] { a }) },
                new[] { new ReactionRule("second", new[] { b }, new[] { c }), new ReactionRule("first", new[] { a }, new[] { b }), new ReactionRule("cycle", new[] { c }, new[] { a }) });
            var s = new GameSession(rules); s.Inspect(DemoRules.Plant); var events = s.Interpret(DemoRules.Growth, DemoRules.Plant);
            Assert.That(s.State.Facts.Count, Is.EqualTo(3)); Assert.That(s.State.FiredReactions.Count, Is.EqualTo(3)); Assert.That(events.OfType<FactAdded>().Count(), Is.EqualTo(3));
        }
        [Test] public void MissingInterpretationPrerequisiteDoesNotApply()
        {
            var demo = DemoRules.Create(); var rules = new RuleSet(demo.Objects.ToArray(), demo.Concepts.ToArray(), new[] { new InterpretationRule(DemoRules.Growth, DemoRules.Plant, new[] { DemoRules.WindowFar }, new[] { DemoRules.PlantGrown }) }, Array.Empty<ReactionRule>());
            var s = new GameSession(rules); s.Inspect(DemoRules.Plant); Assert.That(s.Interpret(DemoRules.Growth, DemoRules.Plant).Single(), Is.TypeOf<InterpretationFailed>()); Assert.That(s.State.Facts, Is.Empty);
        }
        [Test] public void ApplicationPublishesExactlyReturnedEvents()
        { var received = new System.Collections.Generic.List<DomainEvent>(); session.EventRaised += received.Add; var e = session.Inspect(DemoRules.Plant); Assert.That(received, Is.EqualTo(e)); }
        [Test] public void DuplicateReactionIdsAreRejected()
        { var d = DemoRules.Create(); Assert.Throws<ArgumentException>(() => new RuleSet(d.Objects.ToArray(), d.Concepts.ToArray(), d.Interpretations.ToArray(), new[] { d.Reactions[0], d.Reactions[0] })); }
        [Test] public void StateCollectionsCannotBeMutatedExternally()
        { session.Inspect(DemoRules.Plant); var snapshot = (System.Collections.Generic.ICollection<ObjectId>)session.State.KnownObjects; Assert.Throws<NotSupportedException>(() => snapshot.Clear()); Assert.That(session.State.Knows(DemoRules.Plant)); }
    }
}
