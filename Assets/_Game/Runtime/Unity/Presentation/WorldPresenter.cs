using Game.Application;
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    public abstract class WorldPresenter : MonoBehaviour
    {
        protected GameSession Session { get; private set; }
        public void Bind(GameSession session)
        {
            if (Session != null) Session.EventRaised -= OnEvent;
            Session = session; Session.EventRaised += OnEvent; Refresh();
        }
        private void OnEvent(DomainEvent e) { if (e is FactAdded) Refresh(); }
        protected abstract void Refresh();
        protected virtual void OnDestroy() { if (Session != null) Session.EventRaised -= OnEvent; }
    }
}
