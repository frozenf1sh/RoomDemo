// =============================================================================
// 文件：GameBootstrap.cs
// 职责：创建会话，显式绑定输入、UI 与所有表现组件。
// 边界：重开与跳转替换整个 Session；避免单例、服务定位和跨 Prefab 控制。
// =============================================================================
using Game.Application;
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    // 等待各 Presenter 的 Awake 先缓存初始表现，之后才绑定和恢复。
    /// <summary>创建会话，显式绑定输入、UI 与所有表现组件。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private WorldPresenter[] presenters;
        [SerializeField] private NotebookView notebook;
        [SerializeField] private DebugView debugView;
        [SerializeField] private InteractionSystem interaction;
        /// <summary>当前会话引用；重开与跳转会整体替换。</summary>
        public GameSession Session { get; private set; }
        /// <summary>由场景生成器显式注入组件引用。</summary>
        public void Configure(WorldPresenter[] views, NotebookView notebookView, DebugView debug, InteractionSystem input)
        { presenters = views; notebook = notebookView; debugView = debug; interaction = input; }
        /// <summary>在表现组件缓存初始姿态后创建会话，并设置帧率。</summary>
        private void Awake()
        {
            Restart(); UnityEngine.Application.targetFrameRate = 60; UnityEngine.Application.runInBackground = true;
        }
        /// <summary>创建全新会话并同步恢复所有表现。</summary>
        public void Restart() => Bind(new GameSession(DemoRules.Create()));
        /// <summary>先恢复新会话，再用它替换当前会话。</summary>
        public void JumpTo(DebugPreset preset) => Bind(GameSession.FromPreset(DemoRules.Create(), preset));
        /// <summary>通过应用层添加事实，然后立即恢复最终表现。</summary>
        public void AddDebugFact(FactId fact) { Session.DebugAddFact(fact); foreach (var view in presenters) view.ApplyState(Session.State, false); }
        /// <summary>统一重绑 Presenter、笔记本、调试面板与输入。</summary>
        private void Bind(GameSession session)
        {
            Session = session;
            // 所有表现消费同一 Session；不让 Prefab 直接调用其他 Prefab。
            foreach (var view in presenters) view.Bind(Session);
            notebook.Bind(Session); debugView.Bind(Session, Restart, JumpTo, AddDebugFact); interaction.Bind(Session, notebook);
        }
    }
}
