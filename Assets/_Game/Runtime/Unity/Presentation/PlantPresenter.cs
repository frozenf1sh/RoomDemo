using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    public sealed class PlantPresenter : WorldPresenter
    {
        [SerializeField] private Transform visual;
        private Vector3 initialScale;
        private Vector3 targetScale;
        public Transform Visual => visual;
        public void Configure(Transform value) { visual = value; }
        private void Awake() { initialScale = visual.localScale; targetScale = initialScale; }
        protected override void Refresh() { targetScale = initialScale * (Session.State.HasFact(DemoRules.PlantGrown) ? 1.85f : 1f); }
        private void Update() { visual.localScale = Vector3.MoveTowards(visual.localScale, targetScale, Time.deltaTime * 1.7f); }
    }
}
