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
            else if (command is Combine combine) accepted = Combine(state, combine.Clues, events);
            else if (command is Interpret interpret) accepted = Interpret(state, interpret, events);
            else throw new ArgumentException("Unsupported command.", nameof(command));
            if (accepted) EvaluateReactions(state, events);
            return events.AsReadOnly();
        }
        private bool Inspect(GameState state, InspectObject command, List<DomainEvent> events)
        {
            var definition = rules.Discoveries.FirstOrDefault(x => x.Source == command.Object);
            if (definition == null || !definition.Requires.All(state.HasFact))
            { events.Add(new InspectionFailed(command.Object, definition == null ? "Unknown object." : definition.LockedDescription)); return false; }
            foreach (var clue in definition.Grants)
            {
                if (!state.Discover(clue)) continue;
                events.Add(new ClueDiscovered(clue));
                var category = rules.Clues.Single(x => x.Id == clue).Category;
                if (category == ClueCategory.Object) events.Add(new ObjectDiscovered(new ObjectId(clue.Value)));
                if (category == ClueCategory.Concept) events.Add(new ConceptDiscovered(new ConceptId(clue.Value)));
            }
            events.Add(new ObjectInspected(command.Object, definition.Description));
            return true;
        }
        private bool Interpret(GameState state, Interpret command, List<DomainEvent> events)
        {
            var modern = new List<DomainEvent>();
            bool accepted = Combine(state, new[] { new ClueId(command.Concept.Value), new ClueId(command.Target.Value) }, modern);
            if (!accepted) events.Add(new InterpretationFailed(command.Concept, command.Target, ((CombinationFailed)modern[0]).Reason));
            else
            {
                events.Add(new InterpretationSucceeded(command.Concept, command.Target));
                events.AddRange(modern.Skip(1));
            }
            return accepted;
        }
        private bool Combine(GameState state, IReadOnlyList<ClueId> clues, List<DomainEvent> events)
        {
            var recipe = rules.Recipes.SingleOrDefault(x => x.Matches(clues));
            string reason = clues.Any(x => !state.Knows(x)) ? "Inspect the room to collect these clues first."
                : recipe == null ? "No new interpretation forms."
                : !recipe.Requires.All(state.HasFact) ? "Required world facts are missing."
                : state.HasCompleted(recipe.Id) ? "This interpretation already belongs to the room." : null;
            if (reason != null) { events.Add(new CombinationFailed(reason)); return false; }
            state.Complete(recipe.Id); events.Add(new CombinationSucceeded(recipe.Id)); AddFacts(state, recipe.Produces, events);
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
