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
        [SerializeField] private StarryWorldSequence starrySequence;
        [SerializeField] private bool developmentMode;
        [SerializeField] private ContentFeedback contentFeedback;
        [SerializeField] private bool narrativeEnabled;
        [SerializeField] private NarrationSystem narration;
        [SerializeField] private CanvasRoutePresenter canvasRoute;
        [SerializeField] private CanvasEndingSequence canvasEnding;
        /// <summary>正式叙事场景的显式引用；灰盒和历史回归仍使用各自内容表。</summary>
        public bool NarrativeEnabled => narrativeEnabled;
        /// <summary>增量接入画布叙事，消费者只接收同一个会话，不互相控制。</summary>
        public void ConfigureNarrative(NarrationSystem voice, CanvasRoutePresenter canvas, CanvasEndingSequence ending)
        { narrativeEnabled = true; narration = voice; canvasRoute = canvas; canvasEnding = ending; }
        /// <summary>主线微反馈的显式绑定；历史灰盒可以没有本组件。</summary>
        public void ConfigureFeedback(ContentFeedback feedback) { contentFeedback = feedback; }
        [SerializeField] private TextMesh[] worldLabels;
        [SerializeField] private PresentationDebugView presentationDebug;
        [SerializeField] private WorldDebugView worldDebug;
        [SerializeField] private BenchmarkReviewDriver reviewDriver;
        private bool legacyRegression;
        /// <summary>技术回归入口使用旧内容表；玩家的重开仍默认使用探索内容。</summary>
        public void RestartLegacyForRegression() { legacyRegression = true; Restart(); }
        /// <summary>由场景工具启用新流程，不把玩法规则分散进 Inspector。</summary>
        public void ConfigureExploration(bool enabled) { explorationEnabled = enabled; }
        /// <summary>当前会话引用；重开与跳转会整体替换。</summary>
        public GameSession Session { get; private set; }
        /// <summary>显式开发开关；编辑器也默认使用正式玩家显示，不自动暴露调试键。</summary>
        public bool DevelopmentMode => developmentMode;
        /// <summary>技术工具显式注入标签与面板；没有每帧场景查找。</summary>
        public void ConfigureDevelopmentTools(TextMesh[] labels, PresentationDebugView seaDebug, WorldDebugView worldTools, BenchmarkReviewDriver review)
        { worldLabels = labels; presentationDebug = seaDebug; worldDebug = worldTools; reviewDriver = review; }
        /// <summary>只调整开发显示和输入许可；不改变会话、配方或事实。</summary>
        public void SetDevelopmentMode(bool enabled)
        {
            developmentMode = enabled;
            if (worldLabels != null) foreach (var label in worldLabels) if (label != null) label.gameObject.SetActive(enabled);
            debugView.SetDevelopmentMode(enabled);
            if (presentationDebug != null) presentationDebug.SetDevelopmentMode(enabled);
            if (worldDebug != null) worldDebug.SetDevelopmentMode(enabled);
            if (reviewDriver != null) reviewDriver.SetDevelopmentMode(enabled);
        }
        /// <summary>由场景生成器显式注入组件引用。</summary>
        public void Configure(WorldPresenter[] views, NotebookView notebookView, DebugView debug, InteractionSystem input)
        { presenters = views; notebook = notebookView; debugView = debug; interaction = input; }
        /// <summary>注入本房间的演出导演，核心会话不依赖它。</summary>
        public void ConfigureSequence(PaintingSeaSequence director) { sequence = director; }
        /// <summary>显式绑定检视输入，重开时负责退出并释放临时相机。</summary>
        public void ConfigureInspection(InspectionController controller) { inspection = controller; }
        /// <summary>绑定结束当天的输入与视觉过渡，不把延迟规则放进场景。</summary>
        public void ConfigureDayTransition(DayTransitionController controller) { dayTransition = controller; }
        /// <summary>注入只消费世界事实的具体星空导演。</summary>
        public void ConfigureStarrySequence(StarryWorldSequence director) { starrySequence = director; }
        /// <summary>以当前事实直接恢复所有表现，用于黑屏后的次日回归。</summary>
        public void RestorePresenters()
        {
            if (starrySequence != null) starrySequence.Cancel();
            if (sequence != null) sequence.ApplyFinalStateImmediately();
            else foreach (var view in presenters) view.ApplyState(Session.State, false);
            if (starrySequence != null) starrySequence.ApplyFinalStateImmediately();
            if (canvasRoute != null && canvasRoute.gameObject.activeSelf) canvasRoute.Restore();
        }
        /// <summary>在表现组件缓存初始姿态后创建会话，并设置帧率。</summary>
        private void Awake()
        {
            // 命令行开发参数是明确的调试入口，正常运行不开放 F 键。
            SetDevelopmentMode(developmentMode || Array.IndexOf(Environment.GetCommandLineArgs(), "-roomDemoDevelopment") >= 0);
            Restart(); UnityEngine.Application.targetFrameRate = 60; UnityEngine.Application.runInBackground = true;
        }
        /// <summary>创建全新会话并同步恢复所有表现。</summary>
        public void Restart() => Bind(new GameSession(CurrentRules()));
        private RuleSet CurrentRules() => explorationEnabled && !legacyRegression
            ? (narrativeEnabled ? NarrativeRules.Create() : MainlineRules.Create()) : DemoRules.Create();
        /// <summary>先恢复新会话，再用它替换当前会话。</summary>
        public void JumpTo(DebugPreset preset)
        {
            bool exploration = explorationEnabled && !legacyRegression;
            Bind(GameSession.FromPreset(CurrentRules(), exploration ? ExplorationPresets.AdaptLegacy(preset) : preset));
        }
        /// <summary>通过应用层添加事实，然后立即恢复最终表现。</summary>
        public void AddDebugFact(FactId fact)
        {
            Session.DebugAddFact(fact);
            // 调试添加事实明确要求即时恢复；已启动的演出必须先取消。
            RestorePresenters();
        }
        /// <summary>独立世界样板的应用层入口：提交明确的调试事实，让演出消费者自行响应。</summary>
        /// <remarks>区别于跳转预设，这个入口保留过渡事件；导演本身不能调用它。</remarks>
        public void PlayStarryBenchmark()
        {
            if (inspection != null) inspection.Exit(false);
            notebook.SetOpen(false);
            // 同一个房间只有一个整体导演持有光照；先结清海画，再开始世界重构。
            if (sequence != null && sequence.IsPlaying) sequence.ApplyFinalStateImmediately();
            Session.DebugAddFact(ExplorationRules.WorldStarry);
        }
        /// <summary>只预览整体世界演出；先归还海画表现所有权，不添加星空事实。</summary>
        public void PreviewStarryBenchmark()
        {
            if (inspection != null) inspection.Exit(false);
            notebook.SetOpen(false);
            ResetStarryPresentation(); starrySequence.PlayBenchmarkPreview();
        }
        /// <summary>归还星空借用的建筑，恢复原有物件表现；领域中的星空事实不撤销。</summary>
        public void ResetStarryPresentation()
        {
            starrySequence.Cancel();
            if (sequence != null) sequence.ApplyFinalStateImmediately();
            else foreach (var view in presenters) view.ApplyState(Session.State, false);
        }
        /// <summary>统一重绑 Presenter、笔记本、调试面板与输入。</summary>
        private void Bind(GameSession session)
        {
            if (dayTransition != null) dayTransition.Cancel();
            if (inspection != null) inspection.Exit(false);
            if (starrySequence != null) starrySequence.Cancel();
            if (sequence != null) sequence.Cancel();
            if (canvasEnding != null) canvasEnding.Cancel();
            Session = session;
            // 所有表现消费同一 Session；不让 Prefab 直接调用其他 Prefab。
            Func<FactId, bool> filter = fact => (dayTransition == null || !dayTransition.IsTransitioning) && (sequence == null || sequence.ShouldPresentImmediately(fact));
            foreach (var view in presenters)
            {
                if(view is EndingPresenter ending) ending.SuppressBenchmarkEnding(explorationEnabled && !legacyRegression);
                view.Bind(Session, filter);
            }
            if (sequence != null) sequence.Bind(Session, presenters);
            if (starrySequence != null) starrySequence.Bind(Session);
            notebook.Bind(Session);
            if (contentFeedback != null) contentFeedback.Bind(Session);
            bool useNarrative = narrativeEnabled && !legacyRegression;
            if (canvasRoute != null)
            {
                canvasRoute.gameObject.SetActive(useNarrative);
                if (useNarrative) canvasRoute.Bind(Session);
                else canvasRoute.Hide();
            }
            if (narration != null)
            {
                narration.gameObject.SetActive(useNarrative);
                if (useNarrative) narration.Bind(Session);
            }
            if (canvasEnding != null)
            {
                canvasEnding.gameObject.SetActive(useNarrative);
                if (useNarrative) canvasEnding.Bind(Session);
            }
            if (inspection != null) inspection.Bind(Session, notebook);
            if (dayTransition != null) dayTransition.Bind(Session, notebook, RestorePresenters);
            debugView.Bind(Session, Restart, JumpTo, AddDebugFact, sequence); interaction.Bind(Session, notebook);
        }
    }
}
