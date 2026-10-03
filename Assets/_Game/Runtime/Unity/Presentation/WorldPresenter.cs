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
            Session = session; Session.EventRaised += OnEvent; ApplyState(Session.State, false);
        }
        private void OnEvent(DomainEvent e) { if (e is FactAdded fact) OnFact(fact.Fact); }
        protected abstract void OnFact(FactId fact);
        public abstract void ApplyState(GameState state, bool animate);
        protected virtual void OnDestroy() { if (Session != null) Session.EventRaised -= OnEvent; }
    }
}
