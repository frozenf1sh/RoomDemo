// =============================================================================
// 文件：WorldBoundaryDissolve.cs
// 职责：为一组建筑或外景 Renderer 应用星光侵蚀、噪声溶解和稀疏边缘粒子。
// 边界：进度只属于表现层；本组件不读取会话、不添加事实、不决定演出时机。
// 资源：共享材质保持不变，属性块在首次借用时缓存，重开后精确归还。
// =============================================================================
using UnityEngine;

namespace Game.Unity
{
    /// <summary>这个房间专用的溶解表面消费者，避免建筑变化只剩整体位移。</summary>
    public sealed class WorldBoundaryDissolve : MonoBehaviour
    {
        [SerializeField] private Renderer[] surfaces = new Renderer[0];
        [SerializeField] private ParticleSystem[] edgeParticles = new ParticleSystem[0];
        [SerializeField, Min(0)] private float maximumEmission = 38;
        private MaterialPropertyBlock[] originals;
        private MaterialPropertyBlock block;
        private bool captured;
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int StarExposureId = Shader.PropertyToID("_StarExposure");

        /// <summary>当前溶解比例，供具体演出和美术复查读取；不是世界事实。</summary>
        public float Progress { get; private set; }

        /// <summary>编辑器绑定被侵蚀的表面与非交互粒子；空数组表示不使用边缘粒子。</summary>
        public void Configure(Renderer[] renderers, ParticleSystem[] particles, float rate = 38)
        {
            surfaces = renderers;
            edgeParticles = particles;
            maximumEmission = rate;
        }

        /// <summary>设置表面显影与溶解；0 为实体完整，1 为表面完全消失。</summary>
        /// <param name="dissolve">噪声裁切比例，不移动建筑。</param>
        /// <param name="starExposure">实体变成星光表面的比例，先于真正溶解出现。</param>
        /// <param name="animateParticles">即时恢复时关闭过程粒子，避免加载预设重播演出。</param>
        public void Apply(float dissolve, float starExposure, bool animateParticles)
        {
            Capture();
            Progress = Mathf.Clamp01(dissolve);
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i] == null) continue;
                surfaces[i].GetPropertyBlock(block);
                block.SetFloat(DissolveId, Progress);
                block.SetFloat(StarExposureId, Mathf.Clamp01(starExposure));
                surfaces[i].SetPropertyBlock(block);
            }

            // 仅在边界实际侵蚀时释放微粒；终态和初始态都保持安静。
            float activity = animateParticles ? Mathf.Max(0, Mathf.Sin(Progress * Mathf.PI)) : 0;
            for (int i = 0; i < edgeParticles.Length; i++)
            {
                var particles = edgeParticles[i];
                if (particles == null) continue;
                var emission = particles.emission;
                emission.rateOverTime = maximumEmission * activity;
                if (activity > .01f && !particles.isPlaying) particles.Play();
                if (!animateParticles) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void Capture()
        {
            if (captured) return;
            block = new MaterialPropertyBlock();
            originals = new MaterialPropertyBlock[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++)
            {
                originals[i] = new MaterialPropertyBlock();
                if (surfaces[i] != null) surfaces[i].GetPropertyBlock(originals[i]);
            }
            captured = true;
        }

        /// <summary>取消旧演出时归还所有原属性块与粒子，不留下星光或孔洞。</summary>
        public void ResetPresentation()
        {
            if (captured)
            {
                for (int i = 0; i < surfaces.Length; i++)
                    if (surfaces[i] != null) surfaces[i].SetPropertyBlock(originals[i]);
            }
            foreach (var particles in edgeParticles)
                if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Progress = 0;
            captured = false;
        }
    }
}
