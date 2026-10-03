// =============================================================================
// 文件：EndingPresenter.cs
// 职责：表现临时切片完成提示、环境光与氛围声音。
// 边界：完成条件由领域层产生；本文件只消费完成事实。
// =============================================================================
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>表现临时切片完成提示、环境光与氛围声音。</summary>
    public sealed class EndingPresenter : WorldPresenter
    {
        [SerializeField] private GameObject endingPanel;
        [SerializeField] private Light roomLight;
        [SerializeField] private AudioSource ambience;
        private Color initialColor, targetColor;
        private float initialIntensity, targetIntensity;
        /// <summary>临时结束提示面板。</summary>
        public GameObject EndingPanel => endingPanel;
        /// <summary>注入结束面板、环境灯光和氛围音源。</summary>
        public void Configure(GameObject panel, Light light, AudioSource audio) { endingPanel = panel; roomLight = light; ambience = audio; }
        /// <summary>缓存初始光照，重开时可恢复。</summary>
        private void Awake() { initialColor = roomLight.color; initialIntensity = roomLight.intensity; }
        /// <summary>仅接收切片完成事实。</summary>
        protected override void OnFact(FactId fact) { if (fact == DemoRules.Complete) ApplyState(Session.State, true); }
        /// <summary>同步结束提示、光照目标与循环氛围声。</summary>
        public override void ApplyState(GameState state, bool animate)
        {
            // 结束条件已由领域闭包产生，本组件不再检查三个前置事实。
            bool complete = state.HasFact(DemoRules.Complete); endingPanel.SetActive(complete);
            targetColor = complete ? new Color(.54f, .84f, .95f) : initialColor; targetIntensity = complete ? initialIntensity * .8f : initialIntensity;
            if (!animate) { roomLight.color = targetColor; roomLight.intensity = targetIntensity; }
            if (complete && !ambience.isPlaying) ambience.Play(); if (!complete) ambience.Stop();
        }
        /// <summary>平滑靠近灯光目标，不判断通关条件。</summary>
        private void Update() { roomLight.color = Color.Lerp(roomLight.color, targetColor, Time.deltaTime * .6f); roomLight.intensity = Mathf.Lerp(roomLight.intensity, targetIntensity, Time.deltaTime * .6f); }
    }
}
