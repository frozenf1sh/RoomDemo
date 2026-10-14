// =============================================================================
// 文件：NotebookWordColumnLayout.cs
// 职责：让笔记本词性列的视窗与内容区域跟随人工调整后的外框尺寸。
// 边界：只维护 UI 裁切与布局，不读取或修改任何游戏事实和词条状态。
// =============================================================================
using UnityEngine;

namespace Game.Unity
{
    /// <summary>将词条列内部的视窗拉伸至父面板，避免调整 Prefab 外框后仍沿用旧的裁切尺寸。</summary>
    [ExecuteAlways]
    public sealed class NotebookWordColumnLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;

        /// <summary>组件启用及 Prefab 载入时恢复内部锚点。</summary>
        private void OnEnable()
        {
            ResolveReferences();
            FitToColumn();
        }

        /// <summary>人工修改外框尺寸后，下一帧让裁切区同步扩展。</summary>
        private void OnRectTransformDimensionsChange()
        {
            ResolveReferences();
            FitToColumn();
        }

        /// <summary>Inspector 修改引用或 Prefab 应用修改时保持可编辑状态一致。</summary>
        private void OnValidate()
        {
            ResolveReferences();
            FitToColumn();
        }

        /// <summary>按稳定层级补回引用，允许美术人员只调整位置、尺寸或图片。</summary>
        private void ResolveReferences()
        {
            if (viewport == null) viewport = transform.Find("视窗") as RectTransform;
            if (content == null && viewport != null) content = viewport.Find("词条内容") as RectTransform;
        }

        /// <summary>保留细小内边距，同时让滚动内容使用视窗完整宽度。</summary>
        private void FitToColumn()
        {
            if (viewport == null || content == null) return;

            // 先扩展裁切窗口，避免新增词条仍被生成阶段的旧尺寸截断。
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(.5f, .5f);
            viewport.offsetMin = new Vector2(8f, 6f);
            viewport.offsetMax = new Vector2(-8f, -6f);

            // 内容顶部固定，布局组只负责纵向累积每张动态词条卡片。
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
        }
    }
}
