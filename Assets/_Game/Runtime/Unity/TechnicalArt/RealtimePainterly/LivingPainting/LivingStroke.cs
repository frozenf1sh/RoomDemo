// 文件：LivingStroke.cs
// 职责：以一条真实几何路径承载流动、点亮、延伸参数。
// 边界：只操作专用笔触的 PropertyBlock；物体原材质与领域事实不参与演出。
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly
{
    /// <summary>颜料线保留轮廓，只在线内慢慢移动色泽；末端沿同一网格生长。</summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class LivingStroke : MonoBehaviour
    {
        [Range(0,1)] public float Flow;
        [Range(0,1)] public float Light;
        [Range(0,1)] public float Extend = .38f;
        [Range(0,1)] public float PainterlyIntensity = .27f;
        public float Length = 3;
        public float VisualTime;
        public float LightDistance;
        public bool AutomaticTime = true;
        private Renderer target;
        private MaterialPropertyBlock properties;

        private void OnEnable()
        {
            target = GetComponent<Renderer>();
            properties = new MaterialPropertyBlock();
            Apply();
        }

        private void Update()
        {
            if (AutomaticTime)
            {
                // 颜料每秒移动不到两厘米；光每秒九厘米，禁止为强调效果加速。
                VisualTime += Time.unscaledDeltaTime;
                if (Light > 0) LightDistance += Time.unscaledDeltaTime*.09f;
            }
            Apply();
        }

        /// <summary>设置表现值，不重建路径；0～1 参数适合 Sequence 连续插值。</summary>
        public void SetVisualState(float flow, float light, float extend, float painterlyIntensity)
        {
            Flow = Mathf.Clamp01(flow);
            Light = Mathf.Clamp01(light);
            Extend = Mathf.Clamp01(extend);
            PainterlyIntensity = Mathf.Clamp01(painterlyIntensity);
            Apply();
        }

        /// <summary>同步专用材质参数，保留同一个共享 Material。</summary>
        public void Apply()
        {
            if (target == null) return;
            target.GetPropertyBlock(properties);
            properties.SetFloat("_Flow",Flow);
            properties.SetFloat("_Light",Light);
            properties.SetFloat("_Extend",Extend);
            properties.SetFloat("_PainterlyIntensity",PainterlyIntensity);
            properties.SetFloat("_StrokeLength",Length);
            properties.SetFloat("_VisualTime",VisualTime);
            properties.SetFloat("_LightDistance",LightDistance);
            target.SetPropertyBlock(properties);
        }
    }
}
