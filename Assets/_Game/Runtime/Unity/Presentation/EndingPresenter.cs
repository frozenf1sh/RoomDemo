using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    public sealed class EndingPresenter : WorldPresenter
    {
        [SerializeField] private GameObject endingPanel;
        [SerializeField] private Light roomLight;
        [SerializeField] private AudioSource ambience;
        private Color initialColor, targetColor;
        private float initialIntensity, targetIntensity;
        public GameObject EndingPanel => endingPanel;
        public void Configure(GameObject panel, Light light, AudioSource audio) { endingPanel = panel; roomLight = light; ambience = audio; }
        private void Awake() { initialColor = roomLight.color; initialIntensity = roomLight.intensity; }
        protected override void OnFact(FactId fact) { if (fact == DemoRules.Complete) ApplyState(Session.State, true); }
        public override void ApplyState(GameState state, bool animate)
        {
            bool complete = state.HasFact(DemoRules.Complete); endingPanel.SetActive(complete);
            targetColor = complete ? new Color(.54f, .84f, .95f) : initialColor; targetIntensity = complete ? initialIntensity * .8f : initialIntensity;
            if (!animate) { roomLight.color = targetColor; roomLight.intensity = targetIntensity; }
            if (complete && !ambience.isPlaying) ambience.Play(); if (!complete) ambience.Stop();
        }
        private void Update() { roomLight.color = Color.Lerp(roomLight.color, targetColor, Time.deltaTime * .6f); roomLight.intensity = Mathf.Lerp(roomLight.intensity, targetIntensity, Time.deltaTime * .6f); }
    }
}
