using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    public sealed class PaintingPresenter : WorldPresenter
    {
        [SerializeField] private GameObject ordinaryPainting;
        [SerializeField] private GameObject seaPainting;
        [SerializeField] private AudioSource seaAudio;
        public GameObject SeaPainting => seaPainting;
        public AudioSource SeaAudio => seaAudio;
        public void Configure(GameObject ordinary, GameObject sea, AudioSource audio) { ordinaryPainting = ordinary; seaPainting = sea; seaAudio = audio; }
        protected override void OnFact(FactId fact) { if (fact == DemoRules.PaintingSea) ApplyState(Session.State, true); }
        public override void ApplyState(GameState state, bool animate)
        {
            bool sea = state.HasFact(DemoRules.PaintingSea); ordinaryPainting.SetActive(!sea); seaPainting.SetActive(sea);
            if (sea && !seaAudio.isPlaying) seaAudio.Play(); if (!sea) seaAudio.Stop();
        }
    }
}
