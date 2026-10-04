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
        public string ReactionId { get; }
        public int DueDay { get; }
        public IReadOnlyList<FactId> Produces { get; }
        public PendingDeferredEffect(string id, int day, IReadOnlyList<FactId> produces)
        { ReactionId = id; DueDay = day; Produces = Array.AsReadOnly(produces.ToArray()); }
    }
}
