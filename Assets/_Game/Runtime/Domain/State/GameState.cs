// =============================================================================
// 文件：GameState.cs
// 职责：保存已知碎片、世界事实、反应记录和配方历史。
// 边界：私有集合为唯一真相；外部只能查询或取得只读快照。
// =============================================================================
using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    /// <summary>唯一的领域状态；所有可变集合保持私有。</summary>
    public sealed class GameState
    {
        // 线索、事实、反应、历史分别去重；外部不得得到集合的可写引用。
        private readonly HashSet<ClueId> clues = new HashSet<ClueId>();
        private readonly HashSet<FactId> facts = new HashSet<FactId>();
        private readonly HashSet<string> reactions = new HashSet<string>();
        private readonly HashSet<string> recipes = new HashSet<string>();
        private readonly Dictionary<ClueId, ClueCategory> categories;
        private readonly Dictionary<string, PendingDeferredEffect> pending = new Dictionary<string, PendingDeferredEffect>();
        /// <summary>显式游戏日序，初始为第一天，不与真实时间关联。</summary>
        public int DayIndex { get; private set; } = 1;
        /// <summary>已登记但尚未到日的效果快照，外部不能修改队列。</summary>
        public IReadOnlyCollection<PendingDeferredEffect> PendingDeferredEffects => pending.Values.OrderBy(x => x.DueDay).ThenBy(x => x.ReactionId).ToList().AsReadOnly();
        /// <summary>建立碎片类别索引，供旧物体与概念查询派生视图。</summary>
        public GameState(IEnumerable<ClueDefinition> definitions) { categories = definitions.ToDictionary(x => x.Id, x => x.Category); }
        // 每次查询返回只读快照，避免外部强制转换后修改内部 HashSet。
        /// <summary>当前已知碎片的只读快照，无法改写内部集合。</summary>
        public IReadOnlyCollection<ClueId> KnownClues => clues.ToList().AsReadOnly();
        // 旧 Objects/Concepts 视图由统一线索派生，不独立存储。
        /// <summary>根据碎片类别派生的已知物体兼容快照。</summary>
        public IReadOnlyCollection<ObjectId> KnownObjects => clues.Where(x => categories[x] == ClueCategory.Object).Select(x => new ObjectId(x.Value)).ToList().AsReadOnly();
        /// <summary>根据碎片类别派生的已知概念兼容快照。</summary>
        public IReadOnlyCollection<ConceptId> KnownConcepts => clues.Where(x => categories[x] == ClueCategory.Concept).Select(x => new ConceptId(x.Value)).ToList().AsReadOnly();
        /// <summary>已成立世界事实的只读快照。</summary>
        public IReadOnlyCollection<FactId> Facts => facts.ToList().AsReadOnly();
        /// <summary>已经执行过的反应 ID 快照，用于一次性执行。</summary>
        public IReadOnlyCollection<string> FiredReactions => reactions.ToList().AsReadOnly();
        /// <summary>已建立的玩家配方 ID 快照，用于历史和重复提交校验。</summary>
        public IReadOnlyCollection<string> CompletedRecipes => recipes.ToList().AsReadOnly();
        /// <summary>查询统一碎片；旧 ID 重载先转换为 ClueId。</summary>
        public bool Knows(ClueId id) => clues.Contains(id);
        /// <summary>查询统一碎片；旧 ID 重载先转换为 ClueId。</summary>
        public bool Knows(ObjectId id) => Knows(new ClueId(id.Value));
        /// <summary>查询统一碎片；旧 ID 重载先转换为 ClueId。</summary>
        public bool Knows(ConceptId id) => Knows(new ClueId(id.Value));
        /// <summary>查询事实是否已成立。</summary>
        public bool HasFact(FactId id) => facts.Contains(id);
        /// <summary>查询反应是否已经执行。</summary>
        public bool HasFired(string id) => reactions.Contains(id);
        /// <summary>查询配方是否已经由玩家建立。</summary>
        public bool HasCompleted(string id) => recipes.Contains(id);
        // 只有领域程序集可以写状态；新增项的布尔返回值用于事件去重。

        /// <summary>领域内部添加已知碎片，返回是否第一次添加。</summary>
        internal bool Discover(ClueId id) => clues.Add(id);
        /// <summary>领域内部添加事实，以集合去重保证事件唯一。</summary>
        internal bool AddFact(FactId id) => facts.Add(id);
        /// <summary>领域内部标记反应，阻止循环或重复执行。</summary>
        internal bool Fire(string id) => reactions.Add(id);
        /// <summary>领域内部记录已完成的玩家配方。</summary>
        internal bool Complete(string id) => recipes.Add(id);
        /// <summary>领域内部推进日序，不发布事件；事件由引擎统一组织。</summary>
        internal void AdvanceDay() { DayIndex = checked(DayIndex + 1); }
        /// <summary>仅全新预设恢复使用；恢复日序不代表玩家结束了一天。</summary>
        internal void RestoreDay(int day) { if (day < 1) throw new System.ArgumentOutOfRangeException(nameof(day)); DayIndex = day; }
        /// <summary>以反应身份去重登记；不能从表现层修改待执行效果。</summary>
        internal void Schedule(PendingDeferredEffect effect) { pending.Add(effect.ReactionId, effect); }
        /// <summary>在输出前移除，防止重复推进日序再次应用同一效果。</summary>
        internal void Consume(string id) { pending.Remove(id); }
    }
}
