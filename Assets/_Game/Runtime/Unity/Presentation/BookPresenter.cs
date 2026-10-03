using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    public sealed class BookPresenter : WorldPresenter
    {
        [SerializeField] private Vector3 fallenPosition;
        private Vector3 shelfPosition;
        private Quaternion shelfRotation;
        private float progress;
        private bool falling;
        public Vector3 FallenPosition => fallenPosition;
        public void Configure(Vector3 position) { fallenPosition = position; }
        private void Awake() { shelfPosition = transform.localPosition; shelfRotation = transform.localRotation; }
        protected override void Refresh() { falling = Session.State.HasFact(DemoRules.BookFallen); }
        private void Update()
        {
            if (!falling) return;
            progress = Mathf.Min(1f, progress + Time.deltaTime / 1.05f);
            transform.localPosition = Vector3.Lerp(shelfPosition, fallenPosition, progress * progress);
            transform.localRotation = Quaternion.Slerp(shelfRotation, Quaternion.Euler(0, 18, 0), progress);
        }
    }
}
