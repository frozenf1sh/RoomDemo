using Game.Application;
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    [DefaultExecutionOrder(100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private WorldPresenter[] presenters;
        [SerializeField] private NotebookView notebook;
        [SerializeField] private DebugView debugView;
        [SerializeField] private InteractionSystem interaction;
        public GameSession Session { get; private set; }
        public void Configure(WorldPresenter[] views, NotebookView notebookView, DebugView debug, InteractionSystem input)
        { presenters = views; notebook = notebookView; debugView = debug; interaction = input; }
        private void Awake()
        {
            Restart(); UnityEngine.Application.targetFrameRate = 60; UnityEngine.Application.runInBackground = true;
        }
        public void Restart() => Bind(new GameSession(DemoRules.Create()));
        public void JumpTo(DebugPreset preset) => Bind(GameSession.FromPreset(DemoRules.Create(), preset));
        public void AddDebugFact(FactId fact) { Session.DebugAddFact(fact); foreach (var view in presenters) view.ApplyState(Session.State, false); }
        private void Bind(GameSession session)
        {
            Session = session;
            foreach (var view in presenters) view.Bind(Session);
            notebook.Bind(Session); debugView.Bind(Session, Restart, JumpTo, AddDebugFact); interaction.Bind(Session, notebook);
        }
    }
}
