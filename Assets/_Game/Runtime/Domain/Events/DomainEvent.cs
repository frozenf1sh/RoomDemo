// =============================================================================
// 文件：DomainEvent.cs
// 职责：定义规则执行后发生的领域事件与中文调试日志。
// 边界：事件记录事实与结果，不包含动画、粒子或 GameObject 操作。
// =============================================================================
namespace Game.Domain
{
    // 事件字段保持稳定类型；ToString 仅是中文调试描述，不用作机器匹配。

    /// <summary>第一次获得一个统一碎片。</summary>
    public sealed class ClueDiscovered : DomainEvent
    {
        /// <summary>第一次发现的统一碎片身份。</summary>
        public ClueId Clue { get; }
        public ClueDiscovered(ClueId clue) { Clue = clue; }
        public override string ToString() => $"[发现] 碎片={Clue}";
    }
    /// <summary>第二个关联碎片出现后，第一次获得一条可用于组合的灵感。</summary>
    public sealed class LogicDiscovered : DomainEvent
    {
        /// <summary>灵感的稳定英文标识。</summary>
        public string LogicId { get; }
        public LogicDiscovered(string logicId) { LogicId = logicId; }
        public override string ToString() => $"[灵感] {LogicId}";
    }
    /// <summary>成功组合后消耗一条一次性灵感。</summary>
    public sealed class LogicConsumed : DomainEvent
    {
        /// <summary>被消耗灵感的稳定英文标识。</summary>
        public string LogicId { get; }
        public LogicConsumed(string logicId) { LogicId = logicId; }
        public override string ToString() => $"[灵感已使用] {LogicId}";
    }
    /// <summary>玩家成功建立一个有限语义配方。</summary>
    public sealed class CombinationSucceeded : DomainEvent
    {
        /// <summary>本次成功的稳定配方 ID。</summary>
        public string RecipeId { get; }
        /// <summary>本次配方实际输出的线索快照；UI 可据类别呈现灵感，不能用它判定输入。</summary>
        public System.Collections.Generic.IReadOnlyList<ClueId> ProducedClues { get; }
        public CombinationSucceeded(string id, System.Collections.Generic.IReadOnlyList<ClueId> producedClues = null)
        { RecipeId = id; ProducedClues = System.Array.AsReadOnly(System.Linq.Enumerable.ToArray(producedClues ?? System.Array.Empty<ClueId>())); }
        public override string ToString() => $"[配方] 成功，配方={RecipeId}";
    }
    /// <summary>玩家组合失败，不消费碎片、不产生事实。</summary>
    public sealed class CombinationFailed : DomainEvent
    {
        /// <summary>面向玩家或维护者的中文失败原因。</summary>
        public string Reason { get; }
        public CombinationFailed(string reason) { Reason = reason; }
        public override string ToString() => $"[配方] 失败：{Reason}";
    }
    /// <summary>世界记下一个将于后续日序执行的反应。</summary>
    public sealed class ReactionScheduled : DomainEvent
    {
        public string ReactionId { get; }
        public int DueDay { get; }
        public ReactionScheduled(string id, int day) { ReactionId = id; DueDay = day; }
        public override string ToString() => $"[延迟反应] {ReactionId}，将在第 {DueDay} 天发生";
    }
    /// <summary>玩家结束今天后，领域日序已经推进。</summary>
    public sealed class DayAdvanced : DomainEvent
    {
        public int DayIndex { get; }
        public DayAdvanced(int day) { DayIndex = day; }
        public override string ToString() => $"[日序] 已进入第 {DayIndex} 天";
    }
    /// <summary>目前没有等待时间的解释，因此本次结束当天无效。</summary>
    public sealed class DayAdvanceFailed : DomainEvent
    {
        public string Reason { get; }
        public DayAdvanceFailed(string reason) { Reason = reason; }
        public override string ToString() => "[日序] " + Reason;
    }
    /// <summary>领域事件共同类型，只描述已发生的事情。</summary>
    public abstract class DomainEvent { }
    /// <summary>旧消费者使用的物体发现兼容事件。</summary>
    public sealed class ObjectDiscovered : DomainEvent
    {
        /// <summary>被调查或发现的稳定物体身份。</summary>
        public ObjectId Object { get; }
        public ObjectDiscovered(ObjectId id) { Object = id; }
        public override string ToString() => $"[发现] 物体={Object}";
    }
    /// <summary>旧消费者使用的概念发现兼容事件。</summary>
    public sealed class ConceptDiscovered : DomainEvent
    {
        /// <summary>旧命令或事件中的概念身份。</summary>
        public ConceptId Concept { get; }
        public ConceptDiscovered(ConceptId id) { Concept = id; }
        public override string ToString() => $"[发现] 概念={Concept}";
    }
    /// <summary>一次成功观察及其中文描述，重复观察也可产生。</summary>
    public sealed class ObjectInspected : DomainEvent
    {
        /// <summary>被调查或发现的稳定物体身份。</summary>
        public ObjectId Object { get; }
        /// <summary>成功观察时显示的中文描述。</summary>
        public string Description { get; }
        public ObjectInspected(ObjectId id, string description) { Object = id; Description = description; }
        public override string ToString() => $"[观察] 物体={Object}";
    }
    /// <summary>调查被前置事实或未知物体拒绝。</summary>
    public sealed class InspectionFailed : DomainEvent
    {
        /// <summary>被调查或发现的稳定物体身份。</summary>
        public ObjectId Object { get; }
        /// <summary>面向玩家或维护者的中文失败原因。</summary>
        public string Reason { get; }
        public InspectionFailed(ObjectId id, string reason) { Object = id; Reason = reason; }
        public override string ToString() => $"[观察失败] {Object}：{Reason}";
    }
    /// <summary>旧二元 API 的解释成功兼容事件。</summary>
    public sealed class InterpretationSucceeded : DomainEvent
    {
        /// <summary>旧命令或事件中的概念身份。</summary>
        public ConceptId Concept { get; }
        /// <summary>旧二元解释的目标物体身份。</summary>
        public ObjectId Target { get; }
        public InterpretationSucceeded(ConceptId concept, ObjectId target) { Concept = concept; Target = target; }
        public override string ToString() => $"[解释成功] {Concept} → {Target}";
    }
    /// <summary>旧二元 API 的解释失败兼容事件。</summary>
    public sealed class InterpretationFailed : DomainEvent
    {
        /// <summary>旧命令或事件中的概念身份。</summary>
        public ConceptId Concept { get; }
        /// <summary>旧二元解释的目标物体身份。</summary>
        public ObjectId Target { get; }
        /// <summary>面向玩家或维护者的中文失败原因。</summary>
        public string Reason { get; }
        public InterpretationFailed(ConceptId concept, ObjectId target, string reason) { Concept = concept; Target = target; Reason = reason; }
        public override string ToString() => $"[解释失败] {Concept} → {Target}：{Reason}";
    }
    /// <summary>一个世界事实第一次成立。</summary>
    public sealed class FactAdded : DomainEvent
    {
        /// <summary>本次第一次成立的领域事实身份。</summary>
        public FactId Fact { get; }
        public FactAdded(FactId fact) { Fact = fact; }
        public override string ToString() => $"[事实] +{Fact}";
    }
    /// <summary>一个自动反应第一次触发。</summary>
    public sealed class ReactionTriggered : DomainEvent
    {
        /// <summary>内容的稳定身份，用于查找与去重。</summary>
        public string Id { get; }
        public ReactionTriggered(string id) { Id = id; }
        public override string ToString() => $"[反应] {Id}";
    }
}
