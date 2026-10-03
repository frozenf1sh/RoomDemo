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
}
