using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    public sealed class GameState
    {
        private readonly HashSet<ClueId> clues = new HashSet<ClueId>();
        private readonly HashSet<FactId> facts = new HashSet<FactId>();
        private readonly HashSet<string> reactions = new HashSet<string>();
        private readonly HashSet<string> recipes = new HashSet<string>();
        private readonly Dictionary<ClueId, ClueCategory> categories;
        public GameState(IEnumerable<ClueDefinition> definitions) { categories = definitions.ToDictionary(x => x.Id, x => x.Category); }
        public IReadOnlyCollection<ClueId> KnownClues => clues.ToList().AsReadOnly();
        public IReadOnlyCollection<ObjectId> KnownObjects => clues.Where(x => categories[x] == ClueCategory.Object).Select(x => new ObjectId(x.Value)).ToList().AsReadOnly();
        public IReadOnlyCollection<ConceptId> KnownConcepts => clues.Where(x => categories[x] == ClueCategory.Concept).Select(x => new ConceptId(x.Value)).ToList().AsReadOnly();
        public IReadOnlyCollection<FactId> Facts => facts.ToList().AsReadOnly();
        public IReadOnlyCollection<string> FiredReactions => reactions.ToList().AsReadOnly();
        public IReadOnlyCollection<string> CompletedRecipes => recipes.ToList().AsReadOnly();
        public bool Knows(ClueId id) => clues.Contains(id);
        public bool Knows(ObjectId id) => Knows(new ClueId(id.Value));
        public bool Knows(ConceptId id) => Knows(new ClueId(id.Value));
        public bool HasFact(FactId id) => facts.Contains(id);
        public bool HasFired(string id) => reactions.Contains(id);
        public bool HasCompleted(string id) => recipes.Contains(id);
        internal bool Discover(ClueId id) => clues.Add(id);
        internal bool AddFact(FactId id) => facts.Add(id);
        internal bool Fire(string id) => reactions.Add(id);
        internal bool Complete(string id) => recipes.Add(id);
    }
}
