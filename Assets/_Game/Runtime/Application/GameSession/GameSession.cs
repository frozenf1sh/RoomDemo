using System;
using System.Collections.Generic;
using Game.Domain;
namespace Game.Application
{
    public sealed class GameSession
    {
        private readonly RuleEngine engine;
        public RuleSet Rules { get; }
        public GameState State { get; }
        public event Action<DomainEvent> EventRaised;
        public GameSession(RuleSet rules) { Rules = rules; State = new GameState(rules.Clues); engine = new RuleEngine(rules); }
        public IReadOnlyList<DomainEvent> Inspect(ObjectId id) => Dispatch(new InspectObject(id));
        public IReadOnlyList<DomainEvent> Interpret(ConceptId concept, ObjectId target) => Dispatch(new Interpret(concept, target));
        public IReadOnlyList<DomainEvent> Combine(IReadOnlyList<ClueId> clues) => Dispatch(new Combine(clues));
        public bool IsInspectable(ObjectId id) => Rules.IsInspectable(State, id);
        public IReadOnlyList<DomainEvent> DebugGrantAllClues() => Publish(engine.DebugGrantClues(State, System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(Rules.Clues, x => x.Id))));
        public IReadOnlyList<DomainEvent> DebugAddFact(FactId fact) => Publish(engine.DebugAddFacts(State, new[] { fact }));
        public static GameSession FromPreset(RuleSet rules, DebugPreset preset)
        { var session = new GameSession(rules); session.engine.RestorePreset(session.State, preset); return session; }
        private IReadOnlyList<DomainEvent> Publish(IReadOnlyList<DomainEvent> events)
        { foreach (var e in events) EventRaised?.Invoke(e); return events; }
        private IReadOnlyList<DomainEvent> Dispatch(ICommand command)
        {
            var events = engine.Execute(State, command);
            return Publish(events);
        }
    }
}
