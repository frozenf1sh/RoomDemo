// =============================================================================
// 文件：FixedLook.cs
// 职责：保留旧场景和自动验收使用的固定观察点控制接口。
// 边界：只改变旋转，不提供位移、物理角色或世界规则。
// =============================================================================
using UnityEngine;
namespace Game.Unity
{
    /// <summary>固定观察点不读取玩家鼠标或键盘来改变朝向。</summary>
    public sealed class FixedLook : MonoBehaviour
    {
        [SerializeField] private NotebookView notebook;
        [SerializeField] private float sensitivity = 0.12f;
        private float yaw;
        private float pitch;
        private Quaternion origin;
        /// <summary>保留历史场景生成器的绑定接口；正式玩家输入不依赖该引用。</summary>
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
    }
}
