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
        [SerializeField] private DayTransitionController dayTransition;
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
        /// <summary>绑定结束当天的输入与视觉过渡，不把延迟规则放进场景。</summary>
        public void ConfigureDayTransition(DayTransitionController controller) { dayTransition = controller; }
        /// <summary>以当前事实直接恢复所有表现，用于黑屏后的次日回归。</summary>
        public void RestorePresenters()
        {
            if (sequence != null) sequence.ApplyFinalStateImmediately();
            else foreach (var view in presenters) view.ApplyState(Session.State, false);
        }
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
        /// <summary>独立世界样板的应用层入口：提交明确的调试事实，让演出消费者自行响应。</summary>
        /// <remarks>区别于跳转预设，这个入口保留过渡事件；导演本身不能调用它。</remarks>
        public void PlayStarryBenchmark() { Session.DebugAddFact(ExplorationRules.WorldStarry); }
        /// <summary>统一重绑 Presenter、笔记本、调试面板与输入。</summary>
        private void Bind(GameSession session)
        {
            if (dayTransition != null) dayTransition.Cancel();
            if (inspection != null) inspection.Exit(false);
            if (sequence != null) sequence.Cancel();
            Session = session;
            // 所有表现消费同一 Session；不让 Prefab 直接调用其他 Prefab。
            Func<FactId, bool> filter = fact => (dayTransition == null || !dayTransition.IsTransitioning) && (sequence == null || sequence.ShouldPresentImmediately(fact));
            foreach (var view in presenters) view.Bind(Session, filter);
            if (sequence != null) sequence.Bind(Session, presenters);
            notebook.Bind(Session);
            if (inspection != null) inspection.Bind(Session, notebook);
            if (dayTransition != null) dayTransition.Bind(Session, notebook, RestorePresenters);
            debugView.Bind(Session, Restart, JumpTo, AddDebugFact, sequence); interaction.Bind(Session, notebook);
        }
    }
}
