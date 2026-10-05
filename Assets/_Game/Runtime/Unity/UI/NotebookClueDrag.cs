// =============================================================================
// 文件：NotebookClueDrag.cs
// 职责：把边栏或画布词条的指针拖动转交有限语义画布。
// 边界：只处理输入和纸面坐标，不持有领域配方或收藏状态。
// =============================================================================
using Game.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Game.Unity
{
    /// <summary>同一个拖动适配器支持边栏复制与纸面重新摆放。</summary>
    public sealed class NotebookClueDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private NotebookCanvasView canvas;
        private ClueId clue;
        private bool onCanvas;
        private RectTransform moving;
        private Vector2 offset;
        /// <summary>运行时绑定本条线索及所在区域，不修改稳定 ID。</summary>
        public void Configure(NotebookCanvasView owner, ClueId id, bool placed)
        { canvas=owner; clue=id; onCanvas=placed; }
        /// <summary>开始拾取纸面卡片或从边栏复制一条输入，缓存指针偏移。</summary>
        public void OnBeginDrag(PointerEventData data)
        {
            var point=canvas.LocalPoint(data.position,data.pressEventCamera);
            moving=onCanvas ? (RectTransform)transform : canvas.Add(clue,point);
            offset=moving != null ? moving.anchoredPosition-point : Vector2.zero;
            // 禁止一次拖动在释放时额外触发按钮点击／归还。
            data.eligibleForClick=false;
        }
        /// <summary>拖动只更新纸面位置；匹配提示由画布转交会话查询。</summary>
        public void OnDrag(PointerEventData data)
        { if(moving!=null) canvas.Move(clue,canvas.LocalPoint(data.position,data.pressEventCamera)+offset); }
        /// <summary>释放到纸面外归还词条，释放到内部保留临时摆放。</summary>
        public void OnEndDrag(PointerEventData data)
        { if(moving!=null) canvas.EndDrag(clue,data.position,data.pressEventCamera); moving=null; }
    }
}
