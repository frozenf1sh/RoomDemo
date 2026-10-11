// 文件：NotebookPaperPlacement.cs
// 职责：把右页空处的真实点击传给有限纸面。
// 边界：不拥有词条事实，不提交配方，只放下已经选择的临时词。
using UnityEngine.EventSystems;

namespace Game.Unity
{
    /// <summary>与拖动并存的两次点击操作，避免只能依赖按住拖动。</summary>
    public sealed class NotebookPaperPlacement : UnityEngine.MonoBehaviour, IPointerClickHandler
    {
        /// <summary>纸面输入所有者，随笔记本会话绑定。</summary>
        public NotebookCanvasView Owner { get; set; }
        /// <summary>只有左键点击空纸面才尝试放置，未选择时保持原样。</summary>
        public void OnPointerClick(PointerEventData data)
        {
            // 卡片和提交按钮自行处理点击，不向空纸面冒泡。
            if (data.button == PointerEventData.InputButton.Left)
                Owner?.PlacePicked(data.position, data.pressEventCamera);
        }
    }
}
