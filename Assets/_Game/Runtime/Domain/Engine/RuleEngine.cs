using System;
using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    public sealed class RuleEngine
    {
        private readonly RuleSet rules;
        public RuleEngine(RuleSet rules) { this.rules = rules ?? throw new ArgumentNullException(nameof(rules)); }
        public IReadOnlyList<DomainEvent> Execute(GameState state, ICommand command)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var events = new List<DomainEvent>();
            bool accepted;
            if (command is InspectObject inspect) accepted = Inspect(state, inspect, events);
            else if (command is Interpret interpret) accepted = Interpret(state, interpret, events);
            else throw new ArgumentException("Unsupported command.", nameof(command));
            if (accepted) EvaluateReactions(state, events);
            return events.AsReadOnly();
        }
        private bool Inspect(GameState state, InspectObject command, List<DomainEvent> events)
        {
            var definition = rules.Objects.FirstOrDefault(x => x.Id == command.Object);
            if (definition == null || !definition.Requires.All(state.HasFact))
            {
                events.Add(new InspectionFailed(command.Object, definition == null ? "Unknown object." : "The book is out of reach. Let the world change first."));
                return false;
            }
            Discover(command.Object, state, events);
            foreach (var id in definition.RevealsObjects) Discover(id, state, events);
            foreach (var id in definition.Concepts) if (state.Discover(id)) events.Add(new ConceptDiscovered(id));
            events.Add(new ObjectInspected(command.Object, definition.Description));
            return true;
        }
        private static void Discover(ObjectId id, GameState state, List<DomainEvent> events)
        { if (state.Discover(id)) events.Add(new ObjectDiscovered(id)); }
        private bool Interpret(GameState state, Interpret command, List<DomainEvent> events)
        {
            var rule = rules.Interpretations.FirstOrDefault(x => x.Concept == command.Concept && x.Target == command.Target);
            string reason = !state.Knows(command.Concept) || !state.Knows(command.Target) ? "Inspect objects to discover the concept and target first."
                : rule == null ? "This interpretation has no meaning here."
                : !rule.Requires.All(state.HasFact) ? "Required world facts are missing."
                : rule.Produces.All(state.HasFact) ? "This interpretation is already present in the world." : null;
            if (reason != null) { events.Add(new InterpretationFailed(command.Concept, command.Target, reason)); return false; }
            events.Add(new InterpretationSucceeded(command.Concept, command.Target));
            AddFacts(state, rule.Produces, events);
            return true;
        }
        private void EvaluateReactions(GameState state, List<DomainEvent> events)
        {
            // Each pass fires at least one previously unfired rule, so even cyclic fact rules terminate.
            bool fired;
            do
            {
                fired = false;
                foreach (var rule in rules.Reactions)
                {
                    if (state.HasFired(rule.Id) || !rule.Requires.All(state.HasFact)) continue;
                    state.Fire(rule.Id);
                    events.Add(new ReactionTriggered(rule.Id));
                    AddFacts(state, rule.Produces, events);
                    fired = true;
                }
            } while (fired);
        }
        private static void AddFacts(GameState state, IEnumerable<FactId> facts, List<DomainEvent> events)
        { foreach (var fact in facts) if (state.AddFact(fact)) events.Add(new FactAdded(fact)); }
    }
}
