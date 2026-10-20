// 文件：AtelierNotebookHelp.cs
// 职责：首次打开笔记时标注真实区域，并提供可重复打开的帮助入口。
// 边界：只保存当前帮助页和已读状态，不代放词、不提交配方、不修改世界事实。
using System.Collections;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Unity
{
    /// <summary>四步位置介绍；帮助与收藏共用现有笔记，不建立另一套材料状态。</summary>
    public sealed class AtelierNotebookHelp : MonoBehaviour
    {
        private NotebookView notebook;
        private GameSession session;
        private GameObject overlay;
        private Text title,body;
        private RectTransform highlight;
        private Button help,previous,next;
        private int page;
        private bool shown,skyHintShown;
        /// <summary>帮助覆盖时底层点击由全屏遮罩吸收。</summary>
        public bool IsVisible => overlay!=null&&overlay.activeSelf;
        private static readonly string[] Copy={
            "发现的词条记在左页。点选后，再点右页空处放下，也可以直接拖过去。下方可以查看当前词条的说明。",
            "右页一次最多放两个参与词。单击已经放下的词条，就能放回左页；拖出右页也可以。",
            "放好两个词条，再点合成。成功结果会记入材料或收藏，在这里短暂显示；没有形成联系时，可以换词再试。",
            "右侧的‘笔刷收藏’可以点击，查看已经得到的笔刷、图样和来源。紫色文字代表笔刷；想再看说明，点‘帮助’。"};
        /// <summary>绑定本局，只在新会话重置首开标记。</summary>
        public void Bind(NotebookView view,GameSession game)
        {
            if(notebook!=null)notebook.OpenChanged-=OnOpen;
            bool fresh=session==null||session.SessionEpoch!=game.SessionEpoch;
            notebook=view;session=game;
            if(fresh){shown=false;skyHintShown=false;}
            if(overlay==null)Build();
            notebook.OpenChanged+=OnOpen;overlay.SetActive(false);
        }
        private void OnOpen(bool open)
        {
            if(!open){overlay.SetActive(false);return;}
            if(!shown){shown=true;StartCoroutine(FirstOpen());}
            if(!skyHintShown&&session.State.Knows(StudioRules.Sky)&&!session.State.HasFact(AtelierFlowRules.SkyAsCanvas))
            {
                skyHintShown=true;
                if(notebook.DescriptionLabel!=null)notebook.DescriptionLabel.text="把‘天空’和‘画布’放在一起试试。";
            }
        }
        private IEnumerator FirstOpen(){yield return null;if(notebook.IsOpen)Show();}
        /// <summary>主动重看不清除已放的两个词。</summary>
        public void Show(){page=0;overlay.SetActive(true);overlay.transform.SetAsLastSibling();Render();}
        /// <summary>Esc只关闭当前帮助，之后由原笔记协议接手。</summary>
        public bool CloseFromBack(){if(!IsVisible)return false;overlay.SetActive(false);return true;}
        private void Build()
        {
            var parent=notebook.PanelObject.transform;
            help=Button(parent,"常驻帮助","帮助",new Vector2(.37f,.86f),new Vector2(.47f,.94f),Show);
            overlay=Rect(parent,"笔记帮助层",Vector2.zero,Vector2.one).gameObject;
            var shade=overlay.AddComponent<Image>();shade.color=new Color(.04f,.035f,.03f,.76f);shade.raycastTarget=true;
            var header=Rect(overlay.transform,"帮助标题底",new Vector2(.08f,.87f),new Vector2(.92f,.98f));
            header.gameObject.AddComponent<Image>().color=new Color(.16f,.11f,.075f,1);
            title=Label(header,"帮助标题","",new Vector2(.04f,.1f),new Vector2(.96f,.9f),27);
            highlight=Rect(overlay.transform,"当前区域标注",new Vector2(.12f,.35f),new Vector2(.80f,.80f));
            var tint=highlight.gameObject.AddComponent<Image>();tint.color=new Color(1,.9f,.7f,.08f);tint.raycastTarget=false;
            var outline=highlight.gameObject.AddComponent<Outline>();outline.effectColor=Color.white;outline.effectDistance=new Vector2(2,-2);
            var paper=Rect(overlay.transform,"帮助说明底",new Vector2(.08f,.03f),new Vector2(.92f,.31f));
            var backdrop=paper.gameObject.AddComponent<Image>();backdrop.color=new Color(.16f,.11f,.075f,.98f);
            body=Label(paper,"帮助正文","",new Vector2(.04f,.35f),new Vector2(.96f,.94f),23);
            previous=Button(paper,"上一步","上一步",new Vector2(.04f,.04f),new Vector2(.23f,.28f),()=>{page=Mathf.Max(0,page-1);Render();});
            next=Button(paper,"下一步","下一步",new Vector2(.28f,.04f),new Vector2(.48f,.28f),()=>{if(page==3)overlay.SetActive(false);else{page++;Render();}});
            Button(paper,"关闭帮助","关闭帮助",new Vector2(.70f,.04f),new Vector2(.96f,.28f),()=>overlay.SetActive(false));
            var entry=notebook.transform.Find("笔记纸面/藏品入口便签");
            if(entry!=null){var label=entry.GetComponentInChildren<Text>();if(label!=null){label.text="笔\n刷\n收\n藏";label.color=Color.white;label.rectTransform.sizeDelta=new Vector2(24,142);label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Overflow;}}
        }
        private void Render()
        {
            title.text="笔记使用帮助  "+(page+1)+" / 4";body.text=Copy[page];previous.interactable=page>0;
            next.GetComponentInChildren<Text>().text=page==3?"知道了":"下一步";
            Transform target=page==0?notebook.MaterialsRoot:page==1?notebook.CombinationCanvas.Workspace:
                page==2?notebook.CombinationCanvas.SubmitButton.transform:notebook.transform.Find("笔记纸面/藏品入口便签");
            if(target is RectTransform rect)
            {
                Canvas.ForceUpdateCanvases();var corners=new Vector3[4];rect.GetWorldCorners(corners);
                var a=overlay.transform.InverseTransformPoint(corners[0]);var b=overlay.transform.InverseTransformPoint(corners[2]);
                highlight.anchorMin=highlight.anchorMax=new Vector2(.5f,.5f);highlight.pivot=new Vector2(.5f,.5f);
                highlight.anchoredPosition=(a+b)*.5f;highlight.sizeDelta=new Vector2(b.x-a.x+12,b.y-a.y+12);
            }
        }
        private RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        private Text Label(Transform parent,string name,string text,Vector2 min,Vector2 max,int size)
        {
            var label=Rect(parent,name,min,max).gameObject.AddComponent<Text>();label.font=notebook.NotebookFont;
            label.text=text;label.fontSize=size;label.color=Color.white;label.alignment=TextAnchor.MiddleLeft;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Overflow;label.raycastTarget=false;return label;
        }
        private Button Button(Transform parent,string name,string text,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {
            var rect=Rect(parent,name,min,max);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.33f,.20f,.12f,1);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);
            var label=Label(rect,"文字",text,new Vector2(.03f,.02f),new Vector2(.97f,.98f),22);label.alignment=TextAnchor.MiddleCenter;return button;
        }
        private void OnDestroy(){if(notebook!=null)notebook.OpenChanged-=OnOpen;}
    }
}
