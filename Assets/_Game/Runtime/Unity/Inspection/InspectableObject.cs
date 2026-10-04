// =============================================================================
// 文件：InspectableObject.cs
// 职责：把房间实体与独立的近距离检视 Prefab 关联。
// 边界：不挪动房间真实物体；替换近景视觉不改变 SceneEntity 的稳定身份。
// =============================================================================
using UnityEngine;
namespace Game.Unity
{
    /// <summary>房间交互对象的组合组件，检视模型可以与房间模型不同。</summary>
    public sealed class InspectableObject : MonoBehaviour
    {
        [SerializeField] private SceneEntity entity;
        [SerializeField] private GameObject inspectVisual;
        public SceneEntity Entity => entity;
        public GameObject InspectVisual => inspectVisual;
        /// <summary>场景工具显式绑定来源与展示 Prefab，运行中只读取这些引用。</summary>
        public void Configure(SceneEntity source, GameObject visual) { entity = source; inspectVisual = visual; }
    }
}
