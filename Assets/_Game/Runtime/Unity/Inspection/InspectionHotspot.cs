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
        private InspirationVisual inspiration;
        /// <summary>稳定热点身份；文字、美术变化不改变应用层动作标识。</summary>
        public string ActionId => actionId;
        /// <summary>绑定碰撞盒对应的局部强调层与可观察方向。</summary>
        public void Configure(string id, Renderer renderer, Vector3 normal) { actionId = id; accent = renderer; outward = normal; }
        private void Awake()
        {
            original = accent.sharedMaterial.color;
            block = new MaterialPropertyBlock();
            // 灵感效果只在需要它的短语上存在，其他物体热点保持轻量。
            inspiration = GetComponent<InspirationVisual>();
        }
        /// <summary>角度检查仅约束能否看到细节，不影响世界事实。</summary>
        public bool Faces(Camera camera) => Vector3.Dot(transform.TransformDirection(outward).normalized, (camera.transform.position - transform.position).normalized) > .06f;
        /// <summary>轻微提亮共享材质的当前实例，避免每次悬停复制材质。</summary>
        public void SetHover(bool hover)
        {
            // 没有永久黄框；离开热点立即恢复原色。
            block.SetColor(ShaderIds.BaseColor, hover ? Color.Lerp(original, new Color(1, .88f, .53f), .35f) : original);
            accent.SetPropertyBlock(block);
            if (inspiration != null) inspiration.SetHover(hover);
        }
        /// <summary>观察成功后标记视觉；本方法不判断奖励、不修改任何领域集合。</summary>
        public void MarkObserved()
        {
            if (inspiration != null) inspiration.MarkObserved();
            else
            {
                // 普通近景物体仍可以提供一个预制的轻勾线，不要求接入书本效果。
                var line = transform.Find("ObservedUnderline");
                if (line != null) line.gameObject.SetActive(true);
            }
        }
    }
}
