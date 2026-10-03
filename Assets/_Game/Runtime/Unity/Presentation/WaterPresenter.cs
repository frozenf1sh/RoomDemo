using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    public sealed class WaterPresenter : WorldPresenter
    {
        [SerializeField] private GameObject puddle;
        [SerializeField] private Transform[] drops;
        [SerializeField] private AudioSource waterAudio;
        private Vector3[] origins;
        private bool water;
        private float spread;
        public GameObject Puddle => puddle;
        public void Configure(GameObject stain, Transform[] droplets, AudioSource audio) { puddle = stain; drops = droplets; waterAudio = audio; }
        private void Awake() { origins = new Vector3[drops.Length]; for (int i = 0; i < drops.Length; i++) origins[i] = drops[i].localPosition; }
        protected override void OnFact(FactId fact) { if (fact == DemoRules.WaterPresent) ApplyState(Session.State, true); }
        public override void ApplyState(GameState state, bool animate)
        {
            water = state.HasFact(DemoRules.WaterPresent); puddle.SetActive(water); foreach (var drop in drops) drop.gameObject.SetActive(water);
            if (water && !waterAudio.isPlaying) waterAudio.Play(); if (!water) waterAudio.Stop();
            if (!animate) { spread = water ? 1 : 0; for (int i = 0; i < drops.Length; i++) drops[i].localPosition = origins[i]; }
            puddle.transform.localScale = new Vector3(Mathf.Max(.001f, spread), 1, Mathf.Max(.001f, spread));
        }
        private void Update()
        {
            if (!water) return;
            spread = Mathf.MoveTowards(spread, 1, Time.deltaTime / 3);
            puddle.transform.localScale = new Vector3(spread, 1, spread);
            for (int i = 0; i < drops.Length; i++) drops[i].localPosition = origins[i] + Vector3.down * Mathf.Repeat(Time.time * .65f + i * .2f, 1.5f);
        }
    }
}
