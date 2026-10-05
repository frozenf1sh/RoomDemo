// =============================================================================
// 文件：NarrativeSceneSetup.cs
// 职责：在现有美术房间增量接入画内风景、短旁白与普通完成结局。
// 边界：使用 Editor API 保存，不重建已有房间、Prefab 或人工视觉摆放。
// =============================================================================
using System;
using System.IO;
using System.Linq;
using Game.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Editor
{
    /// <summary>第七阶段的具体场景安装器；所有技术对象可重复配置。</summary>
    public static class NarrativeSceneSetup
    {
        /// <summary>接入已经通过领域测试的双路线，并保存必要表现引用。</summary>
        [MenuItem("游戏/叙事/接入画布与房间")]
        public static void Apply()
        {
            var root = PresentationArtPass.Room();
            var notes = root.GetComponentInChildren<NotebookView>(true);
            InstallWords(notes);
            var feedback = root.GetComponentInChildren<ContentFeedback>(true);
            var painting = root.GetComponentInChildren<PaintingPresenter>(true);
            var presentation = root.Find("Presentation");
            var canvasNode = PresentationArtPass.Ensure(presentation, "CanvasRoute");
            var canvas = canvasNode.GetComponent<CanvasRoutePresenter>();
            if (canvas == null) canvas = canvasNode.gameObject.AddComponent<CanvasRoutePresenter>();
            var oldSea = painting.SeaPainting.GetComponentInChildren<MeshFilter>(true);
            var visual = PresentationArtPass.Ensure(oldSea.transform.parent, "CanvasLandscape");
            visual.localPosition = oldSea.transform.localPosition + new Vector3(0,0,-.008f);
            visual.localRotation = oldSea.transform.localRotation;
            visual.localScale = oldSea.transform.localScale;
            var filter = visual.GetComponent<MeshFilter>();
            if (filter == null) filter = visual.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = oldSea.sharedMesh;
            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = visual.gameObject.AddComponent<MeshRenderer>();
            string materialPath = "Assets/_Game/Content/Art/Materials/Effects/M_CanvasLandscape.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            { material = new Material(Shader.Find("Game/CanvasLandscape")); AssetDatabase.CreateAsset(material, materialPath); }
            renderer.sharedMaterial = material;
            canvas.Configure(renderer);
            painting.ConfigureCanvasNarrative(true);
            root.GetComponentInChildren<StarryWorldSequence>(true).ConfigureCanvasNarrative(true);
            root.GetComponentInChildren<PaintingSeaSequence>(true).ConfigureCanvasNarrative(true);
            root.GetComponentInChildren<EndingPresenter>(true).ConfigureCanvasNarrative(true);

            var narrationLabel = Label("Narration", notes.transform, new Vector2(.12f,.026f), new Vector2(.88f,.082f), 21);
            narrationLabel.color = new Color(.83f,.82f,.76f);
            var narratorOpacity = narrationLabel.GetComponent<CanvasGroup>();
            if (narratorOpacity == null) narratorOpacity = narrationLabel.gameObject.AddComponent<CanvasGroup>();
            narratorOpacity.alpha = 0;
            narratorOpacity.blocksRaycasts = false;
            var voice = narrationLabel.GetComponent<NarrationSystem>();
            if (voice == null) voice = narrationLabel.gameObject.AddComponent<NarrationSystem>();
            voice.Configure(narrationLabel, narratorOpacity, feedback, NarrationContent.Create());
            var feedbackObject = new SerializedObject(feedback);
            var existing = feedbackObject.FindProperty("cues");
            var sounds = Enumerable.Range(0, existing.arraySize).Select(i =>
            {
                var item = existing.GetArrayElementAtIndex(i);
                return new AudioCueDefinition { Id=item.FindPropertyRelative("Id").stringValue,
                    Clip=(AudioClip)item.FindPropertyRelative("Clip").objectReferenceValue,
                    Volume=item.FindPropertyRelative("Volume").floatValue };
            }).Where(x=>x.Id!="narration_type").ToList();
            sounds.Add(new AudioCueDefinition { Id="narration_type", Clip=TypeTone(), Volume=.10f });
            feedback.ConfigureContent(MainlinePresentation.Create(), sounds.ToArray());

            var lightNode = PresentationArtPass.Ensure(presentation.Find("Lighting"), "CanvasFocus");
            var focal = lightNode.GetComponent<Light>();
            if (focal == null) focal = lightNode.gameObject.AddComponent<Light>();
            focal.type = LightType.Spot; focal.color = new Color(1,.84f,.61f);
            focal.intensity = 2.2f; focal.range = 5; focal.spotAngle = 42; focal.innerSpotAngle = 26;
            focal.shadows = LightShadows.Soft;
            lightNode.position = painting.transform.position + new Vector3(-.25f,1.35f,-1.8f);
            lightNode.rotation = Quaternion.LookRotation(painting.transform.position - lightNode.position);
            var endingLayer = Rect("CanvasEnding",notes.transform,Vector2.zero,Vector2.one);
            var black = endingLayer.GetComponent<Image>();
            if (black == null) black = endingLayer.gameObject.AddComponent<Image>();
            black.color = Color.black; black.raycastTarget = false;
            var blackout = endingLayer.GetComponent<CanvasGroup>();
            if (blackout == null) blackout = endingLayer.gameObject.AddComponent<CanvasGroup>();
            blackout.alpha = 0; blackout.blocksRaycasts = false;
            var endingWord = Label("Word",endingLayer,new Vector2(.2f,.40f),new Vector2(.8f,.60f),22);
            endingWord.color = new Color(.87f,.84f,.76f);
            // 完成叠层在暂停菜单下方，结束以后 Esc 仍可重开。
            endingLayer.SetAsFirstSibling();
            var endingNode = PresentationArtPass.Ensure(presentation,"CanvasEndingSequence");
            var ending = endingNode.GetComponent<CanvasEndingSequence>();
            if (ending == null) ending = endingNode.gameObject.AddComponent<CanvasEndingSequence>();
            ending.Configure(focal,blackout,endingWord,notes);
            root.GetComponentInChildren<GameBootstrap>(true).ConfigureNarrative(voice,canvas,ending);
            var reviewNode = PresentationArtPass.Ensure(presentation,"NarrativeReview");
            var review = reviewNode.GetComponent<NarrativeReviewDriver>();
            if(review==null) review=reviewNode.gameObject.AddComponent<NarrativeReviewDriver>();
            review.Configure(root.GetComponentInChildren<GameBootstrap>(true),notes,root.GetComponentInChildren<InspectionController>(true),
                root.GetComponentInChildren<InteractionSystem>(true),root.GetComponentInChildren<DayTransitionController>(true),
                root.GetComponentInChildren<StarryWorldSequence>(true),root.GetComponentInChildren<RoomModules>(true),root.GetComponentInChildren<WorldExterior>(true));
            PresentationArtPass.Save(root);
            Debug.Log("[叙事] 已接入双路线画布、短旁白和安静完成结局。");
        }
        private static void InstallWords(NotebookView notes)
        {
            var body=notes.transform.Find("NotebookPanel/Notebook");
            foreach(var name in new[]{"ObjectsHeading","ObservationsHeading","ConceptsHeading","Objects","Observations","Concepts"})
                body.Find(name).gameObject.SetActive(false);
            // 双路线增加了三条画内关系；为完整历史留出底部空间，不缩小中文到难以辨认。
            foreach(var name in new[]{"CompletedRecipes","CompletedRecipesSecond"})
            {
                var history=(RectTransform)body.Find(name);
                history.anchorMax=new Vector2(history.anchorMax.x,.305f);
                history.offsetMin=history.offsetMax=Vector2.zero;
            }
            var list=Rect("ThoughtMaterials",body,new Vector2(.06f,.335f),new Vector2(.295f,.79f));
            var image=list.GetComponent<Image>(); if(image==null) image=list.gameObject.AddComponent<Image>();
            image.color=new Color(.68f,.58f,.38f,.025f); image.raycastTarget=true;
            if(list.GetComponent<RectMask2D>()==null) list.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Words",list,new Vector2(0,1),new Vector2(1,1));
            content.pivot=new Vector2(.5f,1); content.sizeDelta=Vector2.zero;
            var layout=content.GetComponent<VerticalLayoutGroup>(); if(layout==null) layout=content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing=3; layout.padding=new RectOffset(8,8,8,12);
            layout.childControlHeight=layout.childControlWidth=true; layout.childForceExpandHeight=false;
            var fitting=content.GetComponent<ContentSizeFitter>(); if(fitting==null) fitting=content.gameObject.AddComponent<ContentSizeFitter>();
            fitting.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=list.GetComponent<ScrollRect>(); if(scroll==null) scroll=list.gameObject.AddComponent<ScrollRect>();
            scroll.content=content; scroll.viewport=list; scroll.horizontal=false; scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=30;
            var marks=Label("CanvasHabit",body,new Vector2(.095f,.79f),new Vector2(.295f,.835f),16);
            marks.alignment=TextAnchor.MiddleLeft;
            notes.ConfigureUnifiedWords(content,marks);
            var canvas=notes.GetComponentInChildren<NotebookCanvasView>(true);
            var area=(RectTransform)canvas.transform;
            area.anchorMin=new Vector2(.35f,.327f); area.anchorMax=new Vector2(.96f,.80f); area.offsetMin=area.offsetMax=Vector2.zero;
            canvas.ConfigureBlindComposition(true);
            var word=(RectTransform)body.Find("InsightWord"); word.anchorMin=new Vector2(.38f,.48f); word.anchorMax=new Vector2(.94f,.64f); word.offsetMin=word.offsetMax=Vector2.zero;
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var old = parent.Find(name);
            var rect = old != null ? (RectTransform)old : new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            if (old == null) rect.SetParent(parent,false);
            rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=rect.offsetMax=Vector2.zero;
            return rect;
        }
        private static Text Label(string name,Transform parent,Vector2 min,Vector2 max,int size)
        {
            var rect=Rect(name,parent,min,max);
            var text=rect.GetComponent<Text>(); if(text==null) text=rect.gameObject.AddComponent<Text>();
            text.text=""; text.font=AssetDatabase.LoadAssetAtPath<Font>(DemoSceneBuilder.ChineseFontPath);
            text.fontSize=size; text.alignment=TextAnchor.MiddleCenter; text.raycastTarget=false;
            var shadow=text.GetComponent<Shadow>(); if(shadow==null) shadow=text.gameObject.AddComponent<Shadow>();
            shadow.effectColor=new Color(0,0,0,.75f); shadow.effectDistance=new Vector2(0,-1);
            return text;
        }
        private static AudioClip TypeTone()
        {
            const string path="Assets/_Game/Content/Audio/Benchmark/NarrationType.wav";
            if (!File.Exists(path))
            {
                // 原创短音，不引入外部许可；包络迅速衰减，避免打字声压过环境。
                const int rate=22050, count=660;
                using (var writer=new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+count*2);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                    writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate*2);
                    writer.Write((short)2); writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count*2);
                    for (int i=0;i<count;i++)
                    {
                        double t=(double)i/rate;
                        double tone=(Math.Sin(t*2*Math.PI*1180)+.3*Math.Sin(t*2*Math.PI*1780))*Math.Exp(-t*190);
                        writer.Write((short)(tone*2100*Math.Min(1,i/25f)));
                    }
                }
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
