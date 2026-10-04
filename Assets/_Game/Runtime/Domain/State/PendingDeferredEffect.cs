// =============================================================================
// 文件：PendingDeferredEffect.cs
// 职责：记录已登记的一次性延迟反应，在显式推进日序后添加其事实输出。
// 边界：只存日序与领域 ID；没有时、分、真实时钟或动画延迟。
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    /// <summary>不可变待执行效果，产出内容在登记时复制。</summary>
    public sealed class PendingDeferredEffect
    {
        /// <summary>登记来源，用于去重以及到期后消费队列项。</summary>
        public string ReactionId { get; }
        /// <summary>达到此日序后可执行；不是现实日期或时间戳。</summary>
        public int DueDay { get; }
        /// <summary>登记时冻结的输出事实；执行时仍走普通事实去重。</summary>
        public IReadOnlyList<FactId> Produces { get; }
        /// <summary>构造已验证规则的待执行快照，外部不能修改其产出集合。</summary>
        public PendingDeferredEffect(string id, int day, IReadOnlyList<FactId> produces)
        {
            // 即使策划随后改动原数组，已经排定的反应也保持登记时的内容。
            ReactionId = id; DueDay = day; Produces = Array.AsReadOnly(produces.ToArray());
        }
    }
}
