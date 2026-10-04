// =============================================================================
// 文件：NotebookReconstruction.cs
// 职责：把三个分类与组合槽整理为艺术家的暖白手稿、铅笔旁注和淡墨联系。
// 边界：只调整笔记本纸面美术；不定义配方，不改玩家 HUD、发现通知或按钮会话绑定。
// =============================================================================
using System.IO;
using Game.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Editor.VisualSprint
{
    /// <summary>第五阶段笔记本的增量美术工具，保留三列、三个槽和已有灵感／合页演出。</summary>
    public static class NotebookReconstruction
    {
        private static readonly Color Ink = new Color(.25f,.25f,.22f);
        private static readonly Color Pencil = new Color(.41f,.39f,.32f);
        private static readonly Color PaleLine = new Color(.34f,.31f,.24f,.18f);

        /// <summary>应用暖白纸纤维、手稿层次与轻笔迹；不重建或覆盖主场景。</summary>
        public static void Apply()
        {
            var room=PresentationArtPass.Room();
            var view=room.GetComponentInChildren<NotebookView>(true);
            var shade=view.transform.Find("NotebookPanel");
            var body=(RectTransform)shade.Find("Notebook");
            Position(body,new Vector2(.16f,.13f),new Vector2(.84f,.89f));
            shade.GetComponent<Image>().color=new Color(.025f,.023f,.02f,.84f);
            var page=body.GetComponent<Image>(); page.sprite=ManuscriptPaper(); page.color=Color.white;
            var backing=Rect("BookBacking",shade,new Vector2(.153f,.12f),new Vector2(.848f,.898f));
            Image(backing,new Color(.13f,.16f,.14f,.98f)); backing.SetAsFirstSibling();
            var shadow=Rect("PaperShadow",shade,new Vector2(.165f,.116f),new Vector2(.855f,.893f));
            Image(shadow,new Color(0,0,0,.16f)); shadow.SetSiblingIndex(1);
            if(body.Find("PaperCrease")!=null) body.Find("PaperCrease").gameObject.SetActive(false);
            foreach(var text in body.GetComponentsInChildren<Text>(true))
            {
                text.font=AssetDatabase.LoadAssetAtPath<Font>(DemoSceneBuilder.ChineseFontPath);
                text.color=Ink;
                text.raycastTarget=false;
            }
            var title=body.Find("Title").GetComponent<Text>(); title.text="未完成的工作笔记"; title.fontSize=25; title.fontStyle=FontStyle.Normal;
            Position(title.rectTransform,new Vector2(.07f,.885f),new Vector2(.80f,.97f));
            var caption=Label("ManuscriptCaption",body,"光、枝叶，以及尚未说清的事。",new Vector2(.071f,.849f),new Vector2(.78f,.889f),13); caption.color=Pencil;
            Label("PageMark",body,"手稿 · 01",new Vector2(.80f,.897f),new Vector2(.94f,.951f),13).color=Pencil;
            SetHeading(body,"ObjectsHeading","看见的事物",.075f,.32f,16);
            SetHeading(body,"ObservationsHeading","记下的细节",.39f,.645f,15);
            SetHeading(body,"ConceptsHeading","想出的意义",.715f,.95f,17);
            SetColumn(body,"Objects",new Vector2(.07f,.555f),new Vector2(.325f,.785f));
            SetColumn(body,"Observations",new Vector2(.39f,.555f),new Vector2(.647f,.785f));
            SetColumn(body,"Concepts",new Vector2(.715f,.555f),new Vector2(.95f,.785f));
            var template=view.transform.Find("RowTemplate").GetComponent<Button>(); StyleButton(template,false);
            template.GetComponentInChildren<Text>(true).alignment=TextAnchor.MiddleLeft;
            foreach(var button in body.GetComponentsInChildren<Button>(true)) StyleButton(button,false);
            // 槽仍是同一套输入，只把完整矩形卡片变成纸上的三条不规则铅笔落点。
            for(int i=0;i<3;i++)
            {
                var slot=body.Find("Slot"+i).GetComponent<Button>();
                Position((RectTransform)slot.transform,new Vector2(.055f+i/3f,.34f),new Vector2(.315f+i/3f,.426f));
                var text=slot.GetComponentInChildren<Text>(true); text.fontSize=21; text.fontStyle=FontStyle.Normal;
                var underline=Rect("PencilUnderline",slot.transform,new Vector2(.05f,.12f),new Vector2(.95f,.129f)); Image(underline,new Color(.39f,.34f,.23f,.32f));
                underline.localRotation=Quaternion.Euler(0,0,i==0 ? -1.2f : i==1 ? .7f : -.4f);
            }
            var selection=body.Find("Selection").GetComponent<Text>(); selection.fontSize=14; selection.color=Pencil; selection.alignment=TextAnchor.MiddleCenter;
            Position(selection.rectTransform,new Vector2(.07f,.433f),new Vector2(.93f,.478f));
            var insight=body.Find("InsightWord").GetComponent<Text>(); insight.fontSize=39; insight.color=new Color(.27f,.25f,.19f);
            Position(insight.rectTransform,new Vector2(.25f,.443f),new Vector2(.75f,.555f));
            var history=body.Find("CompletedRecipes").GetComponent<Text>(); history.fontSize=15; history.lineSpacing=1.05f; history.color=Pencil;
            Position(history.rectTransform,new Vector2(.07f,.075f),new Vector2(.675f,.286f));
            var interpret=body.Find("Interpret").GetComponent<Button>(); StyleButton(interpret,true);
            Position((RectTransform)interpret.transform,new Vector2(.725f,.137f),new Vector2(.92f,.245f));
            interpret.GetComponentInChildren<Text>(true).text="落下这一笔";
            interpret.GetComponentInChildren<Text>(true).fontSize=22;
            var submitLine=Rect("PencilUnderline",interpret.transform,new Vector2(.12f,.11f),new Vector2(.88f,.12f)); Image(submitLine,new Color(.45f,.35f,.16f,.50f));
            var close=body.Find("CloseNotebook").GetComponent<Button>();
            Position((RectTransform)close.transform,new Vector2(.897f,.93f),new Vector2(.962f,.972f));
            close.GetComponentInChildren<Text>(true).text="合上"; close.GetComponentInChildren<Text>(true).fontSize=13;
            var feedback=body.Find("Feedback").GetComponent<Text>(); feedback.fontSize=14; feedback.color=Pencil;
            Position(feedback.rectTransform,new Vector2(.07f,.015f),new Vector2(.94f,.07f));
            var relation=body.Find("InkRelation").GetComponent<Image>(); relation.color=new Color(.44f,.33f,.15f,.76f);
            AddManuscriptMarks(body);
            PresentationArtPass.Save(room);
            Debug.Log("[美术] 笔记本已整理为暖白手稿与淡墨联系，保留原输入与发现绑定。");
        }

        private static void SetHeading(Transform body,string name,string value,float left,float right,int size)
        {
            var text=body.Find(name).GetComponent<Text>(); text.text=value; text.fontSize=size; text.color=Pencil;
            Position(text.rectTransform,new Vector2(left,.788f),new Vector2(right,.83f));
        }
        private static void SetColumn(Transform body,string name,Vector2 min,Vector2 max)
        {
            var rect=(RectTransform)body.Find(name); Position(rect,min,max);
            var layout=rect.GetComponent<VerticalLayoutGroup>(); layout.spacing=3; layout.childControlHeight=true; layout.childControlWidth=true; layout.childForceExpandHeight=false;
            if(rect.GetComponent<CanvasGroup>()==null) rect.gameObject.AddComponent<CanvasGroup>();
        }
        private static void StyleButton(Button button,bool emphasis)
        {
            // 保留按钮的点击范围；视觉上不再出现现代应用的色块、圆卡片或绿色勾选。
            var image=button.GetComponent<Image>(); image.color=Color.clear;
            var colors=button.colors; colors.normalColor=Color.white; colors.highlightedColor=new Color(1,.96f,.82f); colors.pressedColor=new Color(.77f,.70f,.57f); colors.selectedColor=Color.white; colors.disabledColor=new Color(.7f,.7f,.7f,.4f); button.colors=colors;
            var text=button.GetComponentInChildren<Text>(true); text.color=emphasis ? new Color(.40f,.29f,.12f) : Ink; text.fontStyle=FontStyle.Normal;
            var old=button.transform.Find("PencilUnderline"); if(old!=null) old.GetComponent<Image>().color=PaleLine;
        }

        private static void AddManuscriptMarks(RectTransform body)
        {
            var marks=Rect("ManuscriptMarks",body,Vector2.zero,Vector2.one);
            // 原生 UI 笔迹只作装饰，不接收射线，不建立新的节点或谜题系统。
            var rule=Rect("MarginRule",marks,new Vector2(.048f,.055f),new Vector2(.049f,.924f)); Image(rule,new Color(.48f,.25f,.19f,.12f));
            var header=Rect("HeaderRule",marks,new Vector2(.07f,.843f),new Vector2(.94f,.8445f)); Image(header,new Color(.36f,.31f,.22f,.17f));
            var quote=Label("MarginNote",marks,"有些联系，\n要慢慢看见。",new Vector2(.74f,.272f),new Vector2(.94f,.325f),12); quote.color=new Color(.49f,.44f,.34f,.72f);
            marks.SetAsFirstSibling();
            Canvas.ForceUpdateCanvases();
            // 复用书页的植物速写语言，放在页眉一角；所有点都以纸面尺寸归一化。
            Segment(marks,"Stem",new Vector2(.867f,.486f),new Vector2(.880f,.561f),.9f);
            for(int leaf=0;leaf<4;leaf++)
            {
                float y=.505f+leaf*.012f; float side=leaf%2==0 ? -1 : 1;
                var a=new Vector2(.871f,y); var b=new Vector2(.871f+side*.023f,y+.019f); var c=new Vector2(.871f+side*.024f,y+.001f);
                Segment(marks,"LeafA"+leaf,a,b,.7f); Segment(marks,"LeafB"+leaf,b,c,.7f); Segment(marks,"LeafC"+leaf,c,a,.7f);
            }
        }
        private static void Segment(RectTransform parent,string name,Vector2 a,Vector2 b,float width)
        {
            var rect=Rect(name,parent,(a+b)*.5f,(a+b)*.5f); Vector2 delta=Vector2.Scale(b-a,parent.rect.size);
            rect.sizeDelta=new Vector2(delta.magnitude,width); rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            Image(rect,new Color(.34f,.34f,.25f,.24f));
        }

        private static Sprite ManuscriptPaper()
        {
            const string folder="Assets/_Game/Content/Art/UI/Notebook/"; const string path=folder+"ManuscriptPaper.png";
            var previous=AssetDatabase.LoadAssetAtPath<Sprite>(path); if(previous!=null) return previous;
            Directory.CreateDirectory(folder); const int width=1024,height=768;
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false); var pixels=new Color[width*height];
            for(int y=0;y<height;y++) for(int x=0;x<width;x++)
            {
                float u=x/(float)(width-1),v=y/(float)(height-1);
                float grain=Mathf.PerlinNoise(x*.23f,y*.23f)*.021f;
                float fiber=Mathf.PerlinNoise(x*.041f,y*1.7f)*.009f;
                float edge=Mathf.Min(Mathf.Min(u,1-u),Mathf.Min(v,1-v));
                float rough=.002f+Mathf.PerlinNoise(x*.04f,y*.035f)*.003f;
                float alpha=Mathf.Clamp01((edge-rough)*380);
                float age=(1-Mathf.Clamp01(edge*20))*.065f;
                float crease=Mathf.Exp(-Mathf.Abs(u-.664f)*220)*.014f;
                pixels[y*width+x]=new Color(.965f+grain-age-crease,.95f+fiber-age-crease,.891f+grain-age*.8f,alpha);
            }
            texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.maxTextureSize=1024; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var found=parent.Find(name); var rect=found!=null ? (RectTransform)found : new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            if(found==null) rect.SetParent(parent,false); Position(rect,min,max); return rect;
        }
        private static void Position(RectTransform rect,Vector2 min,Vector2 max)
        { rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private static Image Image(RectTransform rect,Color color)
        {
            var image=rect.GetComponent<Image>(); if(image==null) image=rect.gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=false; return image;
        }
        private static Text Label(string name,Transform parent,string value,Vector2 min,Vector2 max,int size)
        {
            var rect=Rect(name,parent,min,max); var text=rect.GetComponent<Text>(); if(text==null) text=rect.gameObject.AddComponent<Text>();
            text.font=AssetDatabase.LoadAssetAtPath<Font>(DemoSceneBuilder.ChineseFontPath); text.fontSize=size; text.color=Ink; text.text=value; text.raycastTarget=false; return text;
        }
    }
}
