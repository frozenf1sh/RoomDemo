// =============================================================================
// 文件：ObservationDiscovery.cs
// 职责：定义玩家亲点近景细节后能获得什么，以及该细节的世界前置条件。
// 边界：不包含相机、页动画或热点坐标；不能替玩家授予其他场景物体。
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
namespace Game.Domain
{
    /// <summary>一个具体物体中的细节发现表；页许可由应用层检视状态再约束。</summary>
    public sealed class ObservationDiscovery
    {
        /// <summary>玩家必须已亲自认识的真实物体来源。</summary>
        public ObjectId Source { get; }
        /// <summary>来源内的稳定细节 ID，例如新芽或书中文字；不是显示名。</summary>
        public string DetailId { get; }
        /// <summary>明确观察后的中文描述。</summary>
        public string Description { get; }
        /// <summary>世界前置事实未满足时显示的中文反馈。</summary>
        public string LockedDescription { get; }
        /// <summary>世界条件，与近景页状态分别验证。</summary>
        public IReadOnlyList<FactId> Requires { get; }
        /// <summary>首次观察才授予的碎片；内容校验禁止在此授予物体。</summary>
        public IReadOnlyList<ClueId> Grants { get; }
        /// <summary>复制内容数组，令 UI 不能修改授予内容或跳过世界条件。</summary>
        public ObservationDiscovery(ObjectId source, string detail, string description, FactId[] requires, ClueId[] grants, string locked = "这个细节还需要再观察。")
        {
            Source = source; DetailId = detail; Description = description; LockedDescription = locked;
            // 条件与结果都复制，防止外部数组把已创建规则悄悄改写。
            Requires = Array.AsReadOnly(requires.ToArray()); Grants = Array.AsReadOnly(grants.ToArray());
        }
    }
}
