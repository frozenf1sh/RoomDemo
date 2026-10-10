// 文件：AtelierArtState.cs
// 职责：为独立画室切换普通 HDRI 与后期程序化星夜，并持有本场景专用渲染管线。
// 架构边界：只改表现，不读写 GameState，不自动推进旧玩法；后期状态由显式调用触发。
using UnityEngine;
using UnityEngine.Rendering;
using Game.Unity.TechnicalArt.RealtimePainterly.Production;

namespace Game.Unity.ArtRebuild
{
    /// <summary>独立美术状态；正常启动始终使用普通天空和完整屋顶。</summary>
    [ExecuteAlways, DefaultExecutionOrder(-200)]
    public sealed class AtelierArtState : MonoBehaviour
    {
        public RenderPipelineAsset ScenePipeline;
        public Material NormalHdri;
        public GameObject ClosedRoof;
        public GameObject RevealRim;
        public LivingSkyController LivingSky;
        /// <summary>本场景主镜头；预览时只改变取景方向，不移动任何玩法对象。</summary>
        public Camera ViewCamera;
        /// <summary>开局取景更多关注桌面与窗口，减少完整屋顶的空白占比。</summary>
        public Vector3 NormalViewTarget = new Vector3(.08f, 2.20f, 1.65f);
        /// <summary>后期略微抬头，让屋顶流动星夜成为主视觉。</summary>
        public Vector3 LateViewTarget = new Vector3(.08f, 2.83f, 1.65f);
        private RenderPipelineAsset previousPipeline;

        /// <summary>当前仅供美术预览的后期状态，不代表任何领域事实。</summary>
        public bool LateSkyActive { get; private set; }

        private void OnEnable()
        {
            previousPipeline = QualitySettings.renderPipeline;
            if (ScenePipeline != null) QualitySettings.renderPipeline = ScenePipeline;
            SetLateSky(false);
        }

        private void OnDisable()
        {
            // 离开画室时只恢复本组件实际接管的管线，不覆盖其他系统随后选择的管线。
            if (QualitySettings.renderPipeline == ScenePipeline) QualitySettings.renderPipeline = previousPipeline;
        }

        /// <summary>明确切换视觉状态；不会授予碎片、推进日序或产生交互事实。</summary>
        /// <param name="enabled">是否预览后期完整流动星夜。</param>
        public void SetLateSky(bool enabled)
        {
            LateSkyActive = enabled;
            if (NormalHdri != null) RenderSettings.skybox = NormalHdri;
            if (ClosedRoof != null) ClosedRoof.SetActive(!enabled);
            if (RevealRim != null) RevealRim.SetActive(enabled);
            // 两个构图共用同一机位，只有显式的美术状态切换才改变视线。
            if (ViewCamera != null) ViewCamera.transform.LookAt(enabled ? LateViewTarget : NormalViewTarget);
            if (LivingSky == null) return;
            LivingSky.InitialCoverage = 0;
            LivingSky.InitialLight = 1;
            LivingSky.InitialFlow = 1;
            LivingSky.SetCoverage(enabled ? 1 : 0);
            LivingSky.SetFlow(1);
            LivingSky.LightTo(1, 0);
        }
    }
}
