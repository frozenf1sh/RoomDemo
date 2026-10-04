// =============================================================================
// 文件：GameBootstrap.cs
// 职责：创建会话，显式绑定输入、UI 与所有表现组件。
// 边界：重开与跳转替换整个 Session；避免单例、服务定位和跨 Prefab 控制。
// =============================================================================
using System;
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
        [SerializeField] private PaintingSeaSequence sequence;
        [SerializeField] private InspectionController inspection;
        [SerializeField] private bool explorationEnabled;
        private bool legacyRegression;
        /// <summary>技术回归入口使用旧内容表；玩家的重开仍默认使用探索内容。</summary>
        public void RestartLegacyForRegression() { legacyRegression = true; Restart(); }
        /// <summary>由场景工具启用新流程，不把玩法规则分散进 Inspector。</summary>
        public void ConfigureExploration(bool enabled) { explorationEnabled = enabled; }
        /// <summary>当前会话引用；重开与跳转会整体替换。</summary>
        public GameSession Session { get; private set; }
        /// <summary>由场景生成器显式注入组件引用。</summary>
        public void Configure(WorldPresenter[] views, NotebookView notebookView, DebugView debug, InteractionSystem input)
        { presenters = views; notebook = notebookView; debugView = debug; interaction = input; }
        /// <summary>注入本房间的演出导演，核心会话不依赖它。</summary>
        public void ConfigureSequence(PaintingSeaSequence director) { sequence = director; }
        /// <summary>显式绑定检视输入，重开时负责退出并释放临时相机。</summary>
        public void ConfigureInspection(InspectionController controller) { inspection = controller; }
        /// <summary>在表现组件缓存初始姿态后创建会话，并设置帧率。</summary>
        private void Awake()
        {
            Restart(); UnityEngine.Application.targetFrameRate = 60; UnityEngine.Application.runInBackground = true;
        }
        /// <summary>创建全新会话并同步恢复所有表现。</summary>
        public void Restart() => Bind(new GameSession(explorationEnabled && !legacyRegression ? ExplorationRules.Create() : DemoRules.Create()));
        /// <summary>先恢复新会话，再用它替换当前会话。</summary>
        public void JumpTo(DebugPreset preset)
        {
            bool exploration = explorationEnabled && !legacyRegression;
            Bind(GameSession.FromPreset(exploration ? ExplorationRules.Create() : DemoRules.Create(), exploration ? ExplorationPresets.AdaptLegacy(preset) : preset));
        }
        /// <summary>通过应用层添加事实，然后立即恢复最终表现。</summary>
        public void AddDebugFact(FactId fact)
        {
            Session.DebugAddFact(fact);
            // 调试添加事实明确要求即时恢复；已启动的演出必须先取消。
            if (sequence != null) sequence.ApplyFinalStateImmediately();
            else foreach (var view in presenters) view.ApplyState(Session.State, false);
        }
        /// <summary>统一重绑 Presenter、笔记本、调试面板与输入。</summary>
        private void Bind(GameSession session)
        {
            if (inspection != null) inspection.Exit(false);
            if (sequence != null) sequence.Cancel();
            Session = session;
            // 所有表现消费同一 Session；不让 Prefab 直接调用其他 Prefab。
            Func<FactId, bool> filter = sequence == null ? (Func<FactId, bool>)null : sequence.ShouldPresentImmediately;
            foreach (var view in presenters) view.Bind(Session, filter);
            if (sequence != null) sequence.Bind(Session, presenters);
            notebook.Bind(Session);
            if (inspection != null) inspection.Bind(Session, notebook);
            debugView.Bind(Session, Restart, JumpTo, AddDebugFact, sequence); interaction.Bind(Session, notebook);
        }
    }
}
