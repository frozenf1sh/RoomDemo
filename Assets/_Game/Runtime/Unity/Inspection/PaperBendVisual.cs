// =============================================================================
// 文件：PaperBendVisual.cs
// 职责：把一次翻页的归一化进度写入纸张弯曲 Shader。
// 边界：组件只操作自己拥有的 Renderer PropertyBlock，不控制检视动作或世界状态。
// =============================================================================
using UnityEngine;

namespace Game.Unity
{
    /// <summary>离散页状态之间的一张临时弯曲纸页；网格与材质由 Editor 预先生成。</summary>
    public sealed class PaperBendVisual : MonoBehaviour
    {
        [SerializeField] private Renderer paper;
        private MaterialPropertyBlock block;
        private static readonly int Turn = Shader.PropertyToID("_Turn");

        /// <summary>设置自己负责的纸页，禁止借此修改其他物体的视觉。</summary>
        public void Configure(Renderer renderer) => paper = renderer;

        /// <summary>更新纸面卷曲；0 为平躺右页，1 为越过书脊后平躺左页。</summary>
        /// <param name="progress">只描述动画进度，输入被限制在 0～1。</param>
        public void ApplyProgress(float progress)
        {
            if (block == null) block = new MaterialPropertyBlock();
            // 保留同一 Renderer 上其他视觉参数；不修改共享 Material，也不产生实例 Material。
            paper.GetPropertyBlock(block);
            block.SetFloat(Turn, Mathf.Clamp01(progress));
            paper.SetPropertyBlock(block);
        }
    }
}
