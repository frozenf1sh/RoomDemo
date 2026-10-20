// =============================================================================
// 文件：NotebookView.cs
// 职责：显示思想材料、有限语义画布、组合结果与既有解释；兼容旧灰盒槽位。
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
    /// <summary>显示已收集碎片、有限组合区、组合结果与既有解释。</summary>
    public sealed class NotebookView : MonoBehaviour
    {
        [SerializeField] private bool quietStudio;
        private bool revealNewestOnOpen;
        [SerializeField] private Text clueDescription;
        /// <summary>只通知实际界面操作，教程不能借此添加世界事实。</summary>
        public event System.Action<bool> OpenChanged;
        /// <summary>用户点击词语阅读说明。</summary>
        public event System.Action<ClueId> ClueRead;
        /// <summary>帮助和详情覆盖时暂停结果预览的可见计时。</summary>
        public System.Func<bool> PreviewCoverQuery;
        public bool PreviewCovered => (GetComponent<AtelierNotebookHelp>()?.IsVisible ?? false)||(PreviewCoverQuery?.Invoke()??false);
        public GameObject PanelObject => panel;
        public Transform MaterialsRoot => leftMaterialsRoot != null ? leftMaterialsRoot
            : unifiedWords != null ? unifiedWords.GetComponentInParent<ScrollRect>()?.transform ?? unifiedWords : objectList;
        public Text DescriptionLabel => clueDescription;
        public Font NotebookFont => rowTemplate.GetComponentInChildren<Text>().font;

        /// <summary>纸面拖动完成后的输入快照通知。</summary>
        public event System.Action CanvasChanged;
        /// <summary>仅在玩家提交之后接管需要世界手势确认的输入；返回 false 保留原提交路径。</summary>
        public System.Func<IReadOnlyList<ClueId>, bool> SubmissionHandler { get; set; }
        /// <summary>独立画室由暂停菜单消费 Esc；历史界面仍可用 Esc 关书。</summary>
        public bool EscapeClosesNotebook { get; set; } = true;
        /// <summary>固定说明区域位于组合区域下方。</summary>
        public void ConfigureDescription(Text text) { clueDescription = text; }
        /// <summary>由画布适配器通知真实拖动结果。</summary>
        public void NotifyCanvasChanged() { CanvasChanged?.Invoke(); }
        private float clearAfter = -1;
        /// <summary>新版笔记去除装饰性说明，所有词用颜色和相同字号区分。</summary>
        public void ConfigureQuietStudio(bool enabled) { quietStudio = enabled; }
        [SerializeField] private GameObject panel;
        [SerializeField] private NotebookCanvasView semanticCanvas;
        /// <summary>安装有限语义画布；旧灰盒仍可使用三个槽。</summary>
        public void ConfigureCanvas(NotebookCanvasView canvas) { semanticCanvas = canvas; }
        /// <summary>当前组合纸面适配器；只管理临时卡片，正式材料仍读取会话。</summary>
        public NotebookCanvasView CombinationCanvas => semanticCanvas;
        [SerializeField] private Transform objectList;
        [SerializeField] private Transform conceptList;
        [SerializeField] private Transform observationList;
        [SerializeField] private Transform logicList;
        [SerializeField] private Text explorationHint;
        [SerializeField] private Text logicHint;
        [SerializeField] private Text insightWord;
        [SerializeField] private Transform unifiedWords;
        [SerializeField] private Transform brushList;
        [SerializeField] private Transform leftMaterialsRoot;
        [SerializeField] private Text resultNote;
        [SerializeField] private Text canvasMarks;
        /// <summary>所有材料共用左侧列表，正式画室以颜色区分；旧灰盒继续使用原列。</summary>
        public void ConfigureUnifiedWords(Transform words, Text marks) { unifiedWords = words; canvasMarks = marks; }
        private string formedWord;
        private bool clearedInsightSlots;
        /// <summary>仅为当前纸面书写演出，不是“生长已解锁”的第二份状态。</summary>
        public bool ConceptFormationPlaying => formedWord != null && Time.unscaledTime - connectedAt < 2;
        /// <summary>把观察和概念分开展示，灰盒兼容场景可以继续只用旧两列。</summary>
        /// <remarks>传入 <c>null</c> 只表示不改动既有观察列，避免后续仅配置新词显示时误清空形容词区。</remarks>
        public void ConfigureExploration(Transform observations, Text word)
        {
            if (observations != null) observationList = observations;
            insightWord = word;
        }
        /// <summary>将左页材料按词性分入名词、形容词和其他列；不改变既有碎片分类。</summary>
        public void ConfigureWordCategories(Transform nouns, Transform adjectives, Transform others)
        { objectList = nouns; observationList = adjectives; conceptList = others; unifiedWords = null; }
        /// <summary>纯事实解释没有产物词条，改用右页宽字幕；不生成会被卡片宽度截断的结果卡。</summary>
        public void ConfigureResultNote(Text note) { resultNote = note; }
        /// <summary>配置右页灵感清单与两条探索提示；两者只读取领域状态。</summary>
        public void ConfigureLogicShelf(Transform shelf, Text objectHint, Text inspirationHint)
        { logicList = shelf; explorationHint = objectHint; logicHint = inspirationHint; }
        [SerializeField] private Button rowTemplate;
        [SerializeField] private Button interpretButton;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button returnButton;
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
            columnCanvas = new[] { unifiedWords != null ? unifiedWords : objectList, observationList, conceptList, brushList, leftMaterialsRoot }.Where(x => x != null)
                .Select(x => x.GetComponent<CanvasGroup>()).Where(x => x != null).ToArray();
        }
        /// <summary>注入列表、模板、反馈和提交按钮。</summary>
        public void Configure(GameObject body, Transform objects, Transform concepts, Button template, Button interpret, Text selection, Text feedback)
        { panel = body; objectList = objects; conceptList = concepts; rowTemplate = template; interpretButton = interpret; selectionText = selection; feedbackText = feedback; }
        /// <summary>注入三个组合槽与已建立解释的历史文本。</summary>
        public void ConfigureSlots(Button[] slots, Text history) { slotButtons = slots; completedText = history; }
        /// <summary>注入面板外的打开按钮与面板内关闭按钮。</summary>
        public void ConfigureControls(Button open, Button close) { openButton = open; closeButton = close; }
        /// <summary>配置左页返回箭头；点击只合上笔记本，不改变房间事实。</summary>
        public void ConfigureReturnButton(Button button) { returnButton = button; }
        /// <summary>注入画面上方的获取通知，与观察描述分开显示。</summary>
        public void ConfigureDiscoveryFeedback(GameObject notice, Text text)
        {
            discoveryPanel = notice;
            discoveryText = text;
        }
        /// <summary>配置纸页过渡、连接墨线与轻反馈声音；仅为界面演出。</summary>
        public void ConfigurePresentation(RectTransform body, CanvasGroup group, Image line, AudioSource paper, AudioSource ink, AudioSource scratch)
        { paperBody = body; paperCanvas = group; relationLine = line; paperAudio = paper; inkAudio = ink; scratchAudio = scratch; }
        /// <summary>恢复左页按类型分区：物件、观察、概念、笔刷；仍保持四阶段静默纸面。</summary>
        public void ConfigureCategorizedLayout()
        {
            quietStudio = true;
            if (semanticCanvas != null)
            {
                semanticCanvas.ConfigureQuietStudio(true);
                semanticCanvas.ConfigureBlindComposition(true);
            }
            // 分区版不再把左页并成一条总列表；四个列表的引用由场景/母版显式给出。
            unifiedWords = null;
        }
        /// <summary>装配四个等面积分区与覆盖四区的标注根；分区只影响显示，不改变材料许可。</summary>
        public void ConfigureCategorizedLayout(Transform nouns, Transform adjectives, Transform others, Transform brushes, Transform leftRoot)
        {
            objectList = nouns;
            observationList = adjectives;
            conceptList = others;
            brushList = brushes;
            leftMaterialsRoot = leftRoot;
            ConfigureCategorizedLayout();
        }
        /// <summary>历史四阶段合区入口保留给旧场景；当前正式副本改用四区布局。</summary>
        public void ConfigureFourStageLayout()
        {
            quietStudio = true;
            if (semanticCanvas != null)
            {
                semanticCanvas.ConfigureQuietStudio(true);
                semanticCanvas.ConfigureBlindComposition(true);
            }
            // 复用现有滚动区，不创建第二份材料，也不改动收藏和画布工作区。
            if (unifiedWords == null && objectList != null)
            {
                var scroll = objectList.GetComponentInParent<ScrollRect>();
                if (scroll != null)
                {
                    var rect = (RectTransform)scroll.transform;
                    rect.anchorMin = new Vector2(.065f,.20f); rect.anchorMax = new Vector2(.47f,.80f);
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    unifiedWords = objectList;
                    foreach (var old in new[] { conceptList, observationList })
                    {
                        var other = old != null ? old.GetComponentInParent<ScrollRect>() : null;
                        if (other != null && other != scroll) other.gameObject.SetActive(false);
                    }
                    var heading = panel.transform.Find("名词标题")?.GetComponent<Text>();
                    if (heading != null) heading.text = "已发现的词条";
                }
            }
        }
        /// <summary>重绑会话与按钮，清空临时选择并重新建立只读视图。</summary>
        /// <param name="game">新的应用层会话；可以是初始会话或已经恢复的调试会话。</param>
        /// <remarks>本组件拥有这些按钮的监听；重绑必须先移除旧监听与旧会话订阅。</remarks>
        public void Bind(GameSession game)
        {
            revealNewestOnOpen = false; clearAfter = -1;
            if (clueDescription != null) clueDescription.text = "";
            // 重开或预设跳转时先解绑旧会话，避免旧事件修改新界面。
            if (session != null) session.EventRaised -= OnEvent;
            session = game;
            if (game.Rules.DeclaredFacts.Contains(AtelierFlowRules.Enabled)) ConfigureCategorizedLayout();
            if (semanticCanvas != null) semanticCanvas.Bind(game, this); session.EventRaised += OnEvent; closeAt = -1; formedWord = null; if (insightWord != null) insightWord.gameObject.SetActive(false); if (resultNote != null) { resultNote.text = ""; resultNote.gameObject.SetActive(false); } ClearSelection();
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
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => SetOpen(false));
            }
            if (returnButton != null)
            {
                returnButton.onClick.RemoveAllListeners();
                returnButton.onClick.AddListener(() => SetOpen(false));
            }
            for (int i = 0; i < slotButtons.Length; i++) { int index = i; slotButtons[i].onClick.RemoveAllListeners(); slotButtons[i].onClick.AddListener(() => { selected[index] = null; RenderSelection(); }); }
            feedbackText.text = quietStudio ? "" : "把细节放在一起。"; Render();
            if(game.Rules.DeclaredFacts.Contains(AtelierFlowRules.Enabled))
            {
                var help=GetComponent<AtelierNotebookHelp>();
                if(help==null)help=gameObject.AddComponent<AtelierNotebookHelp>();
                help.Bind(this,game);
            }
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
            if (open)
            {
                if (revealNewestOnOpen)
                {
                    // 整体揭示之后把最新词带入视窗；分区版四列都滚到底，避免房间词藏在某列底部。
                    Canvas.ForceUpdateCanvases();
                    foreach (var list in new[] {unifiedWords,objectList,observationList,conceptList,brushList}.Where(x=>x!=null).Distinct())
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)list);
                        var scroll = list.GetComponentInParent<ScrollRect>();
                        if (scroll != null) scroll.verticalNormalizedPosition = 0;
                    }
                    revealNewestOnOpen = false;
                }
                ClearDiscoveryReminder();
            }
            OpenChanged?.Invoke(open);
        }
        /// <summary>切换界面开关，不改变游戏事实。</summary>
        public void Toggle() { SetOpen(!IsOpen); }
        /// <summary>新的纸面拖动开始时结束上一段概念书写，不清除这次拖动的卡片。</summary>
        public void PrepareCanvasInput()
        {
            formedWord=null; closeAt=-1; clearAfter=-1; connectedAt=-1; relationLine.enabled=false;
            if(insightWord!=null) insightWord.gameObject.SetActive(false);
            // 新的输入不继承上一项提交结果，避免尚未解释就显示“解释已留下”。
            if (clueDescription != null) clueDescription.text = "";
        }
        /// <summary>旧物体选择入口，适配为统一碎片选择。</summary>
        public void SelectObject(ObjectId id) => SelectClue(new ClueId(id.Value));
        /// <summary>旧概念选择入口，适配为统一碎片选择。</summary>
        public void SelectConcept(ConceptId id) => SelectClue(new ClueId(id.Value));
        /// <summary>正式纸面只阅读已知碎片；历史灰盒保留槽位选择兼容行为。</summary>
        /// <param name="clue">已收集的稳定碎片身份；未知碎片会被忽略。</param>
        public void SelectClue(ClueId clue)
        {
            if (!session.State.CanUseClue(clue)) return;
            if (semanticCanvas != null && quietStudio)
            {
                // 默认点击只阅读；独立玩法保留拖动和点放，容量由当前规则决定。
                if (clueDescription != null) clueDescription.text = StudioNarration.Description(clue, session.Rules);
                if (semanticCanvas.ClickPlacement)
                {
                    semanticCanvas.Pick(clue);
                    if (clueDescription != null) clueDescription.text = Label(clue) + "\n点击右页空处放下，或直接拖过去。";
                }
                ClueRead?.Invoke(clue);
                return;
            }
            if (formedWord != null) { ClearSelection(); formedWord = null; if (insightWord != null) insightWord.gameObject.SetActive(false); }
            closeAt = -1; connectedAt = -1; relationLine.enabled = false;
            if (semanticCanvas != null) { semanticCanvas.Toggle(clue); return; }
            // 同一碎片只能占一个槽，再次点击会取消；这只是输入组织。
            int existing = System.Array.FindIndex(selected, x => x.HasValue && x.Value == clue);
            if (existing >= 0) selected[existing] = null;
            else
            {
                int capacity = session.Rules.MaximumCombinationInputs;
                int empty = System.Array.FindIndex(selected,0,capacity,x => !x.HasValue);
                if (empty < 0 && capacity == 2) return;
                selected[empty >= 0 ? empty : 2] = clue;
            }
            RenderSelection();
        }
        // 输入足够后直接提交；配方是否匹配由 RuleEngine 决定。

        /// <summary>将槽中的输入交给 Session，UI 不判断配方答案。</summary>
        public void InterpretSelected()
        {
            var input = SelectedClues.ToArray();
            if (input.Length < 2) return;
            // 准备手势不预判拖动、不写事实；确认后仍需由会话重新验证这一输入快照。
            if (SubmissionHandler != null && SubmissionHandler(input)) return;
            session.Combine(input);
        }
        /// <summary>清空三个临时槽，不移除任何已知碎片。</summary>
        private void ClearSelection() { if (semanticCanvas != null) semanticCanvas.Clear(); for (int i = 0; i < selected.Length; i++) selected[i] = null; }
        /// <summary>从内容定义取得中文显示名。</summary>
        private string Label(ClueId id) => session.Rules.Clues.Single(x => x.Id == id).DisplayName;
        /// <summary>处理发现与组合反馈；正式纸面由玩家合上，历史灰盒保留自动收起。</summary>
        private void OnEvent(DomainEvent e)
        {
            if (e is ClueDiscovered discovered)
            {
                Render();
                ShowDiscovery(discovered.Clue);
            }
            if (e is LogicDiscovered || e is LogicConsumed) Render();
            if (e is ObjectInspected inspected) feedbackText.text = inspected.Description;
            if (e is InspectionFailed inspectFailed) feedbackText.text = inspectFailed.Reason;
            if (e is CombinationFailed)
            {
                feedbackText.text = "还没有形成联系。";
                selectionText.color = new Color(.42f, .35f, .27f);
                closeAt = -1; relationLine.enabled = false;
                scratchAudio.Play();
                // 正式纸面自行回应这次尝试，不等合上笔记才看到失败字幕。
                if (quietStudio) semanticCanvas?.ShowFailure();
            }
            // 成功时世界已经开始响应；界面强调后短暂延时收起。
            if (e is CombinationSucceeded success)
            {
                if (semanticCanvas != null && session.Rules.MaximumCombinationInputs == 2)
                {
                    // 领域已完成消费和产出。立即隐藏旧卡，取消旧延迟清理，再显示真实结果。
                    PrepareCanvasInput();
                    ClearSelection();
                    var worldResult=success.RecipeId.EndsWith("sky_canvas")?"可以在天空作画了。":
                        success.RecipeId.EndsWith("roof_paint")?"屋顶的表层像干颜料。":
                        success.RecipeId.EndsWith("room_paint")?"房间表层可以推开了。":"解释已经记下。";
                    semanticCanvas.ShowResult(success.ProducedClues,worldResult);
                    // 没有产物词条的世界解释改用右页宽字幕；词条配方仍显示原来的结果卡。
                    if(resultNote!=null)
                    {
                        resultNote.text=success.ProducedClues.Count==0?worldResult:"";
                        resultNote.gameObject.SetActive(success.ProducedClues.Count==0);
                        if(success.ProducedClues.Count>0)resultNote.transform.SetAsLastSibling();
                    }
                    feedbackText.text = "";
                    selectionText.text = "";
                    Render();
                    return;
                }
                if (semanticCanvas != null) semanticCanvas.ShowSuccess();
                feedbackText.text = "笔迹落下，房间开始回应。";
                connectedAt = Time.unscaledTime; relationLine.enabled = true; if (semanticCanvas == null) inkAudio.Play();
                // 只根据实际输入数量连接已填槽位，不读取配方答案。
                if (semanticCanvas != null) relationLine.enabled = false;
                if (semanticCanvas == null)
                {
                    // 旧槽位连线不改写新纸面淡线的锚点，避免下一次提交时偏移。
                    relationLine.rectTransform.anchorMin = new Vector2(1f / 6, .350f);
                    relationLine.rectTransform.anchorMax = new Vector2(1f / 6 + (SelectedClues.Count - 1) / 3f, .354f);
                }
                selectionText.text = string.Join("  ─  ", SelectedClues.Select(Label)); selectionText.color = new Color(.48f, .37f, .18f);
                var concepts = success.ProducedClues.Where(c => session.Rules.Clues.Single(x => x.Id == c).Category == ClueCategory.Concept).ToArray();
                if (concepts.Length > 0)
                {
                    // 仅根据实际输出事件改变反馈；没有在 UI 匹配输入或复制配方答案。
                    formedWord = string.Join(" · ", concepts.Select(Label)); clearedInsightSlots = false; closeAt = -1;
                    if (insightWord != null) { insightWord.text = ""; insightWord.gameObject.SetActive(true); }
                    feedbackText.text = quietStudio ? "" : "一个词，慢慢浮现。";
                }
                else if (quietStudio)
                {
                    clearAfter = Time.unscaledTime + .9f;
                    if (clueDescription != null) clueDescription.text = "解释已留下。合上笔记本看看。";
                }
                else closeAt = Time.unscaledTime + .65f;
                RenderHistory();
            }
        }
        /// <summary>聚合同一轮新发现，显示中文名称并启动未读闪烁。</summary>
        /// <param name="clue">领域首次发现事件中的碎片；兼容事件和重复观察不调用此方法。</param>
        private void ShowDiscovery(ClueId clue)
        {
            // 不在材料栏展示的旧来源也不冒出“获得词条”通知；知识仍由领域保存。
            if (session.Rules.DeclaredFacts.Contains(AtelierFlowRules.Enabled) && !session.AtelierNotebookMaterials.Contains(clue)) return;
            // 连续发现共享一张通知，避免盆栽与生长在同一命令中互相覆盖。
            if (Time.unscaledTime >= discoveryUntil) recentDiscoveries.Clear();
            // 房间第一次成为词条时仅留一声与小笔记记号，避免弹窗替玩家解释题眼。
            bool quiet = session.Rules.Clues.Single(x=>x.Id==clue).QuietDiscovery;
            if (quietStudio && quiet) revealNewestOnOpen = true;
            if (!quiet) recentDiscoveries.Add(clue);
            discoveryText.text = quietStudio ? string.Join("、", recentDiscoveries.Skip(Mathf.Max(0,recentDiscoveries.Count-2)).Select(Label)) : "新发现\n" + string.Join("、", recentDiscoveries.Select(Label)) + "\n已记入笔记本 · 按 N 查看";
            discoveryUntil = Time.unscaledTime + 5f;
            if (!quiet) discoveryPanel.SetActive(true);
            if (!IsOpen)
            {
                if (!HasUnreadDiscoveries) blinkStartedAt = Time.unscaledTime;
                HasUnreadDiscoveries = true;
                openButton.GetComponentInChildren<Text>().text = quietStudio ? "笔记 · N" : "新发现 [N]";
            }
        }
        /// <summary>恢复按钮样式并清除未读标记，供打开与重开共用。</summary>
        private void ClearDiscoveryReminder()
        {
            HasUnreadDiscoveries = false;
            if (buttonColorsCaptured) openButton.colors = originalButtonColors;
            openButton.GetComponentInChildren<Text>().text = quietStudio ? "笔记 · N" : "笔记本 [N]";
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
        /// <summary>从 KnownClues 重建思想材料与关系；历史灰盒保留分类布局。</summary>
        private void Render()
        {
            var scroll = unifiedWords != null ? unifiedWords.GetComponentInParent<ScrollRect>() : null;
            float savedScroll = scroll != null ? scroll.verticalNormalizedPosition : 1;
            // 统一列表复用原名词节点，同一行只能清理一次。
            foreach (var list in new[] {objectList,conceptList,observationList,brushList,logicList,unifiedWords}.Where(x=>x!=null).Distinct())ClearRows(list);
            // 依获得顺序排列，不把思想材料重新分成物品栏和技能栏。
            var definitions = session.Rules.DeclaredFacts.Contains(AtelierFlowRules.Enabled) ? session.AtelierNotebookMaterials.Select(id => session.Rules.Clues.Single(x=>x.Id==id))
                : session.Rules.Clues.Where(x => session.State.CanUseClue(x.Id));
            foreach (var definition in definitions)
            {
                // 捕获本条碎片的 ID，生成的按钮仅提交选择。
                var id = definition.Id;
                var list = unifiedWords != null ? unifiedWords : AtelierNotebookInk.IsBrush(id) ? brushList ?? conceptList
                    : definition.PartOfSpeech == CluePartOfSpeech.Noun ? objectList
                    : definition.PartOfSpeech == CluePartOfSpeech.Adjective ? observationList : conceptList;
                AddRow(list, definition.PartOfSpeech + "_" + id.Value, definition.DisplayName, definition.Category, () => SelectClue(id),id);
            }
            if (logicList != null)
                foreach (var logic in session.Rules.KnownLogics(session.State)) AddLogicRow(logic);
            RenderSelection(); RenderHistory();
            RefreshCanvasMarks();
            RefreshHints();
            if (scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)unifiedWords);
                scroll.verticalNormalizedPosition = savedScroll;
            }
        }
        /// <summary>只显示领域记录的已完成配方，不自行维护历史。</summary>
        private void RefreshCanvasMarks()
        {
            if (canvasMarks == null) return;
            // 旧笔迹表达习惯；偏离后渐淡，不显示听话／反抗计数。
            float importance = Mathf.Clamp01(1 - session.State.DeviationCount * .45f);
            canvasMarks.color = new Color(.45f,.37f,.24f,.52f * importance);
            // 旧笔迹明确指向“画”，不会因词栏按发现顺序增长而误指第一行的盆栽。
            var painting = new ClueId("painting");
            canvasMarks.text = session.State.Knows(painting) ? Label(painting) + "  ← 这里。" : "";
        }
        private void RenderHistory()
        {
            RefreshCanvasMarks();
            var relations=session.Rules.Recipes.Where(x=>session.State.HasCompleted(x.Id))
                .Select(x=>string.Join(" ─ ",x.Inputs.Select(Label))+(x.ProducesClues.Count>0 ? " → "+string.Join("、",x.ProducesClues.Select(Label)) : "")).ToArray();
            // 两列都是同一份领域历史的视图，没有另外保存已建立关系。
            int split=completedTextSecondary!=null ? (relations.Length+1)/2 : relations.Length;
            completedText.text= quietStudio ? "" : "留下的联系\n"+string.Join("\n",relations.Take(split));
            if(completedTextSecondary!=null) completedTextSecondary.text=quietStudio ? "" : "\n"+string.Join("\n",relations.Skip(split));
        }
        /// <summary>先隐藏旧按钮再延迟销毁，避免同一帧残留可点击项。</summary>
        private static void ClearRows(Transform list) { foreach (Transform child in list) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
        /// <summary>右页灵感只显示已获得的领域记录，不可拖动也不会与纸面词条争夺层级。</summary>
        private void AddLogicRow(LogicDefinition logic)
        {
            var row = Instantiate(rowTemplate, logicList);
            row.name = "Logic_" + logic.Id;
            row.gameObject.SetActive(true);
            row.interactable = false;
            var text = row.GetComponentInChildren<Text>();
            text.text = "灵感 · " + logic.DisplayName;
            text.fontSize = 18;
            text.color = new Color(.43f, .30f, .14f);
            row.GetComponent<LayoutElement>().preferredHeight = 34;
            row.targetGraphic = null;
        }
        /// <summary>提示只归纳当前尚可调查的内容与尚未获得的灵感，不提前透露具体答案。</summary>
        private void RefreshHints()
        {
            if (explorationHint != null)
            {
                bool pending = session.Rules.Discoveries.Any(x => session.Rules.CanDiscover(session.State, x) && !session.State.Knows(x.Source));
                explorationHint.text = pending ? "目前场景中还有物品可探索观察。" : "眼前能观察的物品，已经记下了。";
            }
            if (logicHint != null)
            {
                bool pending = session.Rules.Logics.Any(x => !session.State.HasFact(x.UnlockFact) && x.RequiresClues.Any(session.State.Knows));
                logicHint.text = session.Rules.MaximumCombinationInputs == 2
                    ? "在右页放两个词语，再点‘合成’。"
                    : pending ? "还有灵感可观察回忆。" : "继续把词条放到右页，试着落下一笔。";
            }
        }
        /// <summary>从模板创建中文条目，并绑定本条碎片的选择回调。</summary>
        private void AddRow(Transform list, string name, string label, ClueCategory category, UnityEngine.Events.UnityAction select,ClueId id)
        {
            var row = Instantiate(rowTemplate, list);
            row.name = name;
            row.gameObject.SetActive(true);
            var text = row.GetComponentInChildren<Text>();
            text.text = label;
            // 同一碎片输入仍使用同一按钮；字的重量区分具体事物、旁注观察和抽象灵感。
            bool concept = category == ClueCategory.Concept;
            bool observation = category == ClueCategory.Observation;
            text.fontSize = quietStudio ? 22 : concept ? 26 : observation ? 17 : 21;
            text.color = quietStudio ? AtelierNotebookInk.For(id,category) : observation ? new Color(.40f, .38f, .32f) : new Color(.25f, .25f, .21f);
            row.GetComponent<LayoutElement>().preferredHeight = quietStudio ? 40 : concept ? 44 : observation ? 32 : 36;
            row.targetGraphic = text;
            if (quietStudio) row.transition = Selectable.Transition.None;
            row.onClick.AddListener(select);
            if (semanticCanvas != null) row.gameObject.AddComponent<NotebookClueDrag>().Configure(semanticCanvas, new ClueId(name.Substring(name.IndexOf('_')+1)), false);
        }
        /// <summary>刷新槽位与提交许可，只校验输入数量。</summary>
        private void RenderSelection()
        {
            if (formedWord != null) return;
            relationLine.enabled = false;
            selectionText.text = quietStudio ? "" : "让这些细节靠近一些"; selectionText.color = new Color(.40f, .38f, .33f);
            for (int i = 0; i < slotButtons.Length; i++)
            {
                slotButtons[i].gameObject.SetActive(semanticCanvas == null && i < session.Rules.MaximumCombinationInputs);
                slotButtons[i].GetComponentInChildren<Text>().text = selected[i].HasValue ? Label(selected[i].Value) : i == 2 ? "···" : "放下碎片";
            }
            interpretButton.interactable = SelectedClues.Count >= 2;
            var submit = interpretButton.GetComponentInChildren<Text>();
            // 合成字样始终为纯白；是否可提交由按钮状态表达。
            submit.color = Color.white;
        }
        /// <summary>纸页轻展开并呈现成功墨迹；正式纸面不自动合页，也不判断配方。</summary>
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
                float alpha = quietStudio ? 1 : formedWord != null ? .36f : 1;
                foreach (var column in columnCanvas) column.alpha = Mathf.MoveTowards(column.alpha, alpha, Time.unscaledDeltaTime * 2.5f);
            }
        }
        /// <summary>处理面板按键与成功后收起；输入框编辑时不抢占键盘。</summary>
        private void Update()
        {
            if (clearAfter > 0 && Time.unscaledTime >= clearAfter)
            { clearAfter = -1; ClearSelection(); RenderSelection(); }
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
                    feedbackText.text = quietStudio ? "" : "“" + formedWord + "”。重新看待一件事物。";
                }
            }
            if (closeAt > 0 && Time.unscaledTime >= closeAt) { closeAt = -1; SetOpen(false); ClearSelection(); RenderSelection(); }
            var keyboard = Keyboard.current; if (keyboard == null) return;
            var focused = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            // 编辑调试事实时不抢 N、Tab 和 Esc，保证输入体验。
            if (focused != null && focused.TryGetComponent<InputField>(out var input) && input.isFocused) return;
            if (ExternalInputBlocked) return;
            if (keyboard.nKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame) Toggle();
            if (EscapeClosesNotebook && keyboard.escapeKey.wasPressedThisFrame) SetOpen(false);
        }
        /// <summary>解除会话订阅，避免重开或退出后的重复回调。</summary>
        private void OnDestroy() { if (session != null) session.EventRaised -= OnEvent; }
    }
}
