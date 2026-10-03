using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    public sealed class BookPresenter : WorldPresenter
    {
        [SerializeField] private Vector3 fallenPosition;
        [SerializeField] private AudioSource fallAudio;
        private Vector3 shelfPosition;
        private Quaternion shelfRotation;
        private float progress;
        private bool falling, hit;
        public Vector3 FallenPosition => fallenPosition;
        public void Configure(Vector3 position, AudioSource audio) { fallenPosition = position; fallAudio = audio; }
        private void Awake() { shelfPosition = transform.localPosition; shelfRotation = transform.localRotation; }
        protected override void OnFact(FactId fact) { if (fact == DemoRules.BookFallen) ApplyState(Session.State, true); }
        public override void ApplyState(GameState state, bool animate)
        {
            falling = state.HasFact(DemoRules.BookFallen);
            if (!animate) { progress = falling ? 1 : 0; hit = falling; fallAudio.Stop(); SetPose(); }
        }
        private void SetPose()
        {
            transform.localPosition = Vector3.Lerp(shelfPosition, fallenPosition, progress * progress);
            transform.localRotation = Quaternion.Slerp(shelfRotation, Quaternion.Euler(0, 18, 0), progress);
        }
        private void Update()
        {
            if (!falling || progress >= 1) return;
            progress = Mathf.Min(1, progress + Time.deltaTime / 1.25f); SetPose();
            if (progress >= 1 && !hit) { hit = true; fallAudio.Play(); }
        }
    }
}
