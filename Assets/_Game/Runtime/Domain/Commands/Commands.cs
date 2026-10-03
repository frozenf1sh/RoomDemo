// =============================================================================
// 文件：Commands.cs
// 职责：定义外部可提交的观察、组合与兼容解释命令。
// 边界：命令只承载输入；合法性与世界变化由 RuleEngine 判断。
// =============================================================================
namespace Game.Domain
{
    /// <summary>携带碎片快照的组合命令。</summary>
    public sealed class Combine : ICommand
    {
        // 持有防御性复制的输入快照，避免 UI 清空槽位影响正在执行的命令。
        /// <summary>本次组合的碎片输入快照，调用方无法修改。</summary>
        public System.Collections.Generic.IReadOnlyList<ClueId> Clues { get; }
        /// <summary>复制输入列表，防止调用方在提交后改变命令内容。</summary>
        public Combine(System.Collections.Generic.IReadOnlyList<ClueId> clues) { Clues = System.Array.AsReadOnly(System.Linq.Enumerable.ToArray(clues)); }
    }
    /// <summary>所有外部命令的轻量标记边界。</summary>
    public interface ICommand { }
    /// <summary>携带调查来源 ID 的命令。</summary>
    public readonly struct InspectObject : ICommand
    {
        /// <summary>被调查或发现的稳定物体身份。</summary>
        public ObjectId Object { get; }
        /// <summary>保存调查来源的领域身份。</summary>
        public InspectObject(ObjectId id) { Object = id; }
    }
    /// <summary>保留第一版调用方式的二元解释命令。</summary>
    public readonly struct Interpret : ICommand
    {
        /// <summary>旧命令或事件中的概念身份。</summary>
        public ConceptId Concept { get; }
        /// <summary>旧二元解释的目标物体身份。</summary>
        public ObjectId Target { get; }
        /// <summary>保存旧概念与目标 ID，稍后由引擎适配为碎片。</summary>
        public Interpret(ConceptId concept, ObjectId target) { Concept = concept; Target = target; }
    }
}
