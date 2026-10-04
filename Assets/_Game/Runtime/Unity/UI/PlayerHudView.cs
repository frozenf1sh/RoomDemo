// =============================================================================
// 文件：PlayerHudView.cs
// 职责：检视时收起房间说明，保留笔记本按钮和新发现通知，避免两个界面叠字。
// 边界：只管理自己绑定的 UI 节点，不操作世界物体，也不读取或推导领域规则。
// =============================================================================
using UnityEngine;
namespace Game.Unity
{
    /// <summary>按当前输入界面收敛玩家 HUD；每次模式切换只刷新一次。</summary>
    public sealed class PlayerHudView : MonoBehaviour
    {
        [SerializeField] private InspectionController inspection;
        [SerializeField] private GameObject[] roomOnlyText;
        private bool captured, lastOpen;
        /// <summary>显式绑定房间说明，按钮闪烁与获取通知不在隐藏清单里。</summary>
        public void Configure(InspectionController controller, GameObject[] roomText) { inspection = controller; roomOnlyText = roomText; }
        private void LateUpdate()
        {
            bool open = inspection.IsOpen;
            if (captured && lastOpen == open) return;
            captured = true; lastOpen = open;
            // 仅收起 UI 文本，检视系统仍拥有相机与输入的切换。
            foreach (var node in roomOnlyText) node.SetActive(!open);
        }
    }
}
