// =============================================================================
// 文件：WaterPresenter.cs
// 职责：先落下第一滴水，再播放落滴声并扩展局部湿痕。
// 边界：滴水轨迹与湿痕面积仅是演出参数；不实现流体，不决定植物反应。
// =============================================================================
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>独立水节拍，支持过渡、即时恢复和只看视觉的调试预览。</summary>
    public sealed class WaterPresenter : WorldPresenter
    {
        [SerializeField] private GameObject puddle;
        [SerializeField] private Transform[] drops;
        [SerializeField] private AudioSource waterAudio;
        [SerializeField] private AudioSource firstDropAudio;
        private Vector3[] origins;
        private Renderer[] wetSurfaces;
        private Color[] wetColors;
        private MaterialPropertyBlock block;
        private bool water, landed;
        private float age, spread;
        /// <summary>湿痕视觉根；激活状态不是世界事实。</summary>
        public GameObject Puddle => puddle;
        /// <summary>已展现的湿痕扩散量，供演出调试使用。</summary>
        public float RevealProgress => spread;
        /// <summary>注入原始水迹、滴水与循环水声。</summary>
        public void Configure(GameObject stain, Transform[] droplets, AudioSource audio)
        { puddle = stain; drops = droplets; waterAudio = audio; }
        /// <summary>注入第一滴水的一次性音源，和持续水声分开。</summary>
        public void ConfigureDropAudio(AudioSource audio) { firstDropAudio = audio; }
        /// <summary>只在初始化缓存位置与颜色，不每帧查找对象或克隆材质。</summary>
        private void Awake()
        {
            origins = new Vector3[drops.Length];
            for (int i = 0; i < drops.Length; i++) origins[i] = drops[i].localPosition;
            wetSurfaces = puddle.GetComponentsInChildren<Renderer>(true); wetColors = new Color[wetSurfaces.Length];
            for (int i = 0; i < wetSurfaces.Length; i++) wetColors[i] = wetSurfaces[i].sharedMaterial.color;
            block = new MaterialPropertyBlock();
        }
        /// <summary>正常事实由导演转发；独立灰盒也能直接播放。</summary>
        protected override void OnFact(FactId fact) { if (fact == DemoRules.WaterPresent) PlayWaterReveal(); }
        /// <summary>开始第一滴，湿痕要等它视觉落地后才出现。</summary>
        public void PlayWaterReveal()
        {
            water = true; landed = false; age = spread = 0;
            waterAudio.Stop(); firstDropAudio.Stop(); puddle.SetActive(false);
            SetDrops(); SetWetSurface();
        }
        /// <summary>直接恢复终态或初始状态，不重复播放第一滴声音。</summary>
        public void SetFinalVisual(bool present)
        {
            water = present; landed = present; age = present ? 5 : 0; spread = present ? 1 : 0;
            firstDropAudio.Stop(); puddle.SetActive(present);
            if (present && !waterAudio.isPlaying) waterAudio.Play(); if (!present) waterAudio.Stop();
            SetDrops(); SetWetSurface();
        }
        /// <summary>从事实恢复；即时恢复取消先前滴水与扩散过渡。</summary>
        public override void ApplyState(GameState state, bool animate)
        {
            if (!animate) SetFinalVisual(state.HasFact(DemoRules.WaterPresent));
            else if (state.HasFact(DemoRules.WaterPresent)) PlayWaterReveal();
        }
        /// <summary>仅更新滴水轨迹与湿痕尺寸，不向 Session 写入任何内容。</summary>
        private void Update()
        {
            if (!water) return;
            age += Time.deltaTime;
            if (!landed && age >= .9f)
            {
                // 落滴声与湿痕同步，是玩家看懂水从画里出来的独立节拍。
                landed = true; puddle.SetActive(true); firstDropAudio.Play(); waterAudio.Play();
            }
            if (landed) spread = Mathf.MoveTowards(spread, 1, Time.deltaTime / 4);
            SetDrops(); SetWetSurface();
        }
        /// <summary>第一滴领先，其余滴水稍后错开；全程使用确定性视觉轨迹。</summary>
        private void SetDrops()
        {
            for (int i = 0; i < drops.Length; i++)
            {
                float delay = i == 0 ? 0 : 1.1f + i * .18f;
                bool visible = water && age >= delay;
                drops[i].gameObject.SetActive(visible);
                float phase = Mathf.Repeat(Mathf.Max(0, age - delay), 1.5f) / .9f;
                drops[i].localPosition = origins[i] + Vector3.down * (1.2f * Mathf.Clamp01(phase * phase));
            }
        }
        /// <summary>湿痕由深色、高光与小范围扩散表现，不把整个房间染蓝。</summary>
        private void SetWetSurface()
        {
            puddle.transform.localScale = water ? new Vector3(Mathf.Max(.001f, spread), 1, Mathf.Max(.001f, spread)) : new Vector3(.001f, 1, .001f);
            for (int i = 0; i < wetSurfaces.Length; i++)
            {
                block.Clear(); var color = wetColors[i]; color.a *= spread;
                block.SetColor(ShaderIds.BaseColor, color); block.SetFloat(ShaderIds.Smoothness, .94f);
                wetSurfaces[i].SetPropertyBlock(block);
            }
        }
    }
}
