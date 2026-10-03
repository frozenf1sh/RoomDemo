using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    public sealed class PaperPresenter : WorldPresenter
    {
        [SerializeField] private Transform sheet;
        [SerializeField] private Vector3 revealedPosition;
        [SerializeField] private Vector3 revealedRotation;
        private Vector3 hiddenPosition;
        private Quaternion hiddenRotation;
        private Vector3 target;
        private Quaternion targetRotation;
        public Transform Sheet => sheet;
        public void Configure(Transform paper, Vector3 position, Vector3 rotation) { sheet = paper; revealedPosition = position; revealedRotation = rotation; }
        private void Awake() { hiddenPosition = sheet.localPosition; hiddenRotation = sheet.localRotation; }
        protected override void OnFact(FactId fact) { if (fact == DemoRules.PaperRevealed) ApplyState(Session.State, true); }
        public override void ApplyState(GameState state, bool animate)
        {
            bool revealed = state.HasFact(DemoRules.PaperRevealed); target = revealed ? revealedPosition : hiddenPosition; targetRotation = revealed ? Quaternion.Euler(revealedRotation) : hiddenRotation;
            if (!animate) { sheet.localPosition = target; sheet.localRotation = targetRotation; }
        }
        private void Update() { sheet.localPosition = Vector3.MoveTowards(sheet.localPosition, target, Time.deltaTime * .7f); sheet.localRotation = Quaternion.RotateTowards(sheet.localRotation, targetRotation, Time.deltaTime * 65); }
    }
}
