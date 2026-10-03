// =============================================================================
// 文件：Rules.cs
// 职责：定义自动反应数据与第一版物体、概念和解释的兼容结构。
// 边界：规则只含 ID、前置条件与产出；不依赖场景或渲染 API。
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    /// <summary>第一版物体定义的兼容视图，不再拥有独立线索状态。</summary>
    public sealed class ObjectDefinition
    {
        /// <summary>内容的稳定身份，用于查找与去重。</summary>
        public ObjectId Id { get; }
        /// <summary>面向玩家的中文名称，供旧 API 兼容查询使用。</summary>
        public string Label { get; }
        /// <summary>成功观察时显示的中文描述。</summary>
        public string Description { get; }
        /// <summary>必须全部成立的前置事实。</summary>
        public IReadOnlyList<FactId> Requires { get; }
        /// <summary>由统一定义派生的旧概念视图或该物体授予的概念。</summary>
        public IReadOnlyList<ConceptId> Concepts { get; }
        /// <summary>旧发现结构中额外授予的物体身份。</summary>
        public IReadOnlyList<ObjectId> RevealsObjects { get; }
        /// <summary>复制旧物体定义中的条件、概念与额外发现物体列表。</summary>
        public ObjectDefinition(ObjectId id, string label, string description, FactId[] requires, ConceptId[] concepts, ObjectId[] revealsObjects)
        {
            Id = id; Label = label; Description = description;
            // 先克隆再只读包装，外部修改传入数组不会改变已装载规则。
            Requires = Array.AsReadOnly((FactId[])requires.Clone());
            Concepts = Array.AsReadOnly((ConceptId[])concepts.Clone());
            RevealsObjects = Array.AsReadOnly((ObjectId[])revealsObjects.Clone());
        }
    }
    /// <summary>第一版概念定义的兼容视图。</summary>
    public sealed class ConceptDefinition
    {
        /// <summary>内容的稳定身份，用于查找与去重。</summary>
        public ConceptId Id { get; }
        /// <summary>面向玩家的中文名称，供旧 API 兼容查询使用。</summary>
        public string Label { get; }
        /// <summary>保存概念 ID 及中文显示名称。</summary>
        public ConceptDefinition(ConceptId id, string label) { Id = id; Label = label; }
    }
    /// <summary>第一版概念加物体配方的数据适配入口。</summary>
    public sealed class InterpretationRule
    {
        /// <summary>旧命令或事件中的概念身份。</summary>
        public ConceptId Concept { get; }
        /// <summary>旧二元解释的目标物体身份。</summary>
        public ObjectId Target { get; }
        /// <summary>必须全部成立的前置事实。</summary>
        public IReadOnlyList<FactId> Requires { get; }
        /// <summary>规则成立时添加的领域事实，不指定 Unity 表现。</summary>
        public IReadOnlyList<FactId> Produces { get; }
        /// <summary>复制二元规则输入条件与事实输出。</summary>
        public InterpretationRule(ConceptId concept, ObjectId target, FactId[] requires, FactId[] produces)
        {
            Concept = concept; Target = target;
            // 先克隆再只读包装，外部修改传入数组不会改变已装载规则。
            Requires = Array.AsReadOnly((FactId[])requires.Clone());
            Produces = Array.AsReadOnly((FactId[])produces.Clone());
        }
    }
    /// <summary>只触发一次的自动世界反应数据。</summary>
    public sealed class ReactionRule
    {
        /// <summary>内容的稳定身份，用于查找与去重。</summary>
        public string Id { get; }
        /// <summary>必须全部成立的前置事实。</summary>
        public IReadOnlyList<FactId> Requires { get; }
        /// <summary>规则成立时添加的领域事实，不指定 Unity 表现。</summary>
        public IReadOnlyList<FactId> Produces { get; }
        /// <summary>校验反应 ID 并保存只读条件与产出。</summary>
        public ReactionRule(string id, FactId[] requires, FactId[] produces)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("反应标识符不能为空。");
            Id = id; Requires = Array.AsReadOnly((FactId[])requires.Clone()); Produces = Array.AsReadOnly((FactId[])produces.Clone());
        }
    }
}
