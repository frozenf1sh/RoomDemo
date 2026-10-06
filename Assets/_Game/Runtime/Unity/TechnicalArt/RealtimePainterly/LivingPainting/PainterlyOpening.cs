// 文件：PainterlyOpening.cs
// 职责：沿既有笔势控制可见表面的局部擦开。
// 边界：仅改变展示面板的几何可见性；不判定门槛，不产生房间事实。
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly
{
    /// <summary>原墙面保留普通材质光照，擦开的像素真的露出后方几何。</summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class PainterlyOpening : MonoBehaviour
    {
        [Range(0,1)] public float Rewrite;
        private Renderer target;
        private MaterialPropertyBlock properties;

        private void OnEnable()
        {
            target=GetComponent<Renderer>();
            properties=new MaterialPropertyBlock();
            Apply();
        }
        private void Update() => Apply();

        /// <summary>控制擦开范围；可连续插值，后方仍由正常摄像机绘制。</summary>
        public void SetRewrite(float value) { Rewrite=Mathf.Clamp01(value); Apply(); }

        private void Apply()
        {
            if (target==null) return;
            // 不交换成天空材质；该面板只裁去笔势经过的位置。
            target.GetPropertyBlock(properties);
            properties.SetFloat("_Rewrite",Rewrite);
            target.SetPropertyBlock(properties);
        }
    }
}
