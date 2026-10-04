// =============================================================================
// 文件：ArtistPlantVisual.cs
// 职责：驱动艺术盆栽的枝条显现、叶片展开与梦境叶层，不放大整株模型。
// 边界：只接收 Presenter 给出的表现目标；不读写领域事实，不创建材质副本。
// =============================================================================
using UnityEngine;

namespace Game.Unity
{
    /// <summary>单株植物的具体美术动画；枝条和叶片共享材质，以属性块保留独立生长节奏。</summary>
    public sealed class ArtistPlantVisual : MonoBehaviour
    {
        [SerializeField] private GameObject baseLayer;
        [SerializeField] private GameObject grownLayer;
        [SerializeField] private GameObject dreamLayer;
        [SerializeField] private Renderer[] baseParts;
        [SerializeField] private Renderer[] grownParts;
        [SerializeField] private float[] grownDelays;
        [SerializeField] private Renderer[] dreamParts;
        [SerializeField] private float[] dreamDelays;
        [SerializeField] private ParticleSystem growthMotes;
        [SerializeField] private ParticleSystem waterDrops;
        [SerializeField, Min(.1f)] private float growthDuration = 6.5f;
        [SerializeField, Min(.1f)] private float dreamDuration = 3.8f;

        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int UnfurlId = Shader.PropertyToID("_Unfurl");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int WindId = Shader.PropertyToID("_WindStrength");
        private MaterialPropertyBlock block;
        private float growth, dream, targetGrowth, targetDream;

        /// <summary>已展现的枝条生长比例，供视觉调试使用，不能作为游戏规则条件。</summary>
        public float GrowthProgress => growth;
        /// <summary>已展现的梦境形态比例，供导演与调试恢复查看。</summary>
        public float DreamProgress => dream;
        /// <summary>成熟枝条视觉层，尚未生长时禁用。</summary>
        public GameObject GrownLayer => grownLayer;

        /// <summary>注入离线制作的网格层、顺序参数与少量粒子。</summary>
        /// <remarks>数组按 Renderer 顺序配对；每个延迟仅描述动画，不包含配方前置条件。</remarks>
        public void Configure(GameObject normal, GameObject mature, GameObject aquatic,
            Renderer[] normals, Renderer[] branches, float[] branchDelays,
            Renderer[] ribbons, float[] ribbonDelays, ParticleSystem motes, ParticleSystem drops)
        {
            baseLayer = normal;
            grownLayer = mature;
            dreamLayer = aquatic;
            baseParts = normals;
            grownParts = branches;
            grownDelays = branchDelays;
            dreamParts = ribbons;
            dreamDelays = ribbonDelays;
            growthMotes = motes;
            waterDrops = drops;
        }

        /// <summary>为每株植物持有一个可重复使用的属性块，运行中不实例化材质。</summary>
        private void Awake() { block = new MaterialPropertyBlock(); }

        /// <summary>设置表现目标；直接恢复时停止粒子与旧过渡并立即重建最终叶层。</summary>
        /// <param name="growthLevel">零为初始株形，约三成为当日新枝，一为成熟枝条。</param>
        /// <param name="aquatic">是否使用长叶、细脉与水滴的梦境层。</param>
        /// <param name="animate">是否从当前视觉渐进变化；不会延迟任何领域事实。</param>
        public void SetTarget(float growthLevel, bool aquatic, bool animate)
        {
            targetGrowth = Mathf.Clamp01(growthLevel);
            targetDream = aquatic ? 1 : 0;
            bool expanding = targetGrowth > growth + .02f;
            bool enteringDream = targetDream > dream + .02f;
            if (!animate)
            {
                // 重开和调试预设必须清除旧粒子，不能把上一轮的发现光点带进新场景。
                growth = targetGrowth;
                dream = targetDream;
                growthMotes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                waterDrops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (aquatic) waterDrops.Play();
            }
            else
            {
                if (expanding) growthMotes.Play();
                if (enteringDream) waterDrops.Play();
            }
            ApplySurface();
        }

        /// <summary>只推进已有目标与微观动画；风动由顶点 Shader 完成。</summary>
        private void Update()
        {
            growth = Mathf.MoveTowards(growth, targetGrowth, Time.deltaTime / growthDuration);
            dream = Mathf.MoveTowards(dream, targetDream, Time.deltaTime / dreamDuration);
            if (Mathf.Abs(growth - targetGrowth) < .001f && growthMotes.isEmitting)
                growthMotes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            ApplySurface();
        }

        /// <summary>按枝条顺序显现叶片，同时将普通株形溶解到不同轮廓的梦境层。</summary>
        private void ApplySurface()
        {
            if (block == null) block = new MaterialPropertyBlock();
            baseLayer.SetActive(dream < .999f);
            grownLayer.SetActive(growth > 0 && dream < .999f);
            dreamLayer.SetActive(targetDream > 0 || dream > 0);
            ApplyParts(baseParts, null, 1, 1 - dream, .016f);
            ApplyParts(grownParts, grownDelays, growth, 1 - dream, .035f);
            ApplyParts(dreamParts, dreamDelays, dream, 1, .075f);
        }

        /// <summary>延迟和局部展开宽度形成先茎后叶的节奏；属性块保留其他视觉组件的属性。</summary>
        private void ApplyParts(Renderer[] parts, float[] delays, float progress, float fade, float wind)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                float delay = delays == null ? 0 : delays[i];
                float local = Mathf.Clamp01((progress - delay) / Mathf.Max(.08f, 1 - delay));
                parts[i].GetPropertyBlock(block);
                block.SetFloat(RevealId, local);
                block.SetFloat(UnfurlId, Mathf.SmoothStep(0, 1, local));
                block.SetFloat(FadeId, fade);
                block.SetFloat(WindId, wind);
                parts[i].SetPropertyBlock(block);
            }
        }
    }
}
