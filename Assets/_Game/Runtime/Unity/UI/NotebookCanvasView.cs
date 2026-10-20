// =============================================================================
// 文件：NotebookCanvasView.cs
// 职责：管理有限词条的拖动、归还与提交；四阶段最多两个，成功立即替换为结果。
// 边界：位置和活动卡片都是临时 UI 状态；近邻反馈只看摆放，提交以后才由领域判定意义。
// =============================================================================
using System.Collections.Generic;
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Unity
{
    /// <summary>有限语义画布；不提供节点图、连线编辑、自动布局或另一套规则。</summary>
    public sealed class NotebookCanvasView : MonoBehaviour
    {
        [SerializeField] private RectTransform workspace;
        [SerializeField] private Button cardTemplate, action;
        [SerializeField] private Image[] lines;
        [SerializeField] private Text invitation;
        [SerializeField] private bool blindComposition;
        /// <summary>叙事纸面不预告正确答案；历史灰盒仍可保留旧配方提示。</summary>
        public void ConfigureBlindComposition(bool enabled) { blindComposition = enabled; }
        [SerializeField] private bool quietStudio;
        [SerializeField] private bool clickPlacement;
        private ClueId? picked;
        /// <summary>独立玩法允许选词后点击纸面放置；旧场景默认保留原输入。</summary>
        public bool ClickPlacement => clickPlacement;
        /// <summary>启用替代拖动输入，只影响临时纸面，不修改收藏或配方。</summary>
        public void ConfigureClickPlacement(bool enabled) { clickPlacement = enabled; }
        /// <summary>保留拖动与提交，清空装饰性提示并固定字号、提交位置。</summary>
        public void ConfigureQuietStudio(bool enabled) { quietStudio = enabled; }
        private readonly Dictionary<ClueId, RectTransform> cards = new Dictionary<ClueId, RectTransform>();
        private GameSession session;
        private NotebookView notebook;
        private ClueId[] candidateInputs = new ClueId[0];
        private readonly List<GameObject> resultPreviews=new List<GameObject>();
        private float resultAge=-1;
        /// <summary>帮助标注使用真实组合区域和提交按钮。</summary>
        public RectTransform Workspace => workspace;
        public Button SubmitButton => action;
        /// <summary>仅用于视觉检查，不计入两个参与词。</summary>
        public bool HasResultPreview => resultPreviews.Count>0;
        private float successUntil;
        private float failureUntil;
        /// <summary>失败只在纸面上短暂回应，不预告正确配方，也不混入旁白。</summary>
        public void ShowFailure()
        { invitation.text = "这两点还没形成联系。"; failureUntil = Time.unscaledTime + 2.2f; }
        private Image[] inkMotes;
        private Vector2 moteOrigin;
        /// <summary>正式画室提交全部活动词条；历史灰盒保留近邻兼容。</summary>
        public IReadOnlyList<ClueId> CombinationInput => quietStudio ? cards.Keys.ToArray() : candidateInputs.Length > 0 ? candidateInputs : cards.Keys.ToArray();
        /// <summary>活动卡片的数量，不是已知线索数量。</summary>
        public int ActiveCardCount => cards.Count;
        /// <summary>容量来自当前内容规则，不能靠多次点击或拖动放入第三张卡。</summary>
        public int Capacity => session?.Rules.MaximumCombinationInputs ?? 2;
        /// <summary>配置纸面技术对象，保留所有已有线索和世界状态。</summary>
        public void Configure(RectTransform area, Button template, Button submit, Image[] threads, Text prompt)
        {
            workspace = area;
            cardTemplate = template;
            action = submit;
            lines = threads;
            invitation = prompt;
        }
        /// <summary>换会话清除临时摆放，不复用上一局的解释提示。</summary>
        public void Bind(GameSession game, NotebookView view)
        {
            session = game; notebook = view;
            if(game.Rules.DeclaredFacts.Contains(AtelierFlowRules.Enabled))clickPlacement=true;
            if (clickPlacement)
            {
                workspace.GetComponent<Image>().raycastTarget = true;
                var placement = workspace.GetComponent<NotebookPaperPlacement>();
                if (placement == null) placement = workspace.gameObject.AddComponent<NotebookPaperPlacement>();
                placement.Owner = this;
            }
            if (inkMotes == null)
            {
                inkMotes = new Image[4];
                for (int i=0;i<inkMotes.Length;i++)
                {
                    var dot=new GameObject("InkMote"+i,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                    dot.transform.SetParent(workspace,false); dot.raycastTarget=false;
                    dot.rectTransform.sizeDelta=new Vector2(3,3); dot.gameObject.SetActive(false); inkMotes[i]=dot;
                }
            }
            Clear();
            action.onClick.RemoveAllListeners(); action.onClick.AddListener(notebook.InterpretSelected);
        }
        /// <summary>历史灰盒的点击选择入口；正式画室从拖动入口添加词条。</summary>
        public void Toggle(ClueId clue)
        {
            if (cards.ContainsKey(clue)) Remove(clue);
            else Add(clue, new Vector2(cards.Count == 0 ? -90 : cards.Count == 1 ? 90 : 0, cards.Count == 2 ? -65 : 30));
        }
        /// <summary>从边栏开始拖动，创建可用词条；已消费或达到容量时拒绝新增。</summary>
        public RectTransform Add(ClueId clue, Vector2 localPosition)
        {
            if (!session.State.CanUseClue(clue)) return null;
            ClearResultPreview();
            notebook.PrepareCanvasInput();
            if (cards.TryGetValue(clue, out var existing)) return existing;
            if (cards.Count >= Capacity) return null;
            var definition = session.Rules.Clues.Single(x => x.Id == clue);
            var button = Instantiate(cardTemplate, workspace);
            button.name = "CanvasClue_" + clue.Value; button.gameObject.SetActive(true);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            // 词条卡片跟随 Prefab 模板尺寸，方便美术在 Inspector 中直接调节。
            rect.sizeDelta = ((RectTransform)cardTemplate.transform).sizeDelta;
            rect.anchoredPosition = localPosition;
            var label = button.GetComponentInChildren<Text>(); label.text = definition.DisplayName;
            label.fontSize = quietStudio ? 22 : definition.Category == ClueCategory.Concept ? 28 : definition.Category == ClueCategory.Observation ? 20 : 23;
            if (quietStudio) label.color = AtelierNotebookInk.For(clue,definition.Category);
            var image = button.GetComponent<Image>(); image.color = new Color(.87f,.82f,.66f,.12f);
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(() =>
            {
                Remove(clue); notebook.NotifyCanvasChanged();
            });
            button.gameObject.AddComponent<NotebookClueDrag>().Configure(this, clue, true);
            cards.Add(clue, rect); Refresh(); return rect;
        }
        /// <summary>把屏幕坐标换到纸面；只改变 UI，不移动房间物体。</summary>
        public Vector2 LocalPoint(Vector2 screen, Camera camera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(workspace, screen, camera, out var point);
            return point;
        }
        /// <summary>只选择已经亲自获得的词；再次选择覆盖未放下的临时词。</summary>
        public void Pick(ClueId clue)
        {
            if (!clickPlacement || !session.State.CanUseClue(clue)) return;
            ClearResultPreview();
            picked = clue;
        }
        /// <summary>点击纸面空处放下所选词；不提交、不预判配方。</summary>
        public void PlacePicked(Vector2 screen, Camera camera)
        {
            if (!clickPlacement || !picked.HasValue) return;
            var clue = picked.Value;
            var card = Add(clue, LocalPoint(screen, camera));
            if (card != null) { EndDrag(clue, screen, camera); picked = null; }
        }
        /// <summary>移动活动词条并更新淡线；正式画室不查询配方提示。</summary>
        public void Move(ClueId clue, Vector2 position)
        { if (cards.TryGetValue(clue, out var card)) { notebook.PrepareCanvasInput(); card.anchoredPosition = position; Refresh(); } }
        /// <summary>拖到纸面外即归还边栏；已知线索不会被消费。</summary>
        public void EndDrag(ClueId clue, Vector2 screen, Camera camera)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(workspace, screen, camera)) Remove(clue);
            else if (cards.TryGetValue(clue, out var card))
            {
                var halfCard = card.rect.size * .5f;
                var bound = workspace.rect.size * .5f - halfCard;
                card.anchoredPosition = new Vector2(Mathf.Clamp(card.anchoredPosition.x,-bound.x,bound.x),Mathf.Clamp(card.anchoredPosition.y,-bound.y,bound.y));
                Refresh();
            }
            notebook.NotifyCanvasChanged();
        }
        /// <summary>归还一个输入卡片；领域收藏保持原样。</summary>
        public void Remove(ClueId clue)
        {
            if (!cards.TryGetValue(clue, out var card)) return;
            cards.Remove(clue); card.gameObject.SetActive(false); Destroy(card.gameObject); Refresh();
        }
        /// <summary>清空纸面、淡线和临时提示；用于重开、形成概念或解释后收起。</summary>
        public void Clear()
        {
            ClearResultPreview();
            foreach (var card in cards.Values) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            if (inkMotes != null) foreach(var mote in inkMotes) mote.gameObject.SetActive(false);
            cards.Clear(); picked = null; candidateInputs = new ClueId[0]; successUntil = 0; failureUntil = 0;
            foreach (var line in lines) line.enabled = false;
            action.gameObject.SetActive(false); invitation.text = quietStudio ? "" : "把记下的细节拖到这里";
        }
        /// <summary>同一次成功回调里撤去全部旧卡，再放入领域实际产出的蓝色词条。</summary>
        /// <param name="produced">命令已确认的结果；不从UI猜测配方。</param>
        public void ShowResult(IReadOnlyList<ClueId> produced,string worldResult="解释已经记下。")
        {
            Clear();
            int count=Mathf.Max(1,produced.Count);
            for(int index=0;index<count;index++)
            {
                var card=Instantiate(cardTemplate,workspace);
                card.name="结果预览";card.gameObject.SetActive(true);card.interactable=false;
                foreach(var graphic in card.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
                var rect=(RectTransform)card.transform;
                rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
                rect.anchoredPosition=new Vector2((index-(count-1)*.5f)*150,20);
                var label=card.GetComponentInChildren<Text>();
                if(produced.Count>0)
                {
                    var definition=session.Rules.Clues.Single(x=>x.Id==produced[index]);
                    label.text=definition.DisplayName;label.color=AtelierNotebookInk.For(definition.Id,definition.Category);
                }
                else {label.text=worldResult;label.color=new Color(.25f,.30f,.26f);}
                resultPreviews.Add(card.gameObject);
            }
            resultAge=0;
            notebook.NotifyCanvasChanged();
        }
        /// <summary>只撤本次结果图形，不清除玩家下一轮的活动词。</summary>
        public void ClearResultPreview()
        {
            foreach(var preview in resultPreviews)if(preview!=null){preview.SetActive(false);Destroy(preview);}
            resultPreviews.Clear();resultAge=-1;
        }
        private void OnDisable() { picked = null; ClearResultPreview(); }
        /// <summary>成功时淡线短暂变亮；不把闪光当作配方完成的第二份状态。</summary>
        public void ShowSuccess()
        {
            failureUntil = 0; invitation.text = "";
            successUntil = Time.unscaledTime + .85f;
            moteOrigin=Vector2.zero;
            foreach(var card in cards.Values) moteOrigin+=card.anchoredPosition/Mathf.Max(1,cards.Count);
            foreach(var mote in inkMotes) mote.gameObject.SetActive(true);
            foreach (var line in lines) if (line.enabled) line.color = new Color(.62f,.46f,.17f,.8f);
            action.gameObject.SetActive(false);
        }
        private void Refresh()
        {
            if (session == null) return;
            candidateInputs = new ClueId[0]; SemanticRecipe recipe = null;
            var all = cards.Keys.ToArray();
            // 正式画室全部提交；只有历史灰盒使用旧的近邻选择逻辑。
            if (quietStudio) candidateInputs = all;
            else if (all.Length >= 2)
            {
                var groups = new List<ClueId[]>();
                bool threeTogether=all.Length==3 && all.All(a=>all.All(b=>Vector2.Distance(cards[a].anchoredPosition,cards[b].anchoredPosition)<=205));
                if(threeTogether) groups.Add(all);
                else for (int i=0;i<all.Length;i++) for(int j=i+1;j<all.Length;j++) groups.Add(new[] {all[i],all[j]});
                foreach(var group in groups)
                {
                    if (group.Any(a => group.Any(b => Vector2.Distance(cards[a].anchoredPosition,cards[b].anchoredPosition)>205))) continue;
                    if (blindComposition) { candidateInputs = group; break; }
                    recipe = session.PreviewCombination(group);
                    if (recipe != null) { candidateInputs = group; break; }
                }
            }
            foreach(var line in lines) line.enabled = false;
            bool canTry = quietStudio || blindComposition ? candidateInputs.Length >= 2 : recipe != null;
            // 语义纸面拥有提交许可；不能继承旧槽位在无输入时留下的禁用状态。
            action.interactable = canTry;
            action.gameObject.SetActive(canTry);
            invitation.text = !quietStudio && cards.Count == 0 ? "把记下的细节拖到这里" : "";
            if (!canTry) return;
            // 解释按钮的位置完全由 Prefab 决定；运行时只更新可用性和文案，不能跟随词条或工作区改写位置。
            // 按输入的语义重量提供动词，不查询这组词是否存在答案。
            bool containsObject = candidateInputs.Any(c => session.Rules.Clues.Single(x => x.Id == c).Category == ClueCategory.Object);
            var actionLabel = action.GetComponentInChildren<Text>();
            actionLabel.color = Color.white;
            actionLabel.text = Capacity == 2 ? "合成" : quietStudio ? "解释" : blindComposition ? (containsObject ? "解释" : "联想")
                : recipe.ProducesClues.Count > 0 ? "联想" : "解释";
            for(int i=0;i<candidateInputs.Length-1;i++)
            {
                Vector2 a=cards[candidateInputs[i]].anchoredPosition,b=cards[candidateInputs[i+1]].anchoredPosition;
                var line=lines[i]; line.enabled=true; line.color=new Color(.58f,.49f,.27f,blindComposition ? .14f : .28f);
                line.rectTransform.anchoredPosition=(a+b)*.5f;
                line.rectTransform.sizeDelta=new Vector2(Vector2.Distance(a,b),1.3f);
                line.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
            }
            // 把线留在文字下方，不遮住词条的拾取范围。
            foreach(var line in lines) line.transform.SetAsFirstSibling();
        }
        private void Update()
        {
            if(resultAge>=0 && notebook.IsOpen && !notebook.PreviewCovered && Time.timeScale>0)
            {
                resultAge+=Time.unscaledDeltaTime;
                float alpha=1-Mathf.Clamp01((resultAge-1.5f)/.2f);
                foreach(var preview in resultPreviews)
                    foreach(var graphic in preview.GetComponentsInChildren<Graphic>())
                    {var color=graphic.color;color.a=alpha;graphic.color=color;}
                if(resultAge>=1.7f)ClearResultPreview();
            }
            if (failureUntil > 0 && Time.unscaledTime >= failureUntil) { failureUntil = 0; Refresh(); }
            if (successUntil <= 0) return;
            float remaining=Mathf.Clamp01((successUntil-Time.unscaledTime)/.85f);
            for(int i=0;i<inkMotes.Length;i++)
            {
                inkMotes[i].rectTransform.anchoredPosition=moteOrigin+new Vector2((i-1.5f)*13,(1-remaining)*27+i*4);
                inkMotes[i].color=new Color(.68f,.52f,.20f,Mathf.Sin(remaining*Mathf.PI)*.65f);
            }
            if (Time.unscaledTime >= successUntil)
            { foreach(var mote in inkMotes) mote.gameObject.SetActive(false); successUntil=0; Refresh(); }
        }
    }
}
