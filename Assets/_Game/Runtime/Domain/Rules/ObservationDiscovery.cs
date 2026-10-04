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
        public ObjectId Source { get; }
        public string DetailId { get; }
        public string Description { get; }
        public string LockedDescription { get; }
        public IReadOnlyList<FactId> Requires { get; }
        public IReadOnlyList<ClueId> Grants { get; }
        /// <summary>复制内容数组，令 UI 不能修改授予内容或跳过世界条件。</summary>
        public ObservationDiscovery(ObjectId source, string detail, string description, FactId[] requires, ClueId[] grants, string locked = "这个细节还需要再观察。")
        {
            Source = source; DetailId = detail; Description = description; LockedDescription = locked;
            Requires = Array.AsReadOnly(requires.ToArray()); Grants = Array.AsReadOnly(grants.ToArray());
        }
    }
}
