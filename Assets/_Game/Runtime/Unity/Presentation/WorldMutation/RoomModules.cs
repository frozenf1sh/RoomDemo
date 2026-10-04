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
        /// <summary>室内基础地板，可被保留木台与草地视觉替换。</summary>
        public Transform Floor => floor;
        /// <summary>独立天花板；不能包含在不可拆的房间整体网格中。</summary>
        public Transform Ceiling => ceiling;
        /// <summary>含窗洞四周墙片的后墙模块。</summary>
        public Transform WallBack => wallBack;
        /// <summary>左墙模块，其局部位移不影响保留家具。</summary>
        public Transform WallLeft => wallLeft;
        /// <summary>右墙模块，与左墙分别保存和恢复姿态。</summary>
        public Transform WallRight => wallRight;
        /// <summary>窗的整个视觉边界，不包含实体身份和调查 Collider。</summary>
        public Transform WindowBoundary => windowBoundary;
        /// <summary>随室内地板收起的接缝、踢脚线等装饰。</summary>
        public Transform FloorDetails => floorDetails;
        /// <summary>只绑定独立节点；本组件没有 Update 或规则求值。</summary>
        public void Configure(Transform ground, Transform roof, Transform back, Transform left, Transform right, Transform window, Transform seams)
        {
            // 引用都是表现对象；是否打开房间由只读事实的消费者决定。
            floor = ground; ceiling = roof; wallBack = back; wallLeft = left; wallRight = right;
            windowBoundary = window; floorDetails = seams;
        }
    }
}
