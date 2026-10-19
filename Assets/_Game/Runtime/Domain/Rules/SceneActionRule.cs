// =============================================================================
// 文件：SceneActionRule.cs
// 职责：定义有限的亲手操作，如洗笔、转灯和移开遮挡。
// 边界：只含身份、事实和文字；动作动画不参与规则许可。
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    /// <summary>玩家操作可见物体的明确命令，不能由演出自动提交。</summary>
    public readonly struct PerformSceneAction : ICommand
    {
        /// <summary>当前被亲点的真实来源。</summary>
        public ObjectId Source { get; }
        /// <summary>来源中有限的动作身份。</summary>
        public string ActionId { get; }
        /// <summary>记录输入，不携带输出或动画状态。</summary>
        public PerformSceneAction(ObjectId source, string actionId) { Source = source; ActionId = actionId; }
    }
    /// <summary>有限物理准备操作，不计入服从或偏离的解释历史。</summary>
    public sealed class SceneActionRule
    {
        /// <summary>用于精确匹配的动作身份。</summary>
        public string Id { get; }
        /// <summary>必须亲点并认识的来源。</summary>
        public ObjectId Source { get; }
        /// <summary>必须全部成立的事实。</summary>
        public IReadOnlyList<FactId> Requires { get; }
        /// <summary>成立后阻止该操作的事实，用于已结束或已处理的现场。</summary>
        public IReadOnlyList<FactId> Blocks { get; }
        /// <summary>操作产生的事实，不直接授予碎片。</summary>
        public IReadOnlyList<FactId> Produces { get; }
        /// <summary>有效接触同时记录的性质；只能是观察，不授予物体或完整概念。</summary>
        public IReadOnlyList<ClueId> ProducedObservations { get; }
        /// <summary>动作所需的其他已亲点来源，如洗笔必须同时认识画笔。</summary>
        public IReadOnlyList<ObjectId> AdditionalSources { get; }
        /// <summary>成功时的极短世界描述。</summary>
        public string Description { get; }
        /// <summary>缺少条件时说明可见问题。</summary>
        public string LockedDescription { get; }
        /// <summary>复制所有内容数组，防止适配层修改规则。</summary>
        public SceneActionRule(string id, ObjectId source, FactId[] requires, FactId[] produces, string description, string locked, FactId[] blocks = null, ClueId[] observations = null, ObjectId[] additionalSources = null)
        {
            // 动作只记录有限事实和当场观察，重复请求不重复制造状态或计数。
            Id = id; Source = source; Requires = Array.AsReadOnly(requires.ToArray());
            Produces = Array.AsReadOnly(produces.ToArray()); Blocks = Array.AsReadOnly((blocks ?? Array.Empty<FactId>()).ToArray());
            Description = description; LockedDescription = locked;
            ProducedObservations = Array.AsReadOnly((observations ?? Array.Empty<ClueId>()).ToArray());
            AdditionalSources = Array.AsReadOnly((additionalSources ?? Array.Empty<ObjectId>()).ToArray());
        }
    }
}
