// =============================================================================
// 文件：InteractionSystem.cs
// 职责：通过相机射线寻找交互实体，提交调查并显示中文悬停反馈。
// 边界：UI 遮挡时不调查场景；是否可获得内容由 Session 查询决定。
// =============================================================================
using System.Linq;
using Game.Application;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Game.Unity
{
    /// <summary>通过相机射线寻找交互实体，提交调查并显示中文悬停反馈。</summary>
    public sealed class InteractionSystem : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Text hoverText;
        [SerializeField] private InspectionController inspection;
        private GameSession session;
        private NotebookView notebook;
        private Clickable previousTarget;
        private bool previousInspectable;
        private bool previousKnown;
        private bool hasHoverSnapshot;
        /// <summary>注入射线相机与悬停文本。</summary>
        public void ConfigureInspection(InspectionController controller) { inspection = controller; }
        /// <summary>注入射线相机与悬停文本。</summary>
        public void Configure(Camera camera, Text hover) { viewCamera = camera; hoverText = hover; }
        /// <summary>绑定当前会话及笔记本遮挡状态。</summary>
        public void Bind(GameSession game, NotebookView view) { session = game; notebook = view; hasHoverSnapshot = false; }
        /// <summary>查询射线首先命中的 Collider 上的交互组件。</summary>
        public Clickable FindClickable(Vector2 screenPoint)
        {
            // 射线只查询第一个命中，墙和装饰 Collider 可遮挡交互。
            if (Physics.Raycast(viewCamera.ScreenPointToRay(screenPoint), out var hit, 30f))
                return hit.collider.GetComponent<Clickable>();
            return null;
        }
        /// <summary>把屏幕坐标的交互命中提交为调查命令。</summary>
        public bool InspectAt(Vector2 screenPoint)
        {
            var target = FindClickable(screenPoint);
            if (target == null) return false;
            if (inspection != null && session.Rules.Observations.Count > 0 && target.TryGetComponent<InspectableObject>(out var item)) return inspection.Enter(item);
            target.Inspect(session); return true;
        }
        /// <summary>处理鼠标与 UI 遮挡，显示领域查询得到的中文反馈。</summary>
        private void Update()
        {
            if (Mouse.current == null || session == null) return;
            // 统一判断 UI 遮挡，避免点击按钮同时调查其后的世界物体。
            bool blocked = notebook.IsOpen || notebook.ExternalInputBlocked || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
            var target = blocked ? null : FindClickable(Mouse.current.position.ReadValue());
            // 中文显示来源于内容定义；业务许可来源于状态查询。
            bool inspectable = target != null && session.IsInspectable(target.Entity.Id);
            bool known = target != null && session.State.Knows(target.Entity.Id);
            // 只在目标或可调查状态变化时生成文字，避免悬停期间每帧 LINQ 与字符串分配。
            if (!hasHoverSnapshot || target != previousTarget || inspectable != previousInspectable || known != previousKnown)
            {
                hasHoverSnapshot = true; previousTarget = target; previousInspectable = inspectable; previousKnown = known;
                // 未成功观察前不替物体命名；名字是亲自看过之后留在笔记里的奖励。
                hoverText.text = target == null ? "" : !inspectable ? "这里还藏着什么。"
                    : !known ? (target.GetComponent<InspectableObject>() != null ? "拿近看看" : "仔细看看")
                    : "再看看 · " + session.Rules.Discoveries.First(x => x.Source == target.Entity.Id).DisplayName;
            }
            if (!blocked && Mouse.current.leftButton.wasPressedThisFrame) InspectAt(Mouse.current.position.ReadValue());
        }
    }
}
