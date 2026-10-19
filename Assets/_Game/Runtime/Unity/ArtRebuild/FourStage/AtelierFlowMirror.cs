// 文件：AtelierFlowMirror.cs
// 职责：把已验收真实手镜接入现场近景，限制转角并将玩家主动转动交给流程适配器。
// 边界：镜面仅负责视觉；不修改GameState，不关闭主摄像机后处理。
using Game.Unity.TechnicalArt.RealtimePainterly.Experiments;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace Game.Unity.ArtRebuild
{
    /// <summary>水平拖动和左右方向键共用±15度上限，不要求精密对准。</summary>
    public sealed class AtelierFlowMirror : MonoBehaviour
    {
        public AtelierFlowGameplay Flow;
        public AtelierMirrorProbe Reflection;
        public Transform Pivot;
        public Transform HeldPose;
        public Collider Picking;
        public const float AngleLimit = 15;
        /// <summary>当前主动转角，用于命令门槛和取证。</summary>
        public float Angle { get; private set; }
        private Vector3 restPosition;
        private Quaternion restRotation;
        private bool held, dragging;
        private float previousX;
        private void Awake()
        {
            restPosition = Pivot.position;
            restRotation = Pivot.rotation;
            Reflection.enabled = false;
        }
        /// <summary>拿起时只改变机位和物件姿态，不授予翻转观察。</summary>
        public void Begin()
        {
            held = true;
            Angle = 0;
            Pivot.SetPositionAndRotation(HeldPose.position,HeldPose.rotation);
            Reflection.enabled = true;
        }
        /// <summary>返回时恢复原有摆放；清除本次输入捕获。</summary>
        public void End()
        {
            CancelDrag();
            held = false;
            Reflection.enabled = false;
            Pivot.SetPositionAndRotation(restPosition,restRotation);
        }
        /// <summary>失焦、笔记、暂停时取消按下，防止松开穿透。</summary>
        public void CancelDrag() => dragging = false;
        /// <summary>仅当前镜面近景消费输入；右键仍由统一返回入口处理。</summary>
        public void HandleInput()
        {
            if (!held || !Flow.GameplayInputEnabled || Flow.IsPaused) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (mouse.leftButton.wasPressedThisFrame && !overUI)
            {
                var ray = Flow.References.ViewCamera.ScreenPointToRay(mouse.position.ReadValue());
                if (Picking.Raycast(ray,out var hit,20))
                {
                    dragging = true;
                    previousX = mouse.position.ReadValue().x;
                }
            }
            if (dragging && mouse.leftButton.isPressed && !overUI)
            {
                float x = mouse.position.ReadValue().x;
                TurnTo(Angle + (x-previousX)*.13f);
                previousX = x;
            }
            if (!mouse.leftButton.isPressed || overUI) CancelDrag();
            var keys = Keyboard.current;
            if (keys != null)
            {
                float direction = (keys.rightArrowKey.isPressed ? 1 : 0) - (keys.leftArrowKey.isPressed ? 1 : 0);
                if (direction != 0) TurnTo(Angle + direction*Time.deltaTime*24);
            }
        }
        /// <summary>玩家转镜入口；大角度输入被钳制，不通过拿起动画或等待时间记观察。</summary>
        public void TurnTo(float degrees)
        {
            if (!held || !Flow.GameplayInputEnabled || Flow.IsPaused || Flow.References.Notebook.IsOpen) return;
            float next = Mathf.Clamp(degrees,-AngleLimit,AngleLimit);
            if (Mathf.Abs(next-Angle) < .001f) return;
            Angle = next;
            Pivot.rotation = HeldPose.rotation * Quaternion.Euler(0,Angle,0);
            Flow.CompareMirror();
        }
    }
}
