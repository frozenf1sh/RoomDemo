using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    public sealed class GameState
    {
        private readonly HashSet<ObjectId> objects = new HashSet<ObjectId>();
        private readonly HashSet<ConceptId> concepts = new HashSet<ConceptId>();
        private readonly HashSet<FactId> facts = new HashSet<FactId>();
        private readonly HashSet<string> reactions = new HashSet<string>();
        public IReadOnlyCollection<ObjectId> KnownObjects => objects.ToList().AsReadOnly();
        public IReadOnlyCollection<ConceptId> KnownConcepts => concepts.ToList().AsReadOnly();
        public IReadOnlyCollection<FactId> Facts => facts.ToList().AsReadOnly();
        public IReadOnlyCollection<string> FiredReactions => reactions.ToList().AsReadOnly();
        public bool Knows(ObjectId id) => objects.Contains(id);
        public bool Knows(ConceptId id) => concepts.Contains(id);
        public bool HasFact(FactId id) => facts.Contains(id);
        public bool HasFired(string id) => reactions.Contains(id);
        internal bool Discover(ObjectId id) => objects.Add(id);
        internal bool Discover(ConceptId id) => concepts.Add(id);
        internal bool AddFact(FactId id) => facts.Add(id);
        internal bool Fire(string id) => reactions.Add(id);
    }
}
