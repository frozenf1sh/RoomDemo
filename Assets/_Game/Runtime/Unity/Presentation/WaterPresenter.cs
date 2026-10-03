// =============================================================================
// 文件：WaterPresenter.cs
// 职责：表现局部水渍扩散、画框滴水与水声。
// 边界：水滴循环是视觉效果，不模拟流体，也不判断水生化规则。
// =============================================================================
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>表现局部水渍扩散、画框滴水与水声。</summary>
    public sealed class WaterPresenter : WorldPresenter
    {
        [SerializeField] private GameObject puddle;
        [SerializeField] private Transform[] drops;
        [SerializeField] private AudioSource waterAudio;
        private Vector3[] origins;
        private bool water;
        private float spread;
        /// <summary>地面局部水渍视觉根。</summary>
        public GameObject Puddle => puddle;
        /// <summary>注入水渍、水滴与循环水声。</summary>
        public void Configure(GameObject stain, Transform[] droplets, AudioSource audio) { puddle = stain; drops = droplets; waterAudio = audio; }
        /// <summary>缓存滴水起点，恢复时重置视觉位置。</summary>
        private void Awake() { origins = new Vector3[drops.Length]; for (int i = 0; i < drops.Length; i++) origins[i] = drops[i].localPosition; }
        /// <summary>仅接收水已存在事实。</summary>
        protected override void OnFact(FactId fact) { if (fact == DemoRules.WaterPresent) ApplyState(Session.State, true); }
        /// <summary>显示或隐藏局部水迹；恢复模式直接设置扩散终点。</summary>
        public override void ApplyState(GameState state, bool animate)
        {
            // 这里负责水迹可见性，水生植物与海窗反应不在此计算。
            water = state.HasFact(DemoRules.WaterPresent); puddle.SetActive(water); foreach (var drop in drops) drop.gameObject.SetActive(water);
            if (water && !waterAudio.isPlaying) waterAudio.Play(); if (!water) waterAudio.Stop();
            if (!animate) { spread = water ? 1 : 0; for (int i = 0; i < drops.Length; i++) drops[i].localPosition = origins[i]; }
            puddle.transform.localScale = new Vector3(Mathf.Max(.001f, spread), 1, Mathf.Max(.001f, spread));
        }
        /// <summary>推进水渍扩散与滴水循环，不模拟流体或推导反应。</summary>
        private void Update()
        {
            if (!water) return;
            // 扩散进度与滴水周期仅用于视觉，不代表可读取的水量事实。
            spread = Mathf.MoveTowards(spread, 1, Time.deltaTime / 3);
            puddle.transform.localScale = new Vector3(spread, 1, spread);
            for (int i = 0; i < drops.Length; i++) drops[i].localPosition = origins[i] + Vector3.down * Mathf.Repeat(Time.time * .65f + i * .2f, 1.5f);
        }
    }
}
