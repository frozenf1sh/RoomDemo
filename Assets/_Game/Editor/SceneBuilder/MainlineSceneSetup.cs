// =============================================================================
// 文件：MainlineSceneSetup.cs
// 职责：在已有美术主场景增量接入天花板／房间身份、语义画布与统一反馈。
// 边界：仅使用 Editor API，不重建房间，不覆盖人工模型、材质或稳定旧 ID。
// =============================================================================
using System.Linq;
using Game.Domain;
using Game.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Editor
{
    /// <summary>第六阶段的具体技术安装器，可重复执行且保留现有美术。</summary>
    public static class MainlineSceneSetup
    {
        /// <summary>安装三天主线所需的技术引用并保存明确场景版本。</summary>
        public static void Apply()
        {
            var root=PresentationArtPass.Room();
            Entity(root,MainlineRules.Ceiling,"Ceiling",new Vector3(0,4,1.5f),new Vector3(7.8f,.12f,5));
            Entity(root,MainlineRules.Room,"Room",new Vector3(0,.06f,1.1f),new Vector3(4.2f,.08f,3.8f));
            var window=root.GetComponentsInChildren<SceneEntity>(true).Single(x=>x.Id==DemoRules.Window);
            if(window.GetComponent<WorldObservationSource>()==null) window.gameObject.AddComponent<WorldObservationSource>();
            InstallCanvas(root); InstallFeedback(root); InstallTreeShadow(root);
            root.Find("UI/DebugCanvas/FirstLookTip").GetComponent<Text>().text="右键拖动观察 · N 翻开笔记 · H 轻提示";
            var node=PresentationArtPass.Ensure(root.Find("Presentation"),"MainlineReview");
            var review=node.GetComponent<MainlineReviewDriver>(); if(review==null) review=node.gameObject.AddComponent<MainlineReviewDriver>();
            review.Configure(root.GetComponentInChildren<GameBootstrap>(true),root.GetComponentInChildren<NotebookView>(true),
                root.GetComponentInChildren<InspectionController>(true),root.GetComponentInChildren<InteractionSystem>(true),
                root.GetComponentInChildren<DayTransitionController>(true),root.GetComponentInChildren<StarryWorldSequence>(true),
                root.GetComponentInChildren<RoomModules>(true),root.GetComponentInChildren<WorldExterior>(true));
            PresentationArtPass.Save(root);
            Debug.Log("[主线] 已接入三天内容、独立星空与森林、有限语义画布和单行字幕。");
        }
        private static void InstallTreeShadow(Transform root)
        {
            var modules=root.GetComponentInChildren<RoomModules>(true);
            var shadow=PresentationArtPass.Ensure(modules.WindowBoundary,"StarryTreeShadow");
            shadow.localPosition=new Vector3(-.35f,-.37f,-.19f);
            var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("M_WoodDark t:Material").First()));
            // 小范围的剪影只是新的可见观察，不提前展示整片森林或改动原窗结构。
            for(int i=0;i<6;i++)
            {
                var old=shadow.Find("Twig"+i); var twig=old!=null ? old.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
                if(old==null){twig.name="Twig"+i;twig.transform.SetParent(shadow,false);Object.DestroyImmediate(twig.GetComponent<Collider>());}
                twig.transform.localPosition=new Vector3(i==0?0:(i%2==0?.11f:-.11f),i*.085f,0);
                twig.transform.localScale=new Vector3(i==0?.022f:.013f,i==0?.72f:.30f,.018f);
                twig.transform.localRotation=Quaternion.Euler(0,0,i==0?0:i%2==0?-48:48);
                twig.GetComponent<Renderer>().sharedMaterial=material;
            }
            shadow.gameObject.SetActive(false);
            root.GetComponentInChildren<StarryWorldSequence>(true).ConfigureObservationVisual(shadow.gameObject);
        }
        private static void Entity(Transform root,ObjectId id,string name,Vector3 position,Vector3 size)
        {
            var parent=PresentationArtPass.Ensure(root.Find("Interactables"),name);
            var logic=PresentationArtPass.Ensure(parent,"Logic"); logic.localPosition=position;
            var box=logic.GetComponent<BoxCollider>(); if(box==null) box=logic.gameObject.AddComponent<BoxCollider>(); box.size=size;
            var entity=logic.GetComponent<SceneEntity>(); if(entity==null) entity=logic.gameObject.AddComponent<SceneEntity>(); entity.Configure(id);
            if(logic.GetComponent<Clickable>()==null) logic.gameObject.AddComponent<Clickable>();
            if(logic.GetComponent<WorldObservationSource>()==null) logic.gameObject.AddComponent<WorldObservationSource>();
            // 世界外形继续由原来的建筑模块负责；这个空视觉锚点保持身份与美术解耦。
            PresentationArtPass.Ensure(parent,"Visual");
        }
        private static void InstallCanvas(Transform root)
        {
            var notebook=root.GetComponentInChildren<NotebookView>(true);
            var body=(RectTransform)notebook.transform.Find("NotebookPanel/Notebook");
            Place(body,new Vector2(.10f,.07f),new Vector2(.90f,.93f));
            var backdrop=body.parent.Find("BookBacking"); if(backdrop!=null) Place((RectTransform)backdrop,new Vector2(.094f,.06f),new Vector2(.906f,.937f));
            var shadow=body.parent.Find("PaperShadow"); if(shadow!=null) Place((RectTransform)shadow,new Vector2(.105f,.054f),new Vector2(.912f,.932f));
            string[] headings={"ObjectsHeading","ObservationsHeading","ConceptsHeading"};
            string[] columns={"Objects","Observations","Concepts"};
            for(int i=0;i<3;i++)
            {
                float x=.047f+i*.166f;
                Place((RectTransform)body.Find(headings[i]),new Vector2(x,.78f),new Vector2(x+.157f,.83f));
                var header=body.Find(headings[i]).GetComponent<Text>(); header.fontSize=15;
                Place((RectTransform)body.Find(columns[i]),new Vector2(x,.255f),new Vector2(x+.155f,.767f));
            }
            foreach(var name in new[]{"Slot0","Slot1","Slot2","Interpret","Selection","InkRelation"}) body.Find(name).gameObject.SetActive(false);
            var history=body.Find("CompletedRecipes").GetComponent<Text>(); history.fontSize=12;
            Place(history.rectTransform,new Vector2(.055f,.035f),new Vector2(.95f,.233f));
            history.horizontalOverflow=HorizontalWrapMode.Wrap; history.verticalOverflow=VerticalWrapMode.Truncate;
            Place((RectTransform)body.Find("InsightWord"),new Vector2(.54f,.48f),new Vector2(.97f,.63f));
            var notes=body.Find("ManuscriptMarks/MarginNote"); if(notes!=null) notes.gameObject.SetActive(false);
            body.Find("Feedback").GetComponent<Text>().text="";
            // 纸面最多三个活动词条；左栏只是收藏列表，拖走不会移除已知线索。
            var area=Rect("SemanticCanvas",body,new Vector2(.535f,.263f),new Vector2(.962f,.805f));
            var paper=area.GetComponent<Image>(); if(paper==null) paper=area.gameObject.AddComponent<Image>(); paper.color=new Color(.55f,.49f,.35f,.035f); paper.raycastTarget=false;
            var prompt=Label("Invitation",area,"把记下的细节拖到这里",new Vector2(.06f,.86f),new Vector2(.94f,.98f),14);
            var template=Button("CanvasCardTemplate",area,"词",new Vector2(.5f,.5f),new Vector2(.5f,.5f),22);
            template.gameObject.SetActive(false);
            var action=Button("CanvasAction",area,"联想",new Vector2(.5f,.5f),new Vector2(.5f,.5f),18);
            ((RectTransform)action.transform).sizeDelta=new Vector2(82,36); action.gameObject.SetActive(false);
            var lines=new Image[2];
            for(int i=0;i<2;i++)
            {
                var rect=Rect("SoftThread"+i,area,new Vector2(.5f,.5f),new Vector2(.5f,.5f));
                var image=rect.GetComponent<Image>(); if(image==null) image=rect.gameObject.AddComponent<Image>(); image.raycastTarget=false; image.enabled=false; lines[i]=image;
            }
            var canvas=area.GetComponent<NotebookCanvasView>(); if(canvas==null) canvas=area.gameObject.AddComponent<NotebookCanvasView>();
            canvas.Configure(area,template,action,lines,prompt); notebook.ConfigureCanvas(canvas);
        }
        private static void InstallFeedback(Transform root)
        {
            var notebook=root.GetComponentInChildren<NotebookView>(true);
            var node=PresentationArtPass.Ensure(root.Find("Presentation"),"ContentFeedback");
            var audio=node.GetComponent<AudioSource>(); if(audio==null) audio=node.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake=false; audio.spatialBlend=0; audio.clip=Clip("Ink");
            var cues=new[] {
                Cue("clue_discover_soft","Pickup",.11f), Cue("concept_form","Ink",.18f),
                Cue("interpretation_soft","Paper",.14f), Cue("starry_open","Anomaly",.10f), Cue("forest_reveal","Wind",.07f) };
            var label=Label("InterpretationSubtitle",notebook.transform,"",new Vector2(.17f,.085f),new Vector2(.83f,.15f),23);
            label.color=new Color(.98f,.96f,.86f); (label.GetComponent<Shadow>() ?? label.gameObject.AddComponent<Shadow>()).effectColor=new Color(0,0,0,.85f);
            var group=label.GetComponent<CanvasGroup>(); if(group==null) group=label.gameObject.AddComponent<CanvasGroup>(); group.alpha=0; group.blocksRaycasts=false;
            var subtitle=label.GetComponent<OneLineSubtitle>(); if(subtitle==null) subtitle=label.gameObject.AddComponent<OneLineSubtitle>(); subtitle.Configure(label,group);
            var glint=PresentationArtPass.Ensure(node,"Motes");
            var particles=glint.GetComponent<ParticleSystem>(); if(particles==null) particles=glint.gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main; main.playOnAwake=false; main.loop=false; main.duration=1.2f; main.startLifetime=.9f; main.startSpeed=.08f; main.startSize=.013f; main.startColor=new Color(1,.88f,.60f,.8f); main.maxParticles=15; main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=particles.emission; emission.enabled=false;
            var shape=particles.shape; shape.enabled=true; shape.shapeType=ParticleSystemShapeType.Sphere; shape.radius=.055f;
            var color=particles.colorOverLifetime; color.enabled=true;
            var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.88f,.60f),0),new GradientColorKey(new Color(.7f,.9f,1),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.75f,.15f),new GradientAlphaKey(0,1)}); color.color=gradient;
            // 复用已有粒子材质，避免新导入资源和逐帧实例化材质。
            var renderer=particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial=root.GetComponentsInChildren<ParticleSystemRenderer>(true).First(x=>x!=renderer && x.sharedMaterial!=null).sharedMaterial;
            var feedback=node.GetComponent<ContentFeedback>(); if(feedback==null) feedback=node.gameObject.AddComponent<ContentFeedback>();
            feedback.Configure(audio,cues,MainlinePresentation.Create(),subtitle,notebook,root.GetComponentInChildren<InspectionController>(true),root.GetComponentInChildren<StarryWorldSequence>(true),root.GetComponentInChildren<Camera>(),particles);
            root.GetComponentInChildren<GameBootstrap>(true).ConfigureFeedback(feedback);
            root.GetComponentInChildren<DebugView>(true).ConfigureHint(subtitle);
        }
        private static AudioClip Clip(string name)=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Content/Audio/Benchmark/"+name+".wav");
        private static AudioCueDefinition Cue(string id,string clip,float volume)=>new AudioCueDefinition{Id=id,Clip=Clip(clip),Volume=volume};
        private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {
            var old=parent.Find(name); var rect=old!=null ? (RectTransform)old : new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            if(old==null) rect.SetParent(parent,false); Place(rect,min,max); return rect;
        }
        private static void Place(RectTransform rect,Vector2 min,Vector2 max) {rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private static Text Label(string name,Transform parent,string value,Vector2 min,Vector2 max,int size)
        {
            var rect=Rect(name,parent,min,max); var text=rect.GetComponent<Text>();if(text==null) text=rect.gameObject.AddComponent<Text>();
            text.text=value; text.font=AssetDatabase.LoadAssetAtPath<Font>(DemoSceneBuilder.ChineseFontPath); text.fontSize=size; text.alignment=TextAnchor.MiddleCenter; text.color=new Color(.35f,.31f,.24f,.85f);text.raycastTarget=false;return text;
        }
        private static Button Button(string name,Transform parent,string text,Vector2 min,Vector2 max,int size)
        {
            var rect=Rect(name,parent,min,max); var image=rect.GetComponent<Image>();if(image==null) image=rect.gameObject.AddComponent<Image>();image.color=new Color(.6f,.5f,.3f,.055f);image.raycastTarget=true;
            var button=rect.GetComponent<Button>();if(button==null) button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var label=Label("Label",rect,text,Vector2.zero,Vector2.one,size);label.color=new Color(.29f,.25f,.17f);
            return button;
        }
    }
}
