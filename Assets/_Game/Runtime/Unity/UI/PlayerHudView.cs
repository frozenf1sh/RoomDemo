// =============================================================================
// 文件：PlayerHudView.cs
// 职责：检视／笔记时收起房间说明，把近景新发现通知移到侧边，避免遮挡主物品。
// 边界：只管理自己绑定的 UI 节点，不操作世界物体，也不读取或推导领域规则。
// =============================================================================
using UnityEngine;
namespace Game.Unity
{
    /// <summary>按当前输入界面收敛玩家 HUD；每次模式切换只刷新一次。</summary>
    public sealed class PlayerHudView : MonoBehaviour
    {
        [SerializeField] private InspectionController inspection;
        [SerializeField] private NotebookView notebook;
        [SerializeField] private GameObject[] roomOnlyText;
        [SerializeField] private RectTransform discoveryNotice;
        private bool captured, lastOpen, lastInspection;
        private Vector2 noticeMin, noticeMax, noticeOffsetMin, noticeOffsetMax;
        /// <summary>显式绑定房间说明，按钮闪烁与获取通知不在隐藏清单里。</summary>
        public void Configure(InspectionController controller, NotebookView notes, GameObject[] roomText, RectTransform notice)
        { inspection = controller; notebook = notes; roomOnlyText = roomText; discoveryNotice = notice; }
        private void Awake()
        {
            // 布局快照只保存 UI 坐标；退出近景后恢复原本醒目的中央通知。
            noticeMin = discoveryNotice.anchorMin; noticeMax = discoveryNotice.anchorMax;
            noticeOffsetMin = discoveryNotice.offsetMin; noticeOffsetMax = discoveryNotice.offsetMax;
        }
        private void LateUpdate()
        {
            bool inspecting = inspection.IsOpen, open = inspecting || notebook.IsOpen;
            if (captured && lastOpen == open && lastInspection == inspecting) return;
            captured = true; lastOpen = open; lastInspection = inspecting;
            // 仅收起 UI 文本，检视系统仍拥有相机与输入的切换。
            foreach (var node in roomOnlyText) node.SetActive(!open);
            // 近景物品位于中央，通知改在右侧；不改变五秒期限或未读闪烁行为。
            discoveryNotice.anchorMin = inspecting ? new Vector2(.735f, .64f) : noticeMin;
            discoveryNotice.anchorMax = inspecting ? new Vector2(.985f, .87f) : noticeMax;
            discoveryNotice.offsetMin = inspecting ? Vector2.zero : noticeOffsetMin;
            discoveryNotice.offsetMax = inspecting ? Vector2.zero : noticeOffsetMax;
        }
    }
}
