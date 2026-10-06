// 文件：PainterlyCamera.cs
// 职责：提供摄像机专属的实时绘画参数，供 Benchmark 与以后 Presenter/Sequence 调用。
// 边界：仅 Unity 表现层，不读取或写入领域状态，不替换场景材质。
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly
{
    /// <summary>实时绘画渲染的摄像机入口；没有该组件的摄像机不会执行效果。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class PainterlyCamera : MonoBehaviour
    {
        [Header("绘画强度")]
        [Range(0, 1)] public float PainterlyIntensity = 1;
        [Header("各向异性色彩整理")]
        [Range(2, 12)] public int KernelSize = 6;
        [Range(0.2f, 2)] public float Anisotropy = 1;
        [Range(1, 12)] public float Sharpness = 8;
        [Range(0.2f, 8)] public float Hardness = 1;
        [Header("世界空间笔触")]
        [Range(0.025f, 0.25f)] public float PatchSize = 0.095f;
        [Range(0, 1)] public float PatchBlend = 0.65f;
        [Range(0, 0.4f)] public float PigmentVariation = 0.10f;
        [Range(0, 1)] public float HighlightStrength = 0.45f;
        public Light DirectionalLight;
        public bool DebugWorldCells;

        /// <summary>设置统一强度；0 返回普通渲染，1 完整应用当前摄像机的绘画方案。</summary>
        /// <param name="intensity">将被限制到 0～1 的表现参数，不改变任何世界事实。</param>
        public void SetPainterlyIntensity(float intensity)
        {
            // 调用方只需要控制这个入口，不必枚举物体 Renderer 或更换材质。
            PainterlyIntensity = Mathf.Clamp01(intensity);
        }
    }
}
