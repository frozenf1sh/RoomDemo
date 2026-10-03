using System;
using System.Collections.Generic;
using Game.Domain;
namespace Game.Application
{
    public sealed class GameSession
    {
        private readonly RuleEngine engine;
        public RuleSet Rules { get; }
        public GameState State { get; } = new GameState();
        public event Action<DomainEvent> EventRaised;
        public GameSession(RuleSet rules) { Rules = rules; engine = new RuleEngine(rules); }
        public IReadOnlyList<DomainEvent> Inspect(ObjectId id) => Dispatch(new InspectObject(id));
        public IReadOnlyList<DomainEvent> Interpret(ConceptId concept, ObjectId target) => Dispatch(new Interpret(concept, target));
        private IReadOnlyList<DomainEvent> Dispatch(ICommand command)
        {
            var events = engine.Execute(State, command);
            foreach (var e in events) EventRaised?.Invoke(e);
            return events;
        }
    }
}
