// =============================================================================
// 文件：InspectionHotspot.cs
// 职责：为近景复制体提供射线热点与细微悬停反馈。
// 边界：只表示热点身份和可见角度；动作许可由 InspectionModel 决定。
// =============================================================================
using UnityEngine;
namespace Game.Unity
{
    /// <summary>不常驻描边的细节热点；仅在有效状态、合适方向且鼠标经过时提示。</summary>
    public sealed class InspectionHotspot : MonoBehaviour
    {
        [SerializeField] private string actionId;
        [SerializeField] private Renderer accent;
        [SerializeField] private Vector3 outward = Vector3.back;
        private Color original;
        private MaterialPropertyBlock block;
        public string ActionId => actionId;
        public void Configure(string id, Renderer renderer, Vector3 normal) { actionId = id; accent = renderer; outward = normal; }
        private void Awake() { original = accent.sharedMaterial.color; block = new MaterialPropertyBlock(); }
        /// <summary>角度检查仅约束能否看到细节，不影响世界事实。</summary>
        public bool Faces(Camera camera) => Vector3.Dot(transform.TransformDirection(outward).normalized, (camera.transform.position - transform.position).normalized) > .06f;
        /// <summary>轻微提亮共享材质的当前实例，避免每次悬停复制材质。</summary>
        public void SetHover(bool hover)
        {
            // 没有永久黄框；离开热点立即恢复原色。
            block.SetColor(ShaderIds.BaseColor, hover ? Color.Lerp(original, new Color(1, .88f, .53f), .35f) : original);
            accent.SetPropertyBlock(block);
        }
    }
}
