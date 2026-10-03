// =============================================================================
// 文件：PaintingPresenter.cs
// 职责：切换静态画与动态海画，并同步海浪声。
// 边界：海水进入房间由独立 Reaction 产生，本组件不控制水或盆栽。
// =============================================================================
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>切换静态画与动态海画，并同步海浪声。</summary>
    public sealed class PaintingPresenter : WorldPresenter
    {
        [SerializeField] private GameObject ordinaryPainting;
        [SerializeField] private GameObject seaPainting;
        [SerializeField] private AudioSource seaAudio;
        /// <summary>动态海画视觉层。</summary>
        public GameObject SeaPainting => seaPainting;
        /// <summary>画中海对应的循环音源。</summary>
        public AudioSource SeaAudio => seaAudio;
        /// <summary>注入普通画、海画与海浪音源。</summary>
        public void Configure(GameObject ordinary, GameObject sea, AudioSource audio) { ordinaryPainting = ordinary; seaPainting = sea; seaAudio = audio; }
        /// <summary>仅接收画中海的事实。</summary>
        protected override void OnFact(FactId fact) { if (fact == DemoRules.PaintingSea) ApplyState(Session.State, true); }
        /// <summary>切换视觉层并同步循环海浪声；该变化不需要延迟动画。</summary>
        public override void ApplyState(GameState state, bool animate)
        {
            // 绘画层切换由 painting.sea 决定，海浪动画由材质自身完成。
            bool sea = state.HasFact(DemoRules.PaintingSea); ordinaryPainting.SetActive(!sea); seaPainting.SetActive(sea);
            if (sea && !seaAudio.isPlaying) seaAudio.Play(); if (!sea) seaAudio.Stop();
        }
    }
}
