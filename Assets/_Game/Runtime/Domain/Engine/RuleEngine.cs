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
        public IReadOnlyList<DomainEvent> DebugGrantClues(GameState state, IReadOnlyList<ClueId> clues)
        {
            if (clues.Any(c => !rules.Clues.Any(d => d.Id == c))) throw new ArgumentException("Unknown debug clue.");
            var events = new List<DomainEvent>(); foreach (var clue in clues) if (state.Discover(clue)) events.Add(new ClueDiscovered(clue)); return events.AsReadOnly();
        }
        public IReadOnlyList<DomainEvent> DebugAddFacts(GameState state, IReadOnlyList<FactId> facts)
        {
            var known = rules.Recipes.SelectMany(x => x.Produces.Concat(x.Requires)).Concat(rules.Reactions.SelectMany(x => x.Produces.Concat(x.Requires))).Concat(rules.Discoveries.SelectMany(x => x.Requires)).ToHashSet();
            if (facts.Any(f => !known.Contains(f))) throw new ArgumentException("Unknown debug fact.");
            var events = new List<DomainEvent>(); AddFacts(state, facts, events); EvaluateReactions(state, events); return events.AsReadOnly();
        }
        public void RestorePreset(GameState state, DebugPreset preset)
        {
            if (state.KnownClues.Count > 0 || state.Facts.Count > 0) throw new InvalidOperationException("Restore a preset into a fresh session.");
            if (preset.CompletedRecipes.Any(id => !rules.Recipes.Any(r => r.Id == id))) throw new ArgumentException("Unknown preset recipe.");
            // Seed facts through the same closure as play. Restore emits no transition events.
            DebugGrantClues(state, preset.Known); DebugAddFacts(state, preset.Facts);
            foreach (var id in preset.CompletedRecipes)
            {
                var recipe = rules.Recipes.Single(r => r.Id == id);
                if (!recipe.Produces.All(state.HasFact)) throw new ArgumentException("Preset recipes must agree with their produced facts.");
                state.Complete(id);
            }
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
