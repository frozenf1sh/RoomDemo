// =============================================================================
// 文件：PaintingPresenter.cs
// 职责：让静止画布连续经历颜料异常、海面运动与画框湿润，淡入空间海声。
// 边界：只推进视觉参数；水、植物和窗外的演出由独立导演安排。
// =============================================================================
using System.Linq;
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>海画的连续表现；即时恢复与动画调用共用同一参数设置路径。</summary>
    public sealed class PaintingPresenter : WorldPresenter
    {
        [SerializeField] private GameObject ordinaryPainting;
        [SerializeField] private GameObject seaPainting;
        [SerializeField] private AudioSource seaAudio;
        private Renderer surface;
        private Renderer[] frames;
        private Color[] frameColors;
        private MaterialPropertyBlock block;
        private float progress, target, originalVolume;
        /// <summary>承载静止画布与动态海的共同表面，不用激活状态判断业务。</summary>
        public GameObject SeaPainting => seaPainting;
        /// <summary>画中海的空间循环音源。</summary>
        public AudioSource SeaAudio => seaAudio;
        /// <summary>仅代表已经展现到哪一步，不能用于规则判定。</summary>
        public float TransitionProgress => progress;
        /// <summary>注入独立视觉层与音源，保持旧灰盒配置兼容。</summary>
        public void Configure(GameObject ordinary, GameObject sea, AudioSource audio)
        { ordinaryPainting = ordinary; seaPainting = sea; seaAudio = audio; }
        /// <summary>只在初始化查找自身视觉引用和缓存属性块。</summary>
        private void Awake()
        {
            surface = seaPainting.GetComponentInChildren<Renderer>(true);
            frames = ordinaryPainting.transform.parent.GetComponentsInChildren<Renderer>(true).Where(x => x.name.StartsWith("Frame") || x.name.StartsWith("Gilt")).ToArray();
            frameColors = frames.Select(x => x.sharedMaterial.color).ToArray();
            block = new MaterialPropertyBlock(); originalVolume = seaAudio.volume;
        }
        /// <summary>非导演场景可直接播放完整连续过渡。</summary>
        protected override void OnFact(FactId fact) { if (fact == DemoRules.PaintingSea) PlaySeaTransition(); }
        /// <summary>先出现很小的颜料扰动与微弱海声，吸引注意力。</summary>
        public void PlayAnomaly()
        {
            target = .16f;
            if (!seaAudio.isPlaying) seaAudio.Play();
        }
        /// <summary>由导演启动海面完整展现，沿现有进度连续前进。</summary>
        public void PlaySeaTransition()
        {
            target = 1;
            if (!seaAudio.isPlaying) seaAudio.Play();
        }
        /// <summary>直接设置预览终态；不产生或修改任何领域事实。</summary>
        public void SetFinalVisual(bool sea)
        {
            progress = target = sea ? 1 : 0;
            ordinaryPainting.SetActive(false); seaPainting.SetActive(true);
            if (sea && !seaAudio.isPlaying) seaAudio.Play();
            if (!sea) seaAudio.Stop();
            SetParameters();
        }
        /// <summary>恢复时立即设置最终参数，取消尚未完成的过渡目标。</summary>
        public override void ApplyState(GameState state, bool animate)
        {
            if (!animate) SetFinalVisual(state.HasFact(DemoRules.PaintingSea));
            else if (state.HasFact(DemoRules.PaintingSea)) PlaySeaTransition();
        }
        /// <summary>只推进 Shader 和音量目标；没有延时添加 Fact。</summary>
        private void Update()
        {
            progress = Mathf.MoveTowards(progress, target, Time.deltaTime / 2.8f);
            SetParameters();
        }
        /// <summary>复用属性块修改实例参数，不每帧复制共享材质。</summary>
        private void SetParameters()
        {
            block.Clear();
            block.SetFloat(ShaderIds.Transition, progress);
            block.SetFloat(ShaderIds.Wetness, progress);
            block.SetFloat(ShaderIds.Emission, progress * .18f);
            surface.SetPropertyBlock(block);
            seaAudio.volume = originalVolume * Mathf.Lerp(.08f, 1, progress);
            for (int i = 0; i < frames.Length; i++)
            {
                // 画框逐渐变暗发亮，是画中海侵入现实前的微小预兆。
                block.Clear(); block.SetColor(ShaderIds.BaseColor, frameColors[i] * (1 - progress * .16f));
                block.SetFloat(ShaderIds.Smoothness, .25f + progress * .6f);
                frames[i].SetPropertyBlock(block);
            }
        }
    }
}
