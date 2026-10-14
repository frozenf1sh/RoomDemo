// 文件：HoverGoldPointer.cs
// 职责：在独立实验中用鼠标射线驱动悬停金光，处理遮挡、失焦和输入暂停。
// 边界：仅做视觉拾取；尚未接入 AtelierGameplay，不发送任何玩法命令。
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Unity.TechnicalArt.HoverGold
{
    /// <summary>测试场景的输入适配器；未来应由正式交互许可驱动相同表现入口。</summary>
    public sealed class HoverGoldPointer : MonoBehaviour
    {
        public Camera View;
        public LayerMask PickingMask = ~0;
        public bool InputBlocked;
        private HoverGoldTarget current;
        private Vector2? verificationPoint;

        /// <summary>当前射线首个命中的实验目标。</summary>
        public HoverGoldTarget Current => current;

        /// <summary>查询首个实体 Collider；普通遮挡物不会被跳过。</summary>
        public HoverGoldTarget Pick(Vector2 point)
        {
            if (View == null || !Physics.Raycast(View.ScreenPointToRay(point), out var hit, 30, PickingMask,
                    QueryTriggerInteraction.Collide)) return null;
            var target = hit.collider.GetComponentInParent<HoverGoldTarget>();
            return target != null && target.isActiveAndEnabled ? target : null;
        }

        /// <summary>为可重复截图覆盖坐标；仍经过相同物理射线，不代表真人鼠标验收。</summary>
        public void SetVerificationPoint(Vector2? point) => verificationPoint = point;

        /// <summary>取消当前拾取并清除实验坐标；下一帧允许重新拾取同一个物品。</summary>
        public void ResetEffect()
        {
            verificationPoint = null;
            SetCurrent(null);
        }

        private void Update()
        {
            bool blocked = InputBlocked || Time.timeScale == 0 ||
                (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
            Vector2? point = verificationPoint ?? (Mouse.current != null && Application.isFocused
                ? Mouse.current.position.ReadValue() : (Vector2?)null);
            SetCurrent(!blocked && point.HasValue ? Pick(point.Value) : null);
        }

        private void SetCurrent(HoverGoldTarget target)
        {
            if (current == target) return;
            if (current != null) current.SetHovered(false);
            current = target;
            if (current != null) current.SetHovered(true);
        }

        private void OnDisable()
        {
            ResetEffect();
        }
    }
}
