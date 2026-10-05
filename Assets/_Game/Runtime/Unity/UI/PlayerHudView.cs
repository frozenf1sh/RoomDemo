// =============================================================================
// 文件：PlayerHudView.cs
// 职责：提供近乎无常驻 UI 的玩家画面、短暂发现提示与 Esc 暂停菜单。
// 边界：只读取会话和界面状态；按钮经启动组件／跨日输入提交，不直接修改世界。
// =============================================================================
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Game.Unity
{
    /// <summary>将教学和发现收敛为短暂反馈；日序与重开不占据正常画面。</summary>
    [DefaultExecutionOrder(200)]
    public sealed class PlayerHudView : MonoBehaviour
    {
        [SerializeField] private InspectionController inspection;
        [SerializeField] private NotebookView notebook;
        [SerializeField] private GameObject[] roomOnlyText;
        [SerializeField] private RectTransform discoveryNotice;
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private DayTransitionController dayTransition;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton, restartButton, endDayButton, notebookReminder;
        [SerializeField] private Text tutorial, hover;
        private bool lastNotebook, lastInspection, paused;
        private float startedAt, hoverAt;
        private string lastHover;
        /// <summary>保持旧场景安装接口；隐藏清单在正式模式始终收起。</summary>
        public void Configure(InspectionController controller, NotebookView notes, GameObject[] roomText, RectTransform notice)
        { inspection = controller; notebook = notes; roomOnlyText = roomText; discoveryNotice = notice; }
        /// <summary>注入暂停菜单和临时提示；没有查找全局对象或单例。</summary>
        public void ConfigureCleanHud(GameBootstrap game, DayTransitionController days, GameObject pause, Button resume,
            Button restart, Button endDay, Button reminder, Text firstTip, Text hoverTip)
        {
            bootstrap = game; dayTransition = days; pausePanel = pause; resumeButton = resume;
            restartButton = restart; endDayButton = endDay; notebookReminder = reminder; tutorial = firstTip; hover = hoverTip;
        }
        private void Start()
        {
            startedAt = Time.unscaledTime;
            if (pausePanel == null) return;
            pausePanel.SetActive(false);
            resumeButton.onClick.AddListener(() => SetPaused(false));
            restartButton.onClick.AddListener(() => { SetPaused(false); bootstrap.Restart(); });
            endDayButton.onClick.AddListener(() => { SetPaused(false); dayTransition.Begin(); });
        }
        /// <summary>暂停只拥有临时输入锁和菜单；不改变领域事实或演出时间。</summary>
        public void SetPaused(bool enabled)
        {
            paused = enabled; pausePanel.SetActive(enabled); notebook.ExternalInputBlocked = enabled;
            if (enabled) endDayButton.gameObject.SetActive(bootstrap.Session.CanAdvanceDay);
        }
        private void Update()
        {
            if (pausePanel == null) return;
            // 检视与笔记先消费 Esc，不能在退出它们的同一帧误打开暂停菜单。
            bool uiWasOpen = lastNotebook || lastInspection || notebook.IsOpen || inspection.IsOpen;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !uiWasOpen &&
                (paused || !notebook.ExternalInputBlocked || bootstrap.Session.State.HasFact(Game.Domain.NarrativeRules.BanalComplete))) SetPaused(!paused);
            lastNotebook = notebook.IsOpen; lastInspection = inspection.IsOpen;
        }
        private void LateUpdate()
        {
            // 开发面板仍由自己的 F 键开关控制；旧标题、故事和操作栏不再常驻。
            foreach (var node in roomOnlyText) if (node != null) node.SetActive(false);
            if (notebookReminder == null) return;
            bool inspecting = inspection.IsOpen;
            notebookReminder.gameObject.SetActive(notebook.HasUnreadDiscoveries && !notebook.IsOpen && !paused);
            // 仅发现后出现小笔记记号；闪烁颜色由 NotebookView 维护。
            notebookReminder.GetComponentInChildren<Text>().text = "笔记 · N";
            discoveryNotice.anchorMin = inspecting ? new Vector2(.78f, .55f) : new Vector2(.72f, .10f);
            discoveryNotice.anchorMax = inspecting ? new Vector2(.97f, .73f) : new Vector2(.97f, .24f);
            discoveryNotice.offsetMin = discoveryNotice.offsetMax = Vector2.zero;
            float age = Time.unscaledTime - startedAt;
            tutorial.color = new Color(.9f, .84f, .72f, Mathf.Clamp01((3.4f - age) / .7f));
            tutorial.gameObject.SetActive(age < 3.4f && !inspecting && !notebook.IsOpen && !paused);
            // 同一目标的悬停只短暂出现，不以物体名称铺满场景。
            if (hover.text != lastHover) { lastHover = hover.text; hoverAt = Time.unscaledTime; }
            hover.color = new Color(.9f, .85f, .75f, Mathf.Clamp01((2.4f - (Time.unscaledTime - hoverAt)) / .6f));
            hover.gameObject.SetActive(!inspecting && !notebook.IsOpen && !paused);
        }
        private void OnDisable()
        {
            // 场景卸载或重开必须归还仅由暂停持有的输入锁。
            if (paused && notebook != null) notebook.ExternalInputBlocked = false;
            paused = false;
        }
    }
}
