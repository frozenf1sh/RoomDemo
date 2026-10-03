using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    public sealed class WindowPresenter : WorldPresenter
    {
        [SerializeField] private GameObject nearBackground;
        [SerializeField] private GameObject farBackground;
        public GameObject FarBackground => farBackground;
        public void Configure(GameObject near, GameObject far) { nearBackground = near; farBackground = far; }
        protected override void Refresh()
        {
            bool far = Session.State.HasFact(DemoRules.WindowFar);
            nearBackground.SetActive(!far); farBackground.SetActive(far);
        }
    }
}
