// 文件：AtelierCanvasWorkspaceReferences.cs
// 职责：序列化母版内部引用。边界：不搜索场景、不持有世界会话。
using UnityEngine;
using UnityEngine.UI;
namespace Game.Unity.UI.FourStage
{
    /// <summary>装配通过显式引用注入。</summary>
    public sealed class AtelierCanvasWorkspaceReferences : MonoBehaviour
    {
        public CanvasGroup Group;
        [Tooltip("世界模式直接显示实际画布；仅在独立作品预览中开启，避免覆盖世界射线对应的表面。")]
        public bool ShowArtworkInUI;
        public RawImage Artwork, Brush;
        public AspectRatioFitter BrushAspect;
        public Slider Size, Angle, Paint;
        public Text SizeLabel, AngleLabel, CountLabel, Status;
        public Button Undo, Finish, Return, Knife;
    }
}
