// =============================================================================
// 文件：NotebookCanvasView.cs
// 职责：管理纸面最多三个词条的拖动、归还、近邻淡线与联想／解释入口。
// 边界：位置和活动卡片都是临时 UI 状态；配方提示通过会话只读查询取得。
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
        private readonly Dictionary<ClueId, RectTransform> cards = new Dictionary<ClueId, RectTransform>();
        private GameSession session;
        private NotebookView notebook;
        private ClueId[] candidateInputs = new ClueId[0];
        private float successUntil;
        private Image[] inkMotes;
        private Vector2 moteOrigin;
        /// <summary>当前相邻配方的输入；尚无配方时提交全部词条，由领域返回轻失败。</summary>
        public IReadOnlyList<ClueId> CombinationInput => candidateInputs.Length > 0 ? candidateInputs : cards.Keys.ToArray();
        /// <summary>活动卡片的数量，不是已知线索数量。</summary>
        public int ActiveCardCount => cards.Count;
        /// <summary>配置纸面技术对象，保留所有已有线索和世界状态。</summary>
        public void Configure(RectTransform area, Button template, Button submit, Image[] threads, Text prompt)
        { workspace = area; cardTemplate = template; action = submit; lines = threads; invitation = prompt; }
        /// <summary>换会话清除临时摆放，不复用上一局的解释提示。</summary>
        public void Bind(GameSession game, NotebookView view)
        {
            session = game; notebook = view;
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
        /// <summary>点击词条也能放到画布；已经放入的词再次点击可移回。</summary>
        public void Toggle(ClueId clue)
        {
            if (cards.ContainsKey(clue)) Remove(clue);
            else Add(clue, new Vector2(cards.Count == 0 ? -90 : cards.Count == 1 ? 90 : 0, cards.Count == 2 ? -65 : 30));
        }
        /// <summary>从边栏开始拖动，创建纸面词条；满三个时保持原输入。</summary>
        public RectTransform Add(ClueId clue, Vector2 localPosition)
        {
            if (!session.State.Knows(clue)) return null;
            if (cards.TryGetValue(clue, out var existing)) return existing;
            if (cards.Count >= 3) return null;
            var definition = session.Rules.Clues.Single(x => x.Id == clue);
            var button = Instantiate(cardTemplate, workspace);
            button.name = "CanvasClue_" + clue.Value; button.gameObject.SetActive(true);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(142, 62); rect.anchoredPosition = localPosition;
            var label = button.GetComponentInChildren<Text>(); label.text = definition.DisplayName;
            label.fontSize = definition.Category == ClueCategory.Concept ? 28 : definition.Category == ClueCategory.Observation ? 20 : 23;
            var image = button.GetComponent<Image>(); image.color = new Color(.87f,.82f,.66f,.12f);
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => Remove(clue));
            button.gameObject.AddComponent<NotebookClueDrag>().Configure(this, clue, true);
            cards.Add(clue, rect); Refresh(); return rect;
        }
        /// <summary>把屏幕坐标换到纸面；只改变 UI，不移动房间物体。</summary>
        public Vector2 LocalPoint(Vector2 screen, Camera camera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(workspace, screen, camera, out var point);
            return point;
        }
        /// <summary>移动活动词条，重新请求近邻的只读提示。</summary>
        public void Move(ClueId clue, Vector2 position)
        { if (cards.TryGetValue(clue, out var card)) { card.anchoredPosition = position; Refresh(); } }
        /// <summary>拖到纸面外即归还边栏；已知线索不会被消费。</summary>
        public void EndDrag(ClueId clue, Vector2 screen, Camera camera)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(workspace, screen, camera)) Remove(clue);
            else if (cards.TryGetValue(clue, out var card))
            {
                var bound = workspace.rect.size * .5f - new Vector2(73, 32);
                card.anchoredPosition = new Vector2(Mathf.Clamp(card.anchoredPosition.x,-bound.x,bound.x),Mathf.Clamp(card.anchoredPosition.y,-bound.y,bound.y));
                Refresh();
            }
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
            foreach (var card in cards.Values) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            if (inkMotes != null) foreach(var mote in inkMotes) mote.gameObject.SetActive(false);
            cards.Clear(); candidateInputs = new ClueId[0]; successUntil = 0;
            foreach (var line in lines) line.enabled = false;
            action.gameObject.SetActive(false); invitation.text = "把记下的细节拖到这里";
        }
        /// <summary>成功时淡线短暂变亮；不把闪光当作配方完成的第二份状态。</summary>
        public void ShowSuccess()
        {
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
            // 先考虑三词精确组合；第三词远离时允许独立的两词近邻提示。
            if (all.Length >= 2)
            {
                var groups = new List<ClueId[]>(); if (all.Length == 3) groups.Add(all);
                for (int i=0;i<all.Length;i++) for(int j=i+1;j<all.Length;j++) groups.Add(new[] {all[i],all[j]});
                foreach(var group in groups)
                {
                    if (group.Any(a => group.Any(b => Vector2.Distance(cards[a].anchoredPosition,cards[b].anchoredPosition)>205))) continue;
                    recipe = session.PreviewCombination(group);
                    if (recipe != null) { candidateInputs = group; break; }
                }
            }
            foreach(var line in lines) line.enabled = false;
            action.gameObject.SetActive(recipe != null);
            invitation.text = cards.Count == 0 ? "把记下的细节拖到这里" : "";
            if (recipe == null) return;
            Vector2 center = Vector2.zero;
            foreach(var clue in candidateInputs) center += cards[clue].anchoredPosition / candidateInputs.Length;
            ((RectTransform)action.transform).anchoredPosition = center + new Vector2(0,-62);
            action.GetComponentInChildren<Text>().text = recipe.ProducesClues.Count > 0 ? "联想" : "解释";
            for(int i=0;i<candidateInputs.Length-1;i++)
            {
                Vector2 a=cards[candidateInputs[i]].anchoredPosition,b=cards[candidateInputs[i+1]].anchoredPosition;
                var line=lines[i]; line.enabled=true; line.color=new Color(.58f,.49f,.27f,.28f);
                line.rectTransform.anchoredPosition=(a+b)*.5f;
                line.rectTransform.sizeDelta=new Vector2(Vector2.Distance(a,b),1.3f);
                line.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
            }
            // 把线留在文字下方，不遮住词条的拾取范围。
            foreach(var line in lines) line.transform.SetAsFirstSibling();
        }
        private void Update()
        {
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
