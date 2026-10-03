using System;
using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    public enum ClueCategory { Object, Concept, Modifier, Phrase }
    public sealed class ClueDefinition
    {
        public ClueId Id { get; }
        public string DisplayName { get; }
        public ClueCategory Category { get; }
        public ClueDefinition(ClueId id, string displayName, ClueCategory category)
        { Id = id; DisplayName = displayName; Category = category; }
    }
    public sealed class SemanticRecipe
    {
        public string Id { get; }
        public IReadOnlyList<ClueId> Inputs { get; }
        public IReadOnlyList<FactId> Requires { get; }
        public IReadOnlyList<FactId> Produces { get; }
        public SemanticRecipe(string id, IReadOnlyList<ClueId> inputs, IReadOnlyList<FactId> requires, IReadOnlyList<FactId> produces)
        {
            if (string.IsNullOrWhiteSpace(id) || inputs.Count < 2 || inputs.Count > 3 || inputs.Distinct().Count() != inputs.Count)
                throw new ArgumentException("Recipes need a unique ID and two or three distinct inputs.");
            Id = id; Inputs = Array.AsReadOnly(inputs.ToArray()); Requires = Array.AsReadOnly(requires.ToArray()); Produces = Array.AsReadOnly(produces.ToArray());
        }
        public bool Matches(IReadOnlyList<ClueId> inputs) => inputs != null && inputs.Count == Inputs.Count && inputs.Distinct().Count() == inputs.Count && Inputs.All(inputs.Contains);
    }
    public sealed class DiscoveryRule
    {
        public string Id { get; }
        public ObjectId Source { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string LockedDescription { get; }
        public IReadOnlyList<FactId> Requires { get; }
        public IReadOnlyList<ClueId> Grants { get; }
        public DiscoveryRule(string id, ObjectId source, string displayName, string description, IReadOnlyList<FactId> requires, IReadOnlyList<ClueId> grants, string lockedDescription = "Something must change before you can inspect this.")
        {
            Id = id; Source = source; DisplayName = displayName; Description = description; LockedDescription = lockedDescription;
            Requires = Array.AsReadOnly(requires.ToArray()); Grants = Array.AsReadOnly(grants.ToArray());
        }
    }
    public sealed class RuleSet
    {
        public IReadOnlyList<ClueDefinition> Clues { get; }
        public IReadOnlyList<DiscoveryRule> Discoveries { get; }
        public IReadOnlyList<SemanticRecipe> Recipes { get; }
        public IReadOnlyList<ReactionRule> Reactions { get; }
        // Compatibility views for the first MVP. Matching uses only ClueId recipes.
        public IReadOnlyList<ObjectDefinition> Objects { get; }
        public IReadOnlyList<ConceptDefinition> Concepts { get; }
        public IReadOnlyList<InterpretationRule> Interpretations { get; }
        public RuleSet(ClueDefinition[] clues, DiscoveryRule[] discoveries, SemanticRecipe[] recipes, ReactionRule[] reactions)
        {
            if (clues.Select(x => x.Id).Distinct().Count() != clues.Length || discoveries.Select(x => x.Source).Distinct().Count() != discoveries.Length ||
                discoveries.Select(x => x.Id).Distinct().Count() != discoveries.Length || recipes.Select(x => x.Id).Distinct().Count() != recipes.Length || reactions.Select(x => x.Id).Distinct().Count() != reactions.Length)
                throw new ArgumentException("Content IDs must be unique.");
            if (recipes.Any(x => x.Inputs.Any(c => !clues.Any(d => d.Id == c))) || discoveries.Any(x => x.Grants.Any(c => !clues.Any(d => d.Id == c))))
                throw new ArgumentException("Content references an unknown clue.");
            for (int i = 0; i < recipes.Length; i++)
                for (int j = i + 1; j < recipes.Length; j++)
                    if (recipes[i].Matches(recipes[j].Inputs)) throw new ArgumentException("Ambiguous recipe inputs are not supported.");
            Clues = Array.AsReadOnly((ClueDefinition[])clues.Clone()); Discoveries = Array.AsReadOnly((DiscoveryRule[])discoveries.Clone());
            Recipes = Array.AsReadOnly((SemanticRecipe[])recipes.Clone()); Reactions = Array.AsReadOnly((ReactionRule[])reactions.Clone());
            Objects = Array.AsReadOnly(discoveries.Select(x => new ObjectDefinition(x.Source, x.DisplayName, x.Description, x.Requires.ToArray(),
                x.Grants.Where(c => clues.Single(d => d.Id == c).Category == ClueCategory.Concept).Select(c => new ConceptId(c.Value)).ToArray(),
                x.Grants.Where(c => clues.Single(d => d.Id == c).Category == ClueCategory.Object && c.Value != x.Source.Value).Select(c => new ObjectId(c.Value)).ToArray())).ToArray());
            Concepts = Array.AsReadOnly(clues.Where(x => x.Category == ClueCategory.Concept).Select(x => new ConceptDefinition(new ConceptId(x.Id.Value), x.DisplayName)).ToArray());
            Interpretations = Array.AsReadOnly(recipes.Where(x => x.Inputs.Count == 2 && x.Inputs.Count(c => clues.Single(d => d.Id == c).Category == ClueCategory.Concept) == 1 &&
                x.Inputs.Count(c => clues.Single(d => d.Id == c).Category == ClueCategory.Object) == 1).Select(x => new InterpretationRule(
                    new ConceptId(x.Inputs.Single(c => clues.Single(d => d.Id == c).Category == ClueCategory.Concept).Value),
                    new ObjectId(x.Inputs.Single(c => clues.Single(d => d.Id == c).Category == ClueCategory.Object).Value), x.Requires.ToArray(), x.Produces.ToArray())).ToArray());
        }
        public RuleSet(ObjectDefinition[] objects, ConceptDefinition[] concepts, InterpretationRule[] interpretations, ReactionRule[] reactions)
            : this(objects.Select(x => new ClueDefinition(new ClueId(x.Id.Value), x.Label, ClueCategory.Object)).Concat(concepts.Select(x => new ClueDefinition(new ClueId(x.Id.Value), x.Label, ClueCategory.Concept))).ToArray(),
                objects.Select(x => new DiscoveryRule("inspect." + x.Id.Value, x.Id, x.Label, x.Description, x.Requires,
                    new[] { new ClueId(x.Id.Value) }.Concat(x.Concepts.Select(c => new ClueId(c.Value))).Concat(x.RevealsObjects.Select(o => new ClueId(o.Value))).ToArray())).ToArray(),
                interpretations.Select(x => new SemanticRecipe("recipe." + x.Target.Value + "." + x.Concept.Value,
                    new[] { new ClueId(x.Target.Value), new ClueId(x.Concept.Value) }, x.Requires, x.Produces)).ToArray(), reactions) { }
        public bool IsInspectable(GameState state, ObjectId source) => Discoveries.Any(x => x.Source == source && x.Requires.All(state.HasFact));
        public IEnumerable<SemanticRecipe> AvailableRecipes(GameState state) => Recipes.Where(x => !state.HasCompleted(x.Id) && x.Inputs.All(state.Knows) && x.Requires.All(state.HasFact));
    }
}
