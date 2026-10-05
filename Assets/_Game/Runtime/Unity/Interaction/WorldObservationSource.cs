// =============================================================================
// 文件：WorldObservationSource.cs
// 职责：将玩家这一次房间亲点转交内容表中已开放的观察细节。
// 边界：只提交来源和细节身份；许可、去重和授予由领域负责，不自行发放物体。
// =============================================================================
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>窗、天花板和地板的轻量观察适配器；不在 Update 检查世界规则。</summary>
    public sealed class WorldObservationSource : MonoBehaviour
    {
        /// <summary>只在玩家明确点击当前物体之后调用；已知观察不重复播放发现反馈。</summary>
        public void ObserveAvailable(GameSession session, ObjectId source)
        {
            // 使用内容条件挑选当前细节，命令仍会再次验证来源与许可。
            foreach (var detail in session.Rules.Observations.Where(x => x.Source == source &&
                x.Requires.All(session.State.HasFact) && x.Grants.Any(c => !session.State.Knows(c))))
                session.ObserveDetail(source, detail.DetailId);
        }
    }
}
