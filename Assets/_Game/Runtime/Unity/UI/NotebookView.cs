// =============================================================================
// 文件：NotebookView.cs
// 职责：显示分类碎片、有限语义画布、组合结果与既有解释；兼容旧灰盒槽位。
// 边界：仅持有界面选择和开关状态；不持有配方答案或世界事实。
// =============================================================================
using System.Collections.Generic;
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Game.Unity
{
    /// <summary>显示已收集碎片、三个组合槽、组合结果与既有解释。</summary>
    public sealed class NotebookView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private NotebookCanvasView semanticCanvas;
        /// <summary>安装有限语义画布；旧灰盒仍可使用三个槽。</summary>
        public void ConfigureCanvas(NotebookCanvasView canvas) { semanticCanvas = canvas; }
        [SerializeField] private Transform objectList;
        [SerializeField] private Transform conceptList;
        [SerializeField] private Transform observationList;
        [SerializeField] private Text insightWord;
        private string formedWord;
        private bool clearedInsightSlots;
        /// <summary>仅为当前纸面书写演出，不是“生长已解锁”的第二份状态。</summary>
        public bool ConceptFormationPlaying => formedWord != null && Time.unscaledTime - connectedAt < 2;
        /// <summary>把观察和概念分开展示，灰盒兼容场景可以继续只用旧两列。</summary>
        public void ConfigureExploration(Transform observations, Text word) { observationList = observations; insightWord = word; }
        [SerializeField] private Button rowTemplate;
        [SerializeField] private Button interpretButton;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text selectionText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Button[] slotButtons;
        [SerializeField] private Text completedText;
        [SerializeField] private Text completedTextSecondary;
        /// <summary>关系增多后用两列保留全部记录，不缩小到不可读或静默裁切。</summary>
        public void ConfigureHistoryColumns(Text first, Text second) { completedText=first; completedTextSecondary=second; }
        [SerializeField] private GameObject discoveryPanel;
        [SerializeField] private Text discoveryText;
        [SerializeField] private RectTransform paperBody;
        [SerializeField] private CanvasGroup paperCanvas;
        [SerializeField] private Image relationLine;
        [SerializeField] private AudioSource paperAudio;
        [SerializeField] private AudioSource inkAudio;
        [SerializeField] private AudioSource scratchAudio;
        private float openedAt, connectedAt = -1;
        // 仅保存临时界面选择，KnownClues 与配方历史从 Session 读取。
        private readonly ClueId?[] selected = new ClueId?[3];
        private GameSession session;
        private float closeAt = -1;
        // 获取通知和未读提醒仅属于 UI，不作为任何世界规则的输入。
        private readonly List<ClueId> recentDiscoveries = new List<ClueId>();
        private float discoveryUntil = -1;
        private float blinkStartedAt;
        private ColorBlock originalButtonColors;
        private bool buttonColorsCaptured;
        private CanvasGroup[] columnCanvas;
        /// <summary>是否有尚未打开笔记本查看的新发现；不代表任何领域事实。</summary>
        public bool HasUnreadDiscoveries { get; private set; }
        /// <summary>笔记本界面开关，仅为临时输入状态。</summary>
        public bool IsOpen => panel.activeSelf;
        /// <summary>检视／跨日过渡的临时输入锁；不进入世界状态。</summary>
        public bool ExternalInputBlocked { get; set; }
        /// <summary>本次界面选择的输入快照，不代表已完成配方。</summary>
        public IReadOnlyList<ClueId> SelectedClues => semanticCanvas != null ? semanticCanvas.CombinationInput : selected.Where(x => x.HasValue).Select(x => x.Value).ToArray();
        private void Awake()
        {
            // 纸面分类淡出只是灵感书写的构图；灰盒没有分列 CanvasGroup 时照常工作。
            columnCanvas = new[] { objectList, observationList, conceptList }.Where(x => x != null)
                .Select(x => x.GetComponent<CanvasGroup>()).Where(x => x != null).ToArray();
        }
        /// <summary>注入列表、模板、反馈和提交按钮。</summary>
        public void Configure(GameObject body, Transform objects, Transform concepts, Button template, Button interpret, Text selection, Text feedback)
        { panel = body; objectList = objects; conceptList = concepts; rowTemplate = template; interpretButton = interpret; selectionText = selection; feedbackText = feedback; }
        /// <summary>注入三个组合槽与已建立解释的历史文本。</summary>
        public void ConfigureSlots(Button[] slots, Text history) { slotButtons = slots; completedText = history; }
        /// <summary>注入面板外的打开按钮与面板内关闭按钮。</summary>
        public void ConfigureControls(Button open, Button close) { openButton = open; closeButton = close; }
        /// <summary>注入画面上方的获取通知，与观察描述分开显示。</summary>
        public void ConfigureDiscoveryFeedback(GameObject notice, Text text)
        {
            discoveryPanel = notice;
            discoveryText = text;
        }
        /// <summary>配置纸页过渡、连接墨线与轻反馈声音；仅为界面演出。</summary>
        public void ConfigurePresentation(RectTransform body, CanvasGroup group, Image line, AudioSource paper, AudioSource ink, AudioSource scratch)
        { paperBody = body; paperCanvas = group; relationLine = line; paperAudio = paper; inkAudio = ink; scratchAudio = scratch; }
        /// <summary>重绑会话与按钮，清空临时选择并重新建立只读视图。</summary>
        /// <param name="game">新的应用层会话；可以是初始会话或已经恢复的调试会话。</param>
        /// <remarks>本组件拥有这些按钮的监听；重绑必须先移除旧监听与旧会话订阅。</remarks>
        public void Bind(GameSession game)
        {
            // 重开或预设跳转时先解绑旧会话，避免旧事件修改新界面。
            if (session != null) session.EventRaised -= OnEvent;
            session = game; if (semanticCanvas != null) semanticCanvas.Bind(game, this); session.EventRaised += OnEvent; closeAt = -1; formedWord = null; if (insightWord != null) insightWord.gameObject.SetActive(false); ClearSelection();
            // 重开与预设跳转不播放翻页声，也不能留下上一会话的声音。
            paperAudio.Stop(); inkAudio.Stop(); scratchAudio.Stop(); SetOpen(false, false);
            // 只缓存一次原始按钮样式；重开时不能把上一会话的闪烁颜色当作初始颜色。
            if (!buttonColorsCaptured)
            {
                originalButtonColors = openButton.colors;
                buttonColorsCaptured = true;
            }
            recentDiscoveries.Clear();
            discoveryUntil = -1;
            discoveryText.text = "";
            discoveryPanel.SetActive(false);
            ClearDiscoveryReminder();
            interpretButton.onClick.RemoveAllListeners(); interpretButton.onClick.AddListener(InterpretSelected);
            openButton.onClick.RemoveAllListeners(); openButton.onClick.AddListener(Toggle);
            closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(() => SetOpen(false));
            for (int i = 0; i < slotButtons.Length; i++) { int index = i; slotButtons[i].onClick.RemoveAllListeners(); slotButtons[i].onClick.AddListener(() => { selected[index] = null; RenderSelection(); }); }
            feedbackText.text = "把细节放在一起。"; Render();
        }
        /// <summary>只修改笔记本界面开关。</summary>
        public void SetOpen(bool open, bool playSound = true)
        {
            if (open && ExternalInputBlocked) return;
            bool changed = panel.activeSelf != open;
            panel.SetActive(open);
            if (changed && playSound) paperAudio.Play();
            openedAt = Time.unscaledTime; connectedAt = -1; relationLine.enabled = false;
            paperBody.localScale = Vector3.one; paperCanvas.alpha = 0;
            // 打开即可确认已看到新收集的条目；关闭笔记本不会凭空产生未读提醒。
            if (open) ClearDiscoveryReminder();
        }
        /// <summary>切换界面开关，不改变游戏事实。</summary>
        public void Toggle() { SetOpen(!IsOpen); }
        /// <summary>新的纸面拖动开始时结束上一段概念书写，不清除这次拖动的卡片。</summary>
        public void PrepareCanvasInput()
        {
            formedWord=null; closeAt=-1; connectedAt=-1; relationLine.enabled=false;
            if(insightWord!=null) insightWord.gameObject.SetActive(false);
        }
        /// <summary>旧物体选择入口，适配为统一碎片选择。</summary>
        public void SelectObject(ObjectId id) => SelectClue(new ClueId(id.Value));
        /// <summary>旧概念选择入口，适配为统一碎片选择。</summary>
        public void SelectConcept(ConceptId id) => SelectClue(new ClueId(id.Value));
        /// <summary>选择或取消已知碎片，填充第一个空槽；满槽时替换第三槽。</summary>
        /// <param name="clue">已收集的稳定碎片身份；未知碎片会被忽略。</param>
        public void SelectClue(ClueId clue)
        {
            if (!session.State.Knows(clue)) return;
            if (formedWord != null) { ClearSelection(); formedWord = null; if (insightWord != null) insightWord.gameObject.SetActive(false); }
            closeAt = -1; connectedAt = -1; relationLine.enabled = false;
            if (semanticCanvas != null) { semanticCanvas.Toggle(clue); return; }
            // 同一碎片只能占一个槽，再次点击会取消；这只是输入组织。
            int existing = System.Array.FindIndex(selected, x => x.HasValue && x.Value == clue);
            if (existing >= 0) selected[existing] = null;
            else { int empty = System.Array.FindIndex(selected, x => !x.HasValue); selected[empty >= 0 ? empty : 2] = clue; }
            RenderSelection();
        }
        // 输入足够后直接提交；配方是否匹配由 RuleEngine 决定。

        /// <summary>将槽中的输入交给 Session，UI 不判断配方答案。</summary>
        public void InterpretSelected() { if (SelectedClues.Count >= 2) session.Combine(SelectedClues); }
        /// <summary>清空三个临时槽，不移除任何已知碎片。</summary>
        private void ClearSelection() { if (semanticCanvas != null) semanticCanvas.Clear(); for (int i = 0; i < selected.Length; i++) selected[i] = null; }
        /// <summary>从内容定义取得中文显示名。</summary>
        private string Label(ClueId id) => session.Rules.Clues.Single(x => x.Id == id).DisplayName;
        /// <summary>处理发现与组合反馈；成功后短暂强调并自动收起。</summary>
        private void OnEvent(DomainEvent e)
        {
            if (e is ClueDiscovered discovered)
            {
                Render();
                ShowDiscovery(discovered.Clue);
            }
            if (e is ObjectInspected inspected) feedbackText.text = inspected.Description;
            if (e is InspectionFailed inspectFailed) feedbackText.text = inspectFailed.Reason;
            if (e is CombinationFailed) { feedbackText.text = "还没有形成联系。"; selectionText.color = new Color(.42f, .35f, .27f); closeAt = -1; relationLine.enabled = false; scratchAudio.Play(); }
            // 成功时世界已经开始响应；界面强调后短暂延时收起。
            if (e is CombinationSucceeded success)
            {
                if (semanticCanvas != null) semanticCanvas.ShowSuccess();
                feedbackText.text = "笔迹落下，房间开始回应。";
                connectedAt = Time.unscaledTime; relationLine.enabled = true; if (semanticCanvas == null) inkAudio.Play();
                // 只根据实际输入数量连接已填槽位，不读取配方答案。
                if (semanticCanvas != null) relationLine.enabled = false;
                relationLine.rectTransform.anchorMin = new Vector2(1f / 6, .350f);
                relationLine.rectTransform.anchorMax = new Vector2(1f / 6 + (SelectedClues.Count - 1) / 3f, .354f);
                selectionText.text = string.Join("  ─  ", SelectedClues.Select(Label)); selectionText.color = new Color(.48f, .37f, .18f);
                var concepts = success.ProducedClues.Where(c => session.Rules.Clues.Single(x => x.Id == c).Category == ClueCategory.Concept).ToArray();
                if (concepts.Length > 0)
                {
                    // 仅根据实际输出事件改变反馈；没有在 UI 匹配输入或复制配方答案。
                    formedWord = string.Join(" · ", concepts.Select(Label)); clearedInsightSlots = false; closeAt = -1;
                    if (insightWord != null) { insightWord.text = ""; insightWord.gameObject.SetActive(true); }
                    feedbackText.text = "一个词，慢慢浮现。";
                }
                else closeAt = Time.unscaledTime + .65f;
                RenderHistory();
            }
        }
        /// <summary>聚合同一轮新发现，显示中文名称并启动未读闪烁。</summary>
        /// <param name="clue">领域首次发现事件中的碎片；兼容事件和重复观察不调用此方法。</param>
        private void ShowDiscovery(ClueId clue)
        {
            // 连续发现共享一张通知，避免盆栽与生长在同一命令中互相覆盖。
            if (Time.unscaledTime >= discoveryUntil) recentDiscoveries.Clear();
            recentDiscoveries.Add(clue);
            discoveryText.text = "新发现\n" + string.Join("、", recentDiscoveries.Select(Label)) + "\n已记入笔记本 · 按 N 查看";
            discoveryUntil = Time.unscaledTime + 5f;
            discoveryPanel.SetActive(true);
            if (!IsOpen)
            {
                if (!HasUnreadDiscoveries) blinkStartedAt = Time.unscaledTime;
                HasUnreadDiscoveries = true;
                openButton.GetComponentInChildren<Text>().text = "新发现 [N]";
            }
        }
        /// <summary>恢复按钮样式并清除未读标记，供打开与重开共用。</summary>
        private void ClearDiscoveryReminder()
        {
            HasUnreadDiscoveries = false;
            if (buttonColorsCaptured) openButton.colors = originalButtonColors;
            openButton.GetComponentInChildren<Text>().text = "笔记本 [N]";
        }
        /// <summary>用非缩放时间更新通知期限和按钮脉冲，不检查世界规则。</summary>
        private void UpdateDiscoveryFeedback()
        {
            if (discoveryUntil > 0 && Time.unscaledTime >= discoveryUntil)
            {
                discoveryUntil = -1;
                discoveryPanel.SetActive(false);
                recentDiscoveries.Clear();
            }
            // 通知五秒后收起，但未读按钮持续闪烁，直到玩家打开笔记本。
            if (!HasUnreadDiscoveries) return;
            float pulse = .5f + .5f * Mathf.Sin((Time.unscaledTime - blinkStartedAt) * Mathf.PI * 3f);
            var colors = originalButtonColors;
            colors.normalColor = Color.Lerp(originalButtonColors.normalColor, new Color(2.5f, 1.7f, .3f), pulse);
            colors.highlightedColor = colors.normalColor;
            colors.selectedColor = colors.normalColor;
            colors.fadeDuration = 0f;
            openButton.colors = colors;
        }
        /// <summary>根据 KnownClues 重建分类列表和解释历史。</summary>
        private void Render()
        {
            ClearRows(objectList); ClearRows(conceptList); if (observationList != null) ClearRows(observationList);
            foreach (var definition in session.Rules.Clues.Where(x => session.State.Knows(x.Id)))
            {
                // 捕获本条碎片的 ID，生成的按钮仅提交选择。
                var id = definition.Id; bool objectClue = definition.Category == ClueCategory.Object;
                bool observation = definition.Category == ClueCategory.Observation;
                var list = objectClue ? objectList : observation && observationList != null ? observationList : conceptList;
                AddRow(list, (objectClue ? "Object_" : observation ? "Observation_" : "Concept_") + id.Value, definition.DisplayName, definition.Category, () => SelectClue(id));
            }
            RenderSelection(); RenderHistory();
        }
        /// <summary>只显示领域记录的已完成配方，不自行维护历史。</summary>
        private void RenderHistory()
        {
            var relations=session.Rules.Recipes.Where(x=>session.State.HasCompleted(x.Id))
                .Select(x=>string.Join(" ─ ",x.Inputs.Select(Label))+(x.ProducesClues.Count>0 ? " → "+string.Join("、",x.ProducesClues.Select(Label)) : "")).ToArray();
            // 两列都是同一份领域历史的视图，没有另外保存已建立关系。
            int split=completedTextSecondary!=null ? (relations.Length+1)/2 : relations.Length;
            completedText.text="留下的联系\n"+string.Join("\n",relations.Take(split));
            if(completedTextSecondary!=null) completedTextSecondary.text="\n"+string.Join("\n",relations.Skip(split));
        }
        /// <summary>先隐藏旧按钮再延迟销毁，避免同一帧残留可点击项。</summary>
        private static void ClearRows(Transform list) { foreach (Transform child in list) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
        /// <summary>从模板创建中文条目，并绑定本条碎片的选择回调。</summary>
        private void AddRow(Transform list, string name, string label, ClueCategory category, UnityEngine.Events.UnityAction select)
        {
            var row = Instantiate(rowTemplate, list);
            row.name = name;
            row.gameObject.SetActive(true);
            var text = row.GetComponentInChildren<Text>();
            text.text = label;
            // 同一碎片输入仍使用同一按钮；字的重量区分具体事物、旁注观察和抽象灵感。
            bool concept = category == ClueCategory.Concept;
            bool observation = category == ClueCategory.Observation;
            text.fontSize = concept ? 26 : observation ? 17 : 21;
            text.color = observation ? new Color(.40f, .38f, .32f) : new Color(.25f, .25f, .21f);
            row.GetComponent<LayoutElement>().preferredHeight = concept ? 44 : observation ? 32 : 36;
            row.targetGraphic = text;
            row.onClick.AddListener(select);
            if (semanticCanvas != null) row.gameObject.AddComponent<NotebookClueDrag>().Configure(semanticCanvas, new ClueId(name.Substring(name.IndexOf('_')+1)), false);
        }
        /// <summary>刷新槽位与提交许可，只校验输入数量。</summary>
        private void RenderSelection()
        {
            if (formedWord != null) return;
            relationLine.enabled = false;
            selectionText.text = "让这些细节靠近一些"; selectionText.color = new Color(.40f, .38f, .33f);
            for (int i = 0; i < slotButtons.Length; i++) slotButtons[i].GetComponentInChildren<Text>().text = selected[i].HasValue ? Label(selected[i].Value) : i == 2 ? "···" : "放下碎片";
            interpretButton.interactable = SelectedClues.Count >= 2;
            var submit = interpretButton.GetComponentInChildren<Text>();
            submit.color = new Color(.40f, .29f, .12f, interpretButton.interactable ? 1 : .35f);
        }
        /// <summary>纸页轻展开，成功时墨线连接并在最后短暂合页；没有配方判断。</summary>
        private void UpdatePaperRitual()
        {
            if (!IsOpen) return;
            float opening = Mathf.Clamp01((Time.unscaledTime - openedAt) / .22f);
            float closing = closeAt > 0 ? Mathf.Clamp01((Time.unscaledTime - closeAt + .20f) / .20f) : 0;
            paperCanvas.alpha = opening * (1 - closing);
            paperBody.localScale = new Vector3(.97f + opening * .03f - closing * .14f, .985f + opening * .015f - closing * .025f, 1);
            if (connectedAt >= 0) relationLine.rectTransform.localScale = new Vector3(Mathf.Clamp01((Time.unscaledTime - connectedAt) / .38f), 1, 1);
            if (columnCanvas != null)
            {
                float alpha = formedWord != null ? .36f : 1;
                foreach (var column in columnCanvas) column.alpha = Mathf.MoveTowards(column.alpha, alpha, Time.unscaledDeltaTime * 2.5f);
            }
        }
        /// <summary>处理面板按键与成功后收起；输入框编辑时不抢占键盘。</summary>
        private void Update()
        {
            UpdateDiscoveryFeedback();
            UpdatePaperRitual();
            if (formedWord != null)
            {
                float elapsed = Time.unscaledTime - connectedAt;
                if (insightWord != null) insightWord.text = formedWord.Substring(0, Mathf.Clamp(Mathf.FloorToInt((elapsed - .4f) * 4), 0, formedWord.Length));
                if (elapsed > 1.6f && !clearedInsightSlots)
                {
                    clearedInsightSlots = true; ClearSelection(); interpretButton.interactable = false;
                    for (int i = 0; i < slotButtons.Length; i++) slotButtons[i].GetComponentInChildren<Text>().text = i == 2 ? "···" : "放下碎片";
                    feedbackText.text = "“" + formedWord + "”。重新看待一件事物。";
                }
            }
            if (closeAt > 0 && Time.unscaledTime >= closeAt) { closeAt = -1; SetOpen(false); ClearSelection(); RenderSelection(); }
            var keyboard = Keyboard.current; if (keyboard == null) return;
            var focused = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            // 编辑调试事实时不抢 N、Tab 和 Esc，保证输入体验。
            if (focused != null && focused.TryGetComponent<InputField>(out var input) && input.isFocused) return;
            if (ExternalInputBlocked) return;
            if (keyboard.nKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame) Toggle();
            if (keyboard.escapeKey.wasPressedThisFrame) SetOpen(false);
        }
        /// <summary>解除会话订阅，避免重开或退出后的重复回调。</summary>
        private void OnDestroy() { if (session != null) session.EventRaised -= OnEvent; }
    }
}
