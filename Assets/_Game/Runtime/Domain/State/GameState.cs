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
        /// <summary>四阶段作品与颜料的唯一账本；旧规则不会调用新画布命令。</summary>
        public AtelierCanvasDocument AtelierCanvas { get; } = new AtelierCanvasDocument();
        // 线索、事实、反应、历史分别去重；外部不得得到集合的可写引用。
        private readonly HashSet<ClueId> clues = new HashSet<ClueId>();
        private readonly HashSet<ClueId> consumedClues = new HashSet<ClueId>();
        private readonly HashSet<FactId> facts = new HashSet<FactId>();
        // 灵感是一次性的组合许可；已用记录不能从当前事实反推，否则下一次发现会错误恢复它。
        private readonly HashSet<string> consumedLogics = new HashSet<string>();
        private readonly HashSet<string> reactions = new HashSet<string>();
        private readonly HashSet<string> recipes = new HashSet<string>();
        private readonly Dictionary<ClueId, ClueCategory> categories;
        private readonly FactId? canvasLockFact;
        private readonly Dictionary<string, PendingDeferredEffect> pending = new Dictionary<string, PendingDeferredEffect>();
        /// <summary>显式游戏日序，初始为第一天，不与真实时间关联。</summary>
        public int DayIndex { get; private set; } = 1;
        /// <summary>重要成功解释的服从次数；失败和重复配方不会增加。</summary>
        public int CompliantCount { get; private set; }
        /// <summary>重要成功解释的偏离次数，不作为玩家可见的奖励。</summary>
        public int DeviationCount { get; private set; }
        /// <summary>连续服从次数；中性联想不打断连续性，偏离会清零。</summary>
        public int ConsecutiveCompliance { get; private set; }
        /// <summary>一旦发现了画布之外的解释，后续服从不再强制收束路线。</summary>
        public bool HasDeviated => DeviationCount > 0;
        /// <summary>内容配置主动交稿时等待其事实；历史内容保持连续三次服从的默认收束。</summary>
        public bool CanvasLocked => canvasLockFact.HasValue ? HasFact(canvasLockFact.Value) : !HasDeviated && ConsecutiveCompliance >= 3;
        /// <summary>内部旁白阶段从唯一状态派生，不保存另一份路线状态。</summary>
        public NarrativeStage NarrativeStage => CanvasLocked ? NarrativeStage.CanvasLockIn : DeviationCount >= 3 ? NarrativeStage.LosingAuthority : DeviationCount >= 2 ? NarrativeStage.Concerned : HasDeviated ? NarrativeStage.SlightlyConfused : NarrativeStage.Normal;
        /// <summary>锁入画布后拒绝新的偏离解释；联想与画布解释仍可执行。</summary>
        public bool Allows(NarrativeDisposition disposition) => !CanvasLocked || disposition != NarrativeDisposition.Deviant;
        /// <summary>无分配的等待查询，UI 许可不必创建队列快照。</summary>
        public bool HasPendingDeferredEffects => pending.Count > 0;
        /// <summary>已登记但尚未到日的效果快照，外部不能修改队列。</summary>
        public IReadOnlyCollection<PendingDeferredEffect> PendingDeferredEffects => pending.Values.OrderBy(x => x.DueDay).ThenBy(x => x.ReactionId).ToList().AsReadOnly();
        /// <summary>建立碎片类别索引，供旧物体与概念查询派生视图。</summary>
        public GameState(IEnumerable<ClueDefinition> definitions, FactId? canvasLockFact = null)
        {
            // 收束策略由内容装载一次，不允许 Unity 表现层在运行中改写。
            this.canvasLockFact = canvasLockFact;
            categories = definitions.ToDictionary(x => x.Id, x => x.Category);
        }
        // 每次查询返回只读快照，避免外部强制转换后修改内部 HashSet。
        /// <summary>当前已知碎片的只读快照，无法改写内部集合。</summary>
        public IReadOnlyCollection<ClueId> KnownClues => clues.ToList().AsReadOnly();
        /// <summary>仍可放入组合区的材料；消费不抹除发现历史与来源知识。</summary>
        public IReadOnlyCollection<ClueId> AvailableClues => clues.Where(CanUseClue).ToList().AsReadOnly();
        /// <summary>本局已用于成功配方的材料快照；重复调查不会恢复。</summary>
        public IReadOnlyCollection<ClueId> ConsumedClues => consumedClues.ToList().AsReadOnly();
        /// <summary>查询材料是否已发现且尚未被配方消费。</summary>
        public bool CanUseClue(ClueId id) => clues.Contains(id) && !consumedClues.Contains(id);
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
        /// <summary>查询指定灵感是否已经被一次成功组合消耗。</summary>
        public bool HasConsumedLogic(string id) => consumedLogics.Contains(id);
        /// <summary>查询反应是否已经执行。</summary>
        public bool HasFired(string id) => reactions.Contains(id);
        /// <summary>查询配方是否已经由玩家建立。</summary>
        public bool HasCompleted(string id) => recipes.Contains(id);
        /// <summary>仅复制已验证的第四阶段入口，供内存练习恢复；不提供通用存档接口。</summary>
        internal GameState CopyAtelierPractice(RuleSet rules)
        {
            var copy=new GameState(rules.Clues,rules.CanvasLockFact);
            copy.clues.UnionWith(clues);
            copy.consumedClues.UnionWith(consumedClues);
            copy.facts.UnionWith(facts);
            copy.recipes.UnionWith(recipes);
            copy.reactions.UnionWith(reactions);
            copy.consumedLogics.UnionWith(consumedLogics);
            copy.CompliantCount=CompliantCount;
            copy.DeviationCount=DeviationCount;
            copy.ConsecutiveCompliance=ConsecutiveCompliance;
            copy.DayIndex=DayIndex;
            copy.AtelierCanvas.RestorePractice(AtelierCanvas);
            return copy;
        }
        // 只有领域程序集可以写状态；新增项的布尔返回值用于事件去重。

        /// <summary>领域内部添加已知碎片，返回是否第一次添加。</summary>
        internal bool Discover(ClueId id) => clues.Add(id);
        /// <summary>仅由成功配方消费已知材料，知识与原配方历史继续保留。</summary>
        internal bool ConsumeClue(ClueId id) => clues.Contains(id) && consumedClues.Add(id);
        /// <summary>领域内部添加事实，以集合去重保证事件唯一。</summary>
        internal bool AddFact(FactId id) => facts.Add(id);
        /// <summary>领域内部移除一次性事实，返回是否确实从当前状态消耗。</summary>
        internal bool RemoveFact(FactId id) => facts.Remove(id);
        /// <summary>记录已经消耗的灵感，阻止后续观察再次自动授予。</summary>
        internal bool ConsumeLogic(string id) => consumedLogics.Add(id);
        /// <summary>领域内部标记反应，阻止循环或重复执行。</summary>
        internal bool Fire(string id) => reactions.Add(id);
        /// <summary>领域内部记录已完成的玩家配方。</summary>
        internal bool Complete(string id, NarrativeDisposition disposition = NarrativeDisposition.Neutral)
        {
            // 先去重后计数；失败提交、重复解释和中性联想都不能刷路线次数。
            if (!recipes.Add(id)) return false;
            if (disposition == NarrativeDisposition.Compliant)
            {
                CompliantCount++;
                ConsecutiveCompliance++;
            }
            else if (disposition == NarrativeDisposition.Deviant)
            {
                DeviationCount++;
                ConsecutiveCompliance = 0;
            }
            return true;
        }
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
