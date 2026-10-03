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
            Session = new GameSession(DemoRules.Create());
            foreach (var presenter in presenters) presenter.Bind(Session);
            notebook.Bind(Session); debugView.Bind(Session); interaction.Bind(Session, notebook);
            UnityEngine.Application.targetFrameRate = 60;
            UnityEngine.Application.runInBackground = true;
        }
    }
}
