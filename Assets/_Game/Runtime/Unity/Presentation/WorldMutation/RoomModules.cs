// =============================================================================
// 文件：RoomModules.cs
// 职责：显式引用可独立变化的建筑部分与窗视觉边界。
// 边界：只是表现层的场景绑定，不保存世界事实，也不判定建筑何时改变。
// =============================================================================
using UnityEngine;
namespace Game.Unity
{
    /// <summary>模块化房间的轻量引用表，供具体世界演出使用。</summary>
    public sealed class RoomModules : MonoBehaviour
    {
        [SerializeField] private Transform floor, ceiling, wallBack, wallLeft, wallRight, windowBoundary, floorDetails;
        public Transform Floor => floor;
        public Transform Ceiling => ceiling;
        public Transform WallBack => wallBack;
        public Transform WallLeft => wallLeft;
        public Transform WallRight => wallRight;
        public Transform WindowBoundary => windowBoundary;
        public Transform FloorDetails => floorDetails;
        /// <summary>只绑定独立节点；本组件没有 Update 或规则求值。</summary>
        public void Configure(Transform ground, Transform roof, Transform back, Transform left, Transform right, Transform window, Transform seams)
        { floor = ground; ceiling = roof; wallBack = back; wallLeft = left; wallRight = right; windowBoundary = window; floorDetails = seams; }
    }
}
