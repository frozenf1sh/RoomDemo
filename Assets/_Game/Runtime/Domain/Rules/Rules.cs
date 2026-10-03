using System;
using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    public sealed class ObjectDefinition
    {
        public ObjectId Id { get; }
        public string Label { get; }
        public string Description { get; }
        public IReadOnlyList<FactId> Requires { get; }
        public IReadOnlyList<ConceptId> Concepts { get; }
        public IReadOnlyList<ObjectId> RevealsObjects { get; }
        public ObjectDefinition(ObjectId id, string label, string description, FactId[] requires, ConceptId[] concepts, ObjectId[] revealsObjects)
        {
            Id = id; Label = label; Description = description;
            Requires = Array.AsReadOnly((FactId[])requires.Clone());
            Concepts = Array.AsReadOnly((ConceptId[])concepts.Clone());
            RevealsObjects = Array.AsReadOnly((ObjectId[])revealsObjects.Clone());
        }
    }
    public sealed class ConceptDefinition
    {
        public ConceptId Id { get; }
        public string Label { get; }
        public ConceptDefinition(ConceptId id, string label) { Id = id; Label = label; }
    }
    public sealed class InterpretationRule
    {
        public ConceptId Concept { get; }
        public ObjectId Target { get; }
        public IReadOnlyList<FactId> Requires { get; }
        public IReadOnlyList<FactId> Produces { get; }
        public InterpretationRule(ConceptId concept, ObjectId target, FactId[] requires, FactId[] produces)
        {
            Concept = concept; Target = target;
            Requires = Array.AsReadOnly((FactId[])requires.Clone());
            Produces = Array.AsReadOnly((FactId[])produces.Clone());
        }
    }
    public sealed class ReactionRule
    {
        public string Id { get; }
        public IReadOnlyList<FactId> Requires { get; }
        public IReadOnlyList<FactId> Produces { get; }
        public ReactionRule(string id, FactId[] requires, FactId[] produces)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Reaction ID required.");
            Id = id; Requires = Array.AsReadOnly((FactId[])requires.Clone()); Produces = Array.AsReadOnly((FactId[])produces.Clone());
        }
    }
    public sealed class RuleSet
    {
        public IReadOnlyList<ObjectDefinition> Objects { get; }
        public IReadOnlyList<ConceptDefinition> Concepts { get; }
        public IReadOnlyList<InterpretationRule> Interpretations { get; }
        public IReadOnlyList<ReactionRule> Reactions { get; }
        public RuleSet(ObjectDefinition[] objects, ConceptDefinition[] concepts, InterpretationRule[] interpretations, ReactionRule[] reactions)
        {
            if (objects.Select(x => x.Id).Distinct().Count() != objects.Length || concepts.Select(x => x.Id).Distinct().Count() != concepts.Length ||
                reactions.Select(x => x.Id).Distinct().Count() != reactions.Length || interpretations.Select(x => (x.Concept, x.Target)).Distinct().Count() != interpretations.Length)
                throw new ArgumentException("Rule IDs and interpretation pairs must be unique.");
            if (interpretations.Any(x => !objects.Any(o => o.Id == x.Target) || !concepts.Any(c => c.Id == x.Concept)) ||
                objects.Any(x => x.Concepts.Any(c => !concepts.Any(d => d.Id == c)) || x.RevealsObjects.Any(o => !objects.Any(d => d.Id == o))))
                throw new ArgumentException("Rules contain unknown object or concept IDs.");
            Objects = Array.AsReadOnly((ObjectDefinition[])objects.Clone()); Concepts = Array.AsReadOnly((ConceptDefinition[])concepts.Clone());
            Interpretations = Array.AsReadOnly((InterpretationRule[])interpretations.Clone()); Reactions = Array.AsReadOnly((ReactionRule[])reactions.Clone());
        }
    }
}
