using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    public sealed class WindowPresenter : WorldPresenter
    {
        [SerializeField] private GameObject nearBackground;
        [SerializeField] private GameObject farBackground;
        [SerializeField] private GameObject oceanBackground;
        private float reveal;
        private bool ocean;
        public GameObject FarBackground => farBackground;
        public GameObject OceanBackground => oceanBackground;
        public void Configure(GameObject near, GameObject far, GameObject sea) { nearBackground = near; farBackground = far; oceanBackground = sea; }
        protected override void OnFact(FactId fact) { if (fact == DemoRules.WindowFar || fact == DemoRules.WindowOcean) ApplyState(Session.State, true); }
        public override void ApplyState(GameState state, bool animate)
        {
            bool far = state.HasFact(DemoRules.WindowFar); ocean = state.HasFact(DemoRules.WindowOcean);
            nearBackground.SetActive(!far && !ocean); farBackground.SetActive(far && !ocean); oceanBackground.SetActive(ocean);
            if (!animate) reveal = ocean ? 1 : 0;
            oceanBackground.transform.localScale = new Vector3(1, Mathf.Max(.001f, reveal), 1);
        }
        private void Update()
        {
            if (!ocean || reveal >= 1) return;
            reveal = Mathf.MoveTowards(reveal, 1, Time.deltaTime / 2.5f);
            oceanBackground.transform.localScale = new Vector3(1, Mathf.Max(.001f, reveal), 1);
        }
    }
}
