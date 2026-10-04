// =============================================================================
// 文件：FixedLook.cs
// 职责：提供固定观察点的受限鼠标转头与视角复位。
// 边界：只改变旋转，不提供位移、物理角色或世界规则。
// =============================================================================
using UnityEngine;
using UnityEngine.InputSystem;
namespace Game.Unity
{
    /// <summary>提供固定观察点的受限鼠标转头与视角复位。</summary>
    public sealed class FixedLook : MonoBehaviour
    {
        [SerializeField] private NotebookView notebook;
        [SerializeField] private float sensitivity = 0.12f;
        private float yaw;
        private float pitch;
        private Quaternion origin;
        /// <summary>显式关联笔记本，打开面板时暂停转头。</summary>
        public void Configure(NotebookView view) { notebook = view; }
        /// <summary>缓存固定观察点的初始旋转。</summary>
        private void Awake() { origin = transform.localRotation; }
        /// <summary>将鼠标增量转换为受限偏航和俯仰角。</summary>
        public void ApplyLook(Vector2 delta)
        {
            // 仅累计角度并夹紧范围，固定位置不会随输入改变。
            yaw = Mathf.Clamp(yaw + delta.x * sensitivity, -80f, 80f);
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -45f, 45f);
            transform.localRotation = origin * Quaternion.Euler(pitch, yaw, 0);
        }
        /// <summary>处理转头和复位按键，不处理世界规则。</summary>
        private void Update()
        {
            // 输入设备缺失或笔记本打开时暂停转头，避免界面选择时晃动画面。
            if (Mouse.current == null || notebook.IsOpen || notebook.ExternalInputBlocked) return;
            if (Mouse.current.rightButton.isPressed) ApplyLook(Mouse.current.delta.ReadValue());
            if (Keyboard.current != null && Keyboard.current.homeKey.wasPressedThisFrame)
            { yaw = pitch = 0; transform.localRotation = origin; }
        }
    }
}
