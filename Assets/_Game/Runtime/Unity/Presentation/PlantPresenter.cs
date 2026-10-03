using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    public sealed class PlantPresenter : WorldPresenter
    {
        [SerializeField] private Transform visual;
        [SerializeField] private GameObject aquaticVisual;
        [SerializeField] private AudioSource growAudio;
        private Vector3 initialScale, initialPosition, targetScale, targetPosition;
        private Renderer[] leaves;
        private Material[] materials;
        private Color[] normalColors;
        private float[] normalGloss;
        private bool aquatic;
        public Transform Visual => visual;
        public GameObject AquaticVisual => aquaticVisual;
        public void Configure(Transform value, GameObject kelp, AudioSource audio) { visual = value; aquaticVisual = kelp; growAudio = audio; }
        private void Awake()
        {
            initialScale = visual.localScale; initialPosition = visual.localPosition;
            leaves = visual.GetComponentsInChildren<Renderer>(true); materials = new Material[leaves.Length]; normalColors = new Color[leaves.Length]; normalGloss = new float[leaves.Length];
            for (int i = 0; i < leaves.Length; i++) { materials[i] = leaves[i].material; normalColors[i] = materials[i].color; normalGloss[i] = materials[i].GetFloat("_Smoothness"); }
        }
        protected override void OnFact(FactId fact)
        {
            if (fact != DemoRules.PlantGrown && fact != DemoRules.PlantAquatic) return;
            ApplyState(Session.State, true); if (fact == DemoRules.PlantGrown) growAudio.Play();
        }
        public override void ApplyState(GameState state, bool animate)
        {
            aquatic = state.HasFact(DemoRules.PlantAquatic);
            bool grown = state.HasFact(DemoRules.PlantGrown);
            targetScale = Vector3.Scale(initialScale, aquatic ? new Vector3(1.50f, 2.35f, 1.4f) : grown ? Vector3.one * 1.85f : Vector3.one);
            targetPosition = initialPosition + (grown ? new Vector3(-.27f, 0, 0) : Vector3.zero);
            aquaticVisual.SetActive(aquatic);
            if (!animate)
            {
                visual.localScale = targetScale; visual.localPosition = targetPosition; growAudio.Stop();
                for (int i = 0; i < materials.Length; i++) { materials[i].color = aquatic ? new Color(.07f, .63f, .62f) : normalColors[i]; materials[i].SetFloat("_Smoothness", aquatic ? .88f : normalGloss[i]); }
            }
        }
        private void Update()
        {
            visual.localScale = Vector3.MoveTowards(visual.localScale, targetScale, Time.deltaTime * 1.5f);
            visual.localPosition = Vector3.MoveTowards(visual.localPosition, targetPosition, Time.deltaTime * .35f);
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].color = Color.Lerp(materials[i].color, aquatic ? new Color(.07f, .63f, .62f) : normalColors[i], Time.deltaTime * 1.5f);
                materials[i].SetFloat("_Smoothness", Mathf.Lerp(materials[i].GetFloat("_Smoothness"), aquatic ? .88f : normalGloss[i], Time.deltaTime * 1.5f));
            }
        }
        protected override void OnDestroy() { base.OnDestroy(); if (materials != null) foreach (var material in materials) Destroy(material); }
    }
}
