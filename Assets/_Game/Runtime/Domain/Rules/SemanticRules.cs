// =============================================================================
// 文件：SemanticRules.cs
// 职责：定义线索分类、有限配方、条件发现与规则集合。
// 边界：通过内容数据扩展玩法，构造时拒绝歧义与未知碎片引用。
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    /// <summary>碎片显示分类；观察是具体细节，概念是联想产物；分类不参与配方判定。</summary>
    public enum ClueCategory { Object = 0, Concept = 1, Modifier = 2, Phrase = 3, Observation = 4 }
    /// <summary>碎片的稳定身份、中文显示名与类别。</summary>
    public sealed class ClueDefinition
    {
        /// <summary>内容的稳定身份，用于查找与去重。</summary>
        public ClueId Id { get; }
        /// <summary>面向玩家或维护者的中文显示名称，不参与规则匹配。</summary>
        public string DisplayName { get; }
        /// <summary>碎片的显示分类，不决定配方是否合法。</summary>
        public ClueCategory Category { get; }
        /// <summary>创建包含中文显示名的碎片定义。</summary>
        public ClueDefinition(ClueId id, string displayName, ClueCategory category)
        { Id = id; DisplayName = displayName; Category = category; }
    }
    /// <summary>有限配方：无序精确输入与前置事实，输出线索和／或事实。</summary>
    public sealed class SemanticRecipe
    {
        /// <summary>内容的稳定身份，用于查找与去重。</summary>
        public string Id { get; }
        /// <summary>配方所需的精确碎片集合，顺序无关。</summary>
        public IReadOnlyList<ClueId> Inputs { get; }
        /// <summary>必须全部成立的前置事实。</summary>
        public IReadOnlyList<FactId> Requires { get; }
        /// <summary>规则成立时添加的领域事实，不指定 Unity 表现。</summary>
        public IReadOnlyList<FactId> ProducesFacts { get; }
        /// <summary>组合后形成的线索；概念与世界解释共用同一求值路径。</summary>
        public IReadOnlyList<ClueId> ProducesClues { get; }
        /// <summary>旧内容代码的事实输出兼容视图，不保存第二份数据。</summary>
        public IReadOnlyList<FactId> Produces => ProducesFacts;
        /// <summary>校验输入规模并复制规则数组，避免外部修改已装载内容。</summary>
        /// <param name="id">用于完成历史与日志的稳定配方 ID。</param>
        /// <param name="inputs">两个或三个不同碎片，无序精确匹配。</param>
        /// <param name="requires">必须全部成立的前置事实，可以为空。</param>
        /// <param name="produces">命中后添加的事实，可被后续反应继续消费。</param>
        /// <exception cref="ArgumentException">配方 ID 为空、输入数量越界或输入重复。</exception>
        public SemanticRecipe(string id, IReadOnlyList<ClueId> inputs, IReadOnlyList<FactId> requires, IReadOnlyList<FactId> produces)
            : this(id, inputs, requires, Array.Empty<ClueId>(), produces) { }

        /// <summary>创建可以产生线索和事实的统一配方；不按“灵感／解释”分支执行。</summary>
        /// <param name="producesClues">形成的新线索，通常为概念；可以为空。</param>
        /// <param name="producesFacts">世界事实输出；可以为空。</param>
        public SemanticRecipe(string id, IReadOnlyList<ClueId> inputs, IReadOnlyList<FactId> requires,
            IReadOnlyList<ClueId> producesClues, IReadOnlyList<FactId> producesFacts)
        {
            if (inputs == null || requires == null || producesClues == null || producesFacts == null)
                throw new ArgumentNullException("配方集合不能为 null；没有输出时使用空数组。");
            // 限制为两个或三个不同碎片；重复输入不能伪装成更多语义。
            if (string.IsNullOrWhiteSpace(id) || inputs.Count < 2 || inputs.Count > 3 || inputs.Distinct().Count() != inputs.Count)
                throw new ArgumentException("配方需要非空标识符，以及两个或三个不同的输入碎片。");
            // 复制集合后再包装为只读，调用者修改原数组不会影响配方。
            Id = id;
            Inputs = Array.AsReadOnly(inputs.ToArray());
            Requires = Array.AsReadOnly(requires.ToArray());
            ProducesClues = Array.AsReadOnly(producesClues.ToArray());
            ProducesFacts = Array.AsReadOnly(producesFacts.ToArray());
        }
        /// <summary>无序精确匹配：数量相等、无重复输入且所有碎片一致。</summary>
        /// <param name="inputs">玩家提交的完整输入集合；多余碎片不能忽略。</param>
        /// <returns>仅当输入与本配方完全相等时返回 true。</returns>
        public bool Matches(IReadOnlyList<ClueId> inputs) => inputs != null && inputs.Count == Inputs.Count && inputs.Distinct().Count() == inputs.Count && Inputs.All(inputs.Contains);
    }
    /// <summary>以世界事实控制调查许可与碎片授予的内容数据。</summary>
    public sealed class DiscoveryRule
    {
        /// <summary>内容的稳定身份，用于查找与去重。</summary>
        public string Id { get; }
        /// <summary>触发观察的物体身份。</summary>
        public ObjectId Source { get; }
        /// <summary>面向玩家或维护者的中文显示名称，不参与规则匹配。</summary>
        public string DisplayName { get; }
        /// <summary>成功观察时显示的中文描述。</summary>
        public string Description { get; }
        /// <summary>未满足调查条件时显示的中文反馈。</summary>
        public string LockedDescription { get; }
        /// <summary>必须全部成立的前置事实。</summary>
        public IReadOnlyList<FactId> Requires { get; }
        /// <summary>成功调查时授予的碎片；已经知道的碎片不会重复发现。</summary>
        public IReadOnlyList<ClueId> Grants { get; }
        /// <summary>创建带前置事实与授予列表的条件发现规则。</summary>
        public DiscoveryRule(string id, ObjectId source, string displayName, string description, IReadOnlyList<FactId> requires, IReadOnlyList<ClueId> grants, string lockedDescription = "还需要发生一些变化，才能调查这里。")
        {
            Id = id; Source = source; DisplayName = displayName; Description = description; LockedDescription = lockedDescription;
            Requires = Array.AsReadOnly(requires.ToArray()); Grants = Array.AsReadOnly(grants.ToArray());
        }
    }
    /// <summary>校验后固定的关卡内容，以及第一版兼容查询视图。</summary>
    public sealed class RuleSet
    {
        /// <summary>关卡中所有可收集碎片的只读定义集合。</summary>
        public IReadOnlyList<ClueDefinition> Clues { get; }
        /// <summary>全部条件发现规则的只读集合。</summary>
        public IReadOnlyList<DiscoveryRule> Discoveries { get; }
        /// <summary>全部有限语义配方的只读集合。</summary>
        public IReadOnlyList<SemanticRecipe> Recipes { get; }
        /// <summary>全部一次性自动反应的只读集合。</summary>
        public IReadOnlyList<ReactionRule> Reactions { get; }
        // 第一版的兼容查询视图；实际匹配始终只使用 ClueId 配方。
        /// <summary>由统一内容定义派生的第一版物体视图。</summary>
        public IReadOnlyList<ObjectDefinition> Objects { get; }
        /// <summary>由统一定义派生的旧概念视图或该物体授予的概念。</summary>
        public IReadOnlyList<ConceptDefinition> Concepts { get; }
        /// <summary>从兼容二元配方派生的旧解释视图。</summary>
        public IReadOnlyList<InterpretationRule> Interpretations { get; }
        /// <summary>装载规则并校验唯一性、引用与歧义；旧构造入口会转换成新数据。</summary>
        public RuleSet(ClueDefinition[] clues, DiscoveryRule[] discoveries, SemanticRecipe[] recipes, ReactionRule[] reactions)
        {
            // 一次性验证所有内容身份，尽早暴露策划数据错误。
            if (clues.Select(x => x.Id).Distinct().Count() != clues.Length || discoveries.Select(x => x.Source).Distinct().Count() != discoveries.Length ||
                discoveries.Select(x => x.Id).Distinct().Count() != discoveries.Length || recipes.Select(x => x.Id).Distinct().Count() != recipes.Length || reactions.Select(x => x.Id).Distinct().Count() != reactions.Length)
                throw new ArgumentException("内容标识符必须唯一。");
            // 配方输入和发现产出都必须指向已定义的碎片。
            if (recipes.Any(x => x.Inputs.Concat(x.ProducesClues).Any(c => !clues.Any(d => d.Id == c))) || discoveries.Any(x => x.Grants.Any(c => !clues.Any(d => d.Id == c))))
                throw new ArgumentException("内容引用了未知碎片。");
            // 两个配方即使前置事实不同也不能使用相同输入；当前不引入优先级。
            for (int i = 0; i < recipes.Length; i++)
                for (int j = i + 1; j < recipes.Length; j++)
                    if (recipes[i].Matches(recipes[j].Inputs)) throw new ArgumentException("不支持输入完全相同的多个配方。");
            // 复制内容集合，运行时不允许外部改变规则装载结果。
            Clues = Array.AsReadOnly((ClueDefinition[])clues.Clone()); Discoveries = Array.AsReadOnly((DiscoveryRule[])discoveries.Clone());
            Recipes = Array.AsReadOnly((SemanticRecipe[])recipes.Clone()); Reactions = Array.AsReadOnly((ReactionRule[])reactions.Clone());
            // 旧视图从统一定义推导，不作为第二份世界状态来源。
            Objects = Array.AsReadOnly(discoveries.Select(x => new ObjectDefinition(x.Source, x.DisplayName, x.Description, x.Requires.ToArray(),
                x.Grants.Where(c => clues.Single(d => d.Id == c).Category == ClueCategory.Concept).Select(c => new ConceptId(c.Value)).ToArray(),
                x.Grants.Where(c => clues.Single(d => d.Id == c).Category == ClueCategory.Object && c.Value != x.Source.Value).Select(c => new ObjectId(c.Value)).ToArray())).ToArray());
            // 仅将概念分类映射到旧 API；修饰与短语保留在统一碎片表中。
            Concepts = Array.AsReadOnly(clues.Where(x => x.Category == ClueCategory.Concept).Select(x => new ConceptDefinition(new ConceptId(x.Id.Value), x.DisplayName)).ToArray());
            // 只有一个物体加一个概念的二元配方才能映射成旧解释结构。
            Interpretations = Array.AsReadOnly(recipes.Where(x => x.Inputs.Count == 2 && x.Inputs.Count(c => clues.Single(d => d.Id == c).Category == ClueCategory.Concept) == 1 &&
                x.Inputs.Count(c => clues.Single(d => d.Id == c).Category == ClueCategory.Object) == 1).Select(x => new InterpretationRule(
                    new ConceptId(x.Inputs.Single(c => clues.Single(d => d.Id == c).Category == ClueCategory.Concept).Value),
                    new ObjectId(x.Inputs.Single(c => clues.Single(d => d.Id == c).Category == ClueCategory.Object).Value), x.Requires.ToArray(), x.Produces.ToArray())).ToArray());
        }
        /// <summary>装载规则并校验唯一性、引用与歧义；旧构造入口会转换成新数据。</summary>
        public RuleSet(ObjectDefinition[] objects, ConceptDefinition[] concepts, InterpretationRule[] interpretations, ReactionRule[] reactions)
            : this(objects.Select(x => new ClueDefinition(new ClueId(x.Id.Value), x.Label, ClueCategory.Object)).Concat(concepts.Select(x => new ClueDefinition(new ClueId(x.Id.Value), x.Label, ClueCategory.Concept))).ToArray(),
                objects.Select(x => new DiscoveryRule("inspect." + x.Id.Value, x.Id, x.Label, x.Description, x.Requires,
                    new[] { new ClueId(x.Id.Value) }.Concat(x.Concepts.Select(c => new ClueId(c.Value))).Concat(x.RevealsObjects.Select(o => new ClueId(o.Value))).ToArray())).ToArray(),
                interpretations.Select(x => new SemanticRecipe("recipe." + x.Target.Value + "." + x.Concept.Value,
                    new[] { new ClueId(x.Target.Value), new ClueId(x.Concept.Value) }, x.Requires, x.Produces)).ToArray(), reactions) { }
        /// <summary>只依据发现规则的前置事实查询调查许可。</summary>
        public bool IsInspectable(GameState state, ObjectId source) => Discoveries.Any(x => x.Source == source && x.Requires.All(state.HasFact));
        /// <summary>查询输入与条件已满足但未完成的配方，仅供调试显示。</summary>
        public IEnumerable<SemanticRecipe> AvailableRecipes(GameState state) => Recipes.Where(x => !state.HasCompleted(x.Id) && x.Inputs.All(state.Knows) && x.Requires.All(state.HasFact));
    }
}
