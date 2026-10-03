using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    public sealed class WindPresenter : WorldPresenter
    {
        [SerializeField] private Transform plantVisual;
        [SerializeField] private Transform[] papers;
        [SerializeField] private AudioSource windAudio;
        private Vector3[] paperOrigins;
        private Quaternion plantRotation;
        private Quaternion[] paperRotations;
        private bool activeWind;
        public bool IsWindVisible => activeWind;
        public AudioSource WindAudio => windAudio;
        public void Configure(Transform plant, Transform[] sheets, AudioSource audio) { plantVisual = plant; papers = sheets; windAudio = audio; }
        private void Awake()
        {
            plantRotation = plantVisual.localRotation;
            paperOrigins = new Vector3[papers.Length]; paperRotations = new Quaternion[papers.Length];
            for (int i = 0; i < papers.Length; i++) { paperOrigins[i] = papers[i].localPosition; paperRotations[i] = papers[i].localRotation; }
        }
        protected override void OnFact(FactId fact) { if (fact == DemoRules.WindEntering) ApplyState(Session.State, true); }
        public override void ApplyState(GameState state, bool animate)
        {
            activeWind = state.HasFact(DemoRules.WindEntering);
            if (activeWind && !windAudio.isPlaying) windAudio.Play();
            if (!activeWind) windAudio.Stop();
            if (!animate)
            {
                plantVisual.localRotation = plantRotation;
                for (int i = 0; i < papers.Length; i++) { papers[i].localPosition = paperOrigins[i]; papers[i].localRotation = paperRotations[i]; }
            }
        }
        private void Update()
        {
            if (!activeWind) return;
            float t = Time.time;
            plantVisual.localRotation = plantRotation * Quaternion.Euler(0, 0, Mathf.Sin(t * 2.5f) * 5);
            for (int i = 0; i < papers.Length; i++)
            {
                float phase = t * 2.2f + i * 1.7f;
                papers[i].localPosition = paperOrigins[i] + new Vector3(Mathf.Sin(phase) * .07f, .05f + Mathf.Sin(phase + .4f) * .04f, 0);
                papers[i].localRotation = paperRotations[i] * Quaternion.Euler(Mathf.Sin(phase) * 8f, 0, Mathf.Cos(phase) * 5f);
            }
        }
    }
}
