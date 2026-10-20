// 文件：AtelierNotebookLayoutPolish.cs
// 职责：把四阶段笔记左页恢复为四个等面积分区：物件、观察、概念、笔刷，并同步Prefab母版。
// 边界：只调整UI分区、标题与文本显示；不改材料集合、配方、拖动或提交规则。
using System;
using Game.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Game.Editor.ArtRebuild
{
    /// <summary>笔记四区只做布局与字段绑定；运行时仍由 NotebookView.Render 从会话读取材料。</summary>
    public static class AtelierNotebookLayoutPolish
    {
        private const string Main="Assets/_Game/Content/Scenes/AtelierPlayable.unity";

        /// <summary>显式整理当前可玩副本与笔记母版。</summary>
        [MenuItem("游戏/新四阶段流程/整理笔记四区")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出Play。");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("场景尚未保存，未整理笔记四区。");
            var scene=EditorSceneManager.OpenScene(Main);
            var flow=Object.FindAnyObjectByType<Game.Unity.ArtRebuild.AtelierFlowGameplay>();
            if(flow==null||flow.References.Notebook==null)throw new InvalidOperationException("可玩副本缺少笔记本。");
            var notebook=flow.References.Notebook;
            Apply(notebook);
            BindFields(notebook);
            EditorUtility.SetDirty(notebook);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("笔记四区场景保存失败。");
            // NotebookView在场景根而不是NotebookPanel母版里；四区布局以场景实例为准。
            AssetDatabase.SaveAssets();
            Debug.Log("[笔记四区] 已整理物件、观察、概念、笔刷四个等面积分区。");
        }

        /// <summary>供第四阶段生成器复用；只改当前打开的笔记本，不保存场景。</summary>
        public static void Apply(NotebookView notebook)
        {
            var paper=notebook.transform.Find("笔记纸面")??notebook.transform;
            var noun=paper.Find("名词材料")??throw new InvalidOperationException("缺少名词材料区。");
            var adjective=paper.Find("形容词材料")??throw new InvalidOperationException("缺少形容词材料区。");
            var other=paper.Find("其他材料")??throw new InvalidOperationException("缺少其他材料区。");
            var root=paper.Find("材料四区");
            if(root==null)
            {
                root=new GameObject("材料四区",typeof(RectTransform)).transform;
                root.SetParent(paper,false);
            }
            var rootRect=(RectTransform)root;
            rootRect.anchorMin=rootRect.anchorMax=new Vector2(.5f,.5f);
            rootRect.pivot=new Vector2(.5f,.5f);
            rootRect.anchoredPosition=new Vector2(-270,0);
            rootRect.sizeDelta=new Vector2(520,440);
            // 旧四阶段合区把这两列停用了；分区版重新启用。
            adjective.gameObject.SetActive(true);other.gameObject.SetActive(true);
            // 笔刷区从普通GameObject建立，不能把Prefab子物体复制到同一Prefab实例内，避免循环嵌套。
            // Prefab实例里不能改子物体父级；四个分区与标题保持为纸面同级，只改位置和尺寸。
            var brush=paper.Find("笔刷材料");
            if(brush==null)
            {
                var brushGo=new GameObject("笔刷材料",typeof(RectTransform),typeof(VerticalLayoutGroup));
                brushGo.transform.SetParent(paper,false);brush=brushGo.transform;
            }
            brush.gameObject.SetActive(true);
            LayoutScroll(noun,new Vector2(-382.5f,80));LayoutScroll(adjective,new Vector2(-157.5f,80));
            LayoutScroll(other,new Vector2(-382.5f,-125));LayoutScroll(brush,new Vector2(-157.5f,-125));
            LayoutBrush(brush);
            // 旧“名词标题”是Prefab子物体，兄弟顺序和显示状态无法在实例里稳定改；用它的字体新建普通标题并停用旧件。
            var legacyHeading=paper.Find("旧物件标题")??paper.Find("名词标题");
            if(legacyHeading==null)
            {
                var candidate=paper.Find("物件标题");
                if(candidate!=null&&candidate.GetSiblingIndex()<3)legacyHeading=candidate;
            }
            var headingTemplate=legacyHeading??paper.Find("物件标题");
            if(headingTemplate==null)throw new InvalidOperationException("缺少物件标题。");
            if(legacyHeading!=null){legacyHeading.name="旧物件标题";legacyHeading.gameObject.SetActive(false);}
            var nounHeading=EnsureHeading(paper,headingTemplate,"物件标题","物件",new Vector2(-382.5f,200));
            EnsureHeading(paper,nounHeading,"观察标题","观察",new Vector2(-157.5f,200));
            EnsureHeading(paper,nounHeading,"概念标题","概念",new Vector2(-382.5f,-20));
            EnsureHeading(paper,nounHeading,"笔刷标题","笔刷",new Vector2(-157.5f,-20));
            var note=paper.Find("结果字幕")?.GetComponent<Text>();
            if(note==null)
            {
                var go=new GameObject("结果字幕",typeof(RectTransform),typeof(Text));
                go.transform.SetParent(paper,false);note=go.GetComponent<Text>();
                var rect=(RectTransform)go.transform;
                rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);
                rect.anchoredPosition=new Vector2(258,-5);rect.sizeDelta=new Vector2(440,70);
                note.font=nounHeading.GetComponent<Text>().font;note.fontSize=20;
                note.color=new Color(.30f,.25f,.16f,1);note.alignment=TextAnchor.MiddleCenter;
                note.horizontalOverflow=HorizontalWrapMode.Wrap;note.verticalOverflow=VerticalWrapMode.Overflow;note.raycastTarget=false;
            }
            note.text="";note.gameObject.SetActive(false);note.transform.SetAsLastSibling();
            notebook.ConfigureResultNote(note);
            var nounContent=noun.Find("视窗/词条内容");
            var adjectiveContent=adjective.Find("视窗/词条内容");
            var otherContent=other.Find("视窗/词条内容");
            if(nounContent==null||adjectiveContent==null||otherContent==null)
                throw new InvalidOperationException("材料区缺少词条内容。");
            notebook.ConfigureCategorizedLayout(nounContent,adjectiveContent,otherContent,brush,root);
            EditorUtility.SetDirty(notebook);
        }

        /// <summary>场景实例重新应用母版后按名称重绑四区字段。</summary>
        private static void BindFields(NotebookView notebook)
        {
            var paper=notebook.transform.Find("笔记纸面")??notebook.transform;
            var root=paper.Find("材料四区");
            if(root==null)throw new InvalidOperationException("缺少材料四区。");
            var noun=paper.Find("名词材料");var adjective=paper.Find("形容词材料");var other=paper.Find("其他材料");var brush=paper.Find("笔刷材料");
            var note=paper.Find("结果字幕")?.GetComponent<Text>();if(note!=null)notebook.ConfigureResultNote(note);
            notebook.ConfigureCategorizedLayout(noun.Find("视窗/词条内容"),adjective.Find("视窗/词条内容"),other.Find("视窗/词条内容"),brush,root);
        }

        private static void LayoutScroll(Transform scroll,Vector2 position)
        {
            var rect=(RectTransform)scroll;
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=position;rect.sizeDelta=new Vector2(215,180);
            scroll.gameObject.SetActive(true);
        }

        private static void LayoutBrush(Transform brush)
        {
            var layout=brush.GetComponent<VerticalLayoutGroup>();
            if(layout==null)layout=brush.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing=2;layout.padding=new RectOffset(4,4,4,4);layout.childAlignment=TextAnchor.UpperCenter;
            layout.childControlWidth=true;layout.childControlHeight=false;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        }

        private static Transform EnsureHeading(Transform root,Transform template,string name,string text,Vector2 position)
        {
            var node=root.Find(name);
            if(node==null)
            {
                var go=new GameObject(name,typeof(RectTransform),typeof(Text));
                go.transform.SetParent(root,false);node=go.transform;
                var source=template.GetComponent<Text>();
                var copy=node.GetComponent<Text>();
                copy.font=source.font;copy.fontSize=20;copy.color=new Color(.30f,.25f,.18f,1);
                copy.alignment=TextAnchor.MiddleLeft;copy.raycastTarget=false;
                copy.horizontalOverflow=HorizontalWrapMode.Overflow;copy.verticalOverflow=VerticalWrapMode.Overflow;
            }
            LayoutHeading(node,name,text,position);
            return node;
        }

        private static void LayoutHeading(Transform heading,string name,string text,Vector2 position)
        {
            heading.name=name;
            var rect=(RectTransform)heading;
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=position;rect.sizeDelta=new Vector2(215,26);
            var label=heading.GetComponent<Text>();
            label.text=text;label.fontSize=20;label.alignment=TextAnchor.MiddleLeft;label.raycastTarget=false;
            label.color=new Color(.30f,.25f,.18f,1);
            heading.gameObject.SetActive(true);
            // 旧四阶段合区曾把分类标题用CanvasGroup隐藏；恢复四区时必须显式恢复透明度。
            var group=heading.GetComponent<CanvasGroup>();
            if(group!=null){group.alpha=1;group.interactable=false;group.blocksRaycasts=false;}
            // 新标题是普通GameObject，应放在材料底图之后；旧Prefab标题的顺序不可在实例里改，但它的位置在材料区上方。
            heading.SetAsLastSibling();
        }
    }
}
