// 文件：AtelierFlowStageFourBuilder.cs
// 职责：增量接入点灯、独立末阶段工具、屋面持久细痕与可替换结尾控件。
// 边界：只保存AtelierPlayable，不重建房间、不替换笔记母版、不改星空材质与Build Settings。
using System;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Unity.ArtRebuild;
using Game.Unity.TechnicalArt.RealtimePainterly.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace Game.Editor.ArtRebuild
{
    /// <summary>末批装配器。重复执行只更新本批节点与明确的既有场景引用。</summary>
    public static class AtelierFlowStageFourBuilder
    {
        private const string Main="Assets/_Game/Content/Scenes/AtelierPlayable.unity";
        private const string Root="Assets/_Game/Content/ArtRebuild/FourStage";
        private const string Logs="Logs/AtelierStageFour20261020";
        private static void Guard()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出Play。");
            for(int n=0;n<UnityEngine.SceneManagement.SceneManager.sceneCount;n++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(n).isDirty)throw new InvalidOperationException("存在未保存场景，停止安装第四阶段。");
        }
        /// <summary>接入已有灯具与星空，只新增有限交互和独立功能占位。</summary>
        [MenuItem("游戏/新四阶段流程/接入第四阶段点灯与双收束")]
        public static void Install()
        {
            Guard();var scene=EditorSceneManager.OpenScene(Main);
            var flow=Object.FindAnyObjectByType<AtelierFlowGameplay>();
            if(flow==null||flow.StageThree==null)throw new InvalidOperationException("请先接入前三阶段。");
            var root=Node(flow.transform,"四阶段末阶段_Logic与近景");
            var stage=root.GetComponent<AtelierFlowStageFour>();if(stage==null)stage=root.gameObject.AddComponent<AtelierFlowStageFour>();
            var tool=root.GetComponent<AtelierFinaleTool>();if(tool==null)tool=root.gameObject.AddComponent<AtelierFinaleTool>();
            flow.StageFour=stage;stage.Flow=flow;stage.Tool=tool;tool.Stage=stage;
            flow.References.Lamp.enabled=true;flow.References.Lamp.KeepMatchBurning=true;
            flow.References.Finale.enabled=true;flow.References.Finale.FourStageFlow=true;
            flow.CanvasPort.SpreadShader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/刀面收色.shader");
            AtelierInteractionTarget Target(AtelierTargetKind kind)=>Object.FindObjectsByType<AtelierInteractionTarget>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(t=>t.Kind==kind);
            stage.RoofTarget=Target(AtelierTargetKind.Ceiling);stage.SkyTarget=Target(AtelierTargetKind.Sky);
            stage.MarkTarget=Target(AtelierTargetKind.SharedMark);stage.RoomTarget=Target(AtelierTargetKind.Room);
            var camera=flow.References.ViewCamera;
            var plane=new Plane(Vector3.up,new Vector3(0,4.212f,0));
            if(!plane.Raycast(camera.ViewportPointToRay(new Vector3(.30f,.90f,0)),out float distance))throw new InvalidOperationException("屋面细痕不在主画幅内。");
            stage.RoofOrigin=camera.ViewportPointToRay(new Vector3(.30f,.90f,0)).GetPoint(distance);
            stage.RoofSize=new Vector2(1.35f,1.35f);
            stage.RoofOccluders=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Collider>(true))
                .Where(c=>c.name.StartsWith("遮挡_")&&c.name.Contains("屋顶")).ToArray();
            Vector3 skyCentre=camera.transform.position+(stage.RoofOrigin-camera.transform.position)*((4.6f-camera.transform.position.y)/(stage.RoofOrigin.y-camera.transform.position.y));
            stage.SkyTarget.transform.position=skyCentre;
            stage.SkyTarget.GetComponent<BoxCollider>().size=new Vector3(.9f,.015f,.9f);
            // 屋面拾取只围住真实细痕，不能在整片天花板任意点击获取隐藏来源。
            stage.RoofTarget.transform.position=stage.RoofOrigin;
            var roofBox=stage.RoofTarget.GetComponent<BoxCollider>();roofBox.size=new Vector3(.96f,.012f,.76f);
            stage.RoofTarget.DisplayName="屋面细痕";
            stage.RoofFocus=Pose(root,"屋面细痕固定近景",stage.RoofOrigin+new Vector3(0,-1.65f,-1),stage.RoofOrigin);
            // 另一处调查面选在窗下灰泥，主视角始终可见；不把整个房间的大碰撞当作自动领词按钮。
            stage.RoomTarget.transform.position=new Vector3(.52f,.70f,2.345f);
            var roomBox=stage.RoomTarget.GetComponent<BoxCollider>();roomBox.size=new Vector3(.85f,.30f,.025f);
            stage.RoomTarget.DisplayName="窗下的墙面";
            stage.RoomFocus=Pose(root,"窗下墙面固定近景",new Vector3(.52f,1.20f,.42f),stage.RoomTarget.transform.position);
            var trace=Node(root,"Visual_持久屋面细痕");
            var line=trace.GetComponent<LineRenderer>();if(line==null)line=trace.gameObject.AddComponent<LineRenderer>();
            string materialPath=Root+"/Materials/末阶段屋面冷蓝细痕.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null) {material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(material,materialPath);}
            material.SetColor("_BaseColor",Color.white);EditorUtility.SetDirty(material);
            line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=4;
            line.startWidth=line.endWidth=.008f;
            for(int n=0;n<4;n++)line.SetPosition(n,stage.RoofOrigin+new Vector3((n-1.5f)*.075f,-.008f,Mathf.Sin(n)*.012f));
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
            stage.RoofTrace=line;trace.gameObject.SetActive(false);
            flow.Draft.RoofObjects=flow.References.Finale.RoofSurfaces.Concat(new Renderer[] {line}).ToArray();
            var pointer=Node(root,"Visual_末阶段接触刮刀");
            if(pointer.childCount==0)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/PF_调色刮刀.prefab"),pointer);
            tool.PointerKnife=pointer;pointer.gameObject.SetActive(false);
            var lamp=flow.References.Lamp;
            var lampRoot=lamp.Wick.parent;
            // 两个工作簇显色体积只覆盖左侧灯与火柴，不让右柜、桌布或整张桌面提前恢复。
            var lampRegion=Node(root,"点灯灯具显色范围");lampRegion.position=Target(AtelierTargetKind.OilLamp).GetComponent<Collider>().bounds.center;
            var matchesRegion=Node(root,"火柴盒显色范围");matchesRegion.position=Target(AtelierTargetKind.Matches).GetComponent<Collider>().bounds.center;
            flow.Draft.StageFourObjects=new[] {lamp.WickSurface,lamp.LampFlame,lamp.MatchFlame,lamp.MatchHead}
                .Concat(pointer.GetComponentsInChildren<Renderer>(true)).Where(r=>r!=null).Distinct().ToArray();
            flow.Draft.RevealVolumes=flow.Draft.RevealVolumes.Where(v=>v.Anchor!=lampRegion&&v.Anchor!=matchesRegion)
                .Concat(new[] {new AtelierDraftRevealVolume {Anchor=lampRegion,Stage=4,Size=new Vector3(.30f,.48f,.30f)},
                    new AtelierDraftRevealVolume {Anchor=matchesRegion,Stage=4,Size=new Vector3(.16f,.12f,.14f)}}).ToArray();
            var contact=Node(root,"左灯周围少量桌面");contact.position=new Vector3(-2.51f,.78f,-.15f);
            flow.Draft.ContactRegions=flow.Draft.ContactRegions.Where(r=>r.Anchor!=contact)
                .Append(new AtelierDraftContactRegion {Anchor=contact,Stage=4,Radii=new Vector2(.25f,.20f)}).ToArray();
            flow.References.Finale.ErasureUnlit=Shader.Find("RoomDemo/Atelier/ErasureUnlit");
            flow.References.Finale.AdditionalSurfaces=flow.Draft.ClearObjects.Concat(flow.Draft.StageTwoObjects)
                .Concat(flow.Draft.StageThreeObjects).Concat(flow.Draft.StageFourObjects).Concat(flow.Draft.RoofObjects)
                .Where(r=>r!=null).Distinct().ToArray();
            Controls(stage);
            foreach(var o in new Object[] {flow,stage,tool,flow.Draft,flow.CanvasPort,flow.References.Lamp,flow.References.Finale,
                stage.RoofTarget,stage.RoomTarget,stage.SkyTarget,stage.SkyTarget.transform,stage.SkyTarget.GetComponent<BoxCollider>(),stage.RoofTarget.transform,stage.RoomTarget.transform,roofBox,roomBox})
            {
                EditorUtility.SetDirty(o);
                if(PrefabUtility.IsPartOfPrefabInstance(o))PrefabUtility.RecordPrefabInstancePropertyModifications(o);
            }
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("第四阶段保存失败。");
            AssetDatabase.SaveAssets();
        }
        private static void Controls(AtelierFlowStageFour stage)
        {
            var flow=stage.Flow;var parent=flow.References.ContextPanel.transform.parent;
            var panel=Node(parent,"第四阶段功能占位",true);stage.Panel=panel.gameObject;
            var rect=(RectTransform)panel;
            rect.anchorMin=rect.anchorMax=new Vector2(0,.62f);rect.pivot=new Vector2(0,.5f);
            rect.anchoredPosition=new Vector2(28,0);rect.sizeDelta=new Vector2(408,250);
            var image=panel.GetComponent<Image>();if(image==null)image=panel.gameObject.AddComponent<Image>();image.color=new Color(.10f,.075f,.055f,.90f);image.raycastTarget=false;
            stage.Instruction=Label(flow,panel,"操作说明","",new Vector2(16,-12),new Vector2(376,102),22);
            stage.PaintButton=Button(flow,panel,"暖星笔刷","暖星笔刷",16,-124,178);
            stage.FaceButton=Button(flow,panel,"刀面收色","刀面收色",210,-124,178);
            stage.EdgeButton=Button(flow,panel,"刀刃揭底","刀刃揭底",16,-184,178);
            stage.TargetButton=Button(flow,panel,"在笔记中选择","打开笔记",16,-124,178);
            stage.UndoButton=Button(flow,panel,"撤销一笔","撤销一笔",16,-184,178);
            panel.gameObject.SetActive(false);
            SaveOwnedControls(panel.gameObject,Root+"/Prefabs/PF_第四阶段功能占位.prefab");
            var fade=Node(parent,"四阶段结尾淡出",true);
            ApplyEndingLayout(flow,stage,fade);
            SaveOwnedControls(fade.gameObject,Root+"/Prefabs/PF_四阶段结尾占位.prefab");
        }
        /// <summary>结尾菜单的统一排版：终章标识、路线副题、主操作与低优先级的练习回看。</summary>
        /// <remarks>场景和Prefab母版共用这一入口，避免只在运行时改文字而留下旧的“再试”界面。</remarks>
        public static void ApplyEndingLayout(AtelierFlowGameplay flow,AtelierFlowStageFour stage,Transform fade)
        {
            if(flow==null||stage==null||fade==null)throw new InvalidOperationException("结尾菜单缺少绑定对象。");
            var fr=(RectTransform)fade;
            fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=fr.offsetMax=Vector2.zero;fade.SetAsLastSibling();
            var background=fade.GetComponent<Image>();if(background==null)background=fade.gameObject.AddComponent<Image>();
            // 深靛蓝而不是纯黑：终局仍能透出一点最后的世界，不是失败后的黑屏。
            background.color=new Color(.012f,.018f,.038f,.92f);background.raycastTarget=true;
            var group=fade.GetComponent<CanvasGroup>();if(group==null)group=fade.gameObject.AddComponent<CanvasGroup>();
            group.alpha=0;group.blocksRaycasts=false;group.interactable=true;stage.EndingFade=group;
            var ending=Node(fade,"结束菜单",true);var er=(RectTransform)ending;
            er.anchorMin=er.anchorMax=new Vector2(.5f,.5f);er.pivot=new Vector2(.5f,.5f);er.anchoredPosition=Vector2.zero;er.sizeDelta=new Vector2(720,480);
            stage.EndingPanel=ending.gameObject;
            // 旧版“从第四阶段再试”和第一次整改留下的按钮一并清掉；练习回看改成文字链接。
            foreach(var staleName in new[]{"从第四阶段再试","回到点灯前","回到点灯前_点击区"})
            {var stale=ending.Find(staleName);if(stale!=null)Object.DestroyImmediate(stale.gameObject);}
            var oldNote=ending.Find("练习说明");if(oldNote!=null&&ending.Find("结尾副题")==null)oldNote.name="结尾副题";
            var eyebrow=Label(flow,ending,"结尾眉题","星夜未干 · 终章",new Vector2(0,-16),new Vector2(720,26),15);
            eyebrow.alignment=TextAnchor.MiddleCenter;eyebrow.color=new Color(.56f,.62f,.72f,1);
            stage.EndingTitle=Label(flow,ending,"结尾标题","",new Vector2(0,-48),new Vector2(720,82),46);
            stage.EndingTitle.alignment=TextAnchor.MiddleCenter;
            stage.EndingSubtitle=Label(flow,ending,"结尾副题","画留下了。房间还亮着，夜色也还在。",new Vector2(0,-134),new Vector2(720,46),22);
            stage.EndingSubtitle.alignment=TextAnchor.MiddleCenter;stage.EndingSubtitle.color=new Color(.72f,.78f,.88f,1);
            var dividerNode=Node(ending,"结尾分隔线",true);var dr=(RectTransform)dividerNode;
            dr.anchorMin=dr.anchorMax=new Vector2(.5f,1);dr.pivot=new Vector2(.5f,1);dr.anchoredPosition=new Vector2(0,-182);dr.sizeDelta=new Vector2(180,1);
            var divider=dividerNode.GetComponent<Image>();if(divider==null)divider=dividerNode.gameObject.AddComponent<Image>();
            divider.color=new Color(.72f,.64f,.46f,.32f);divider.raycastTarget=false;
            stage.AgainButton=Button(flow,ending,"重新开始","重新开始",78,-238,270);
            stage.QuitButton=Button(flow,ending,"退出","退出",372,-238,270);
            LayoutEndingButton(stage.AgainButton,-147,-238,270,48,22,new Color(.52f,.47f,.35f,.14f),new Color(.94f,.89f,.78f,1));
            LayoutEndingButton(stage.QuitButton,147,-238,270,48,22,new Color(.52f,.47f,.35f,.06f),new Color(.94f,.89f,.78f,1));
            // 练习回看是低优先级文字链接：文字独立渲染，透明点击区只负责命中，避免按钮模板吞掉文字。
            var retryHit=Node(ending,"回到点灯前_点击区",true);var rr=(RectTransform)retryHit;
            rr.anchorMin=rr.anchorMax=new Vector2(.5f,1);rr.pivot=new Vector2(.5f,1);rr.anchoredPosition=new Vector2(0,-310);rr.sizeDelta=new Vector2(300,34);
            var retryImage=retryHit.GetComponent<Image>();if(retryImage==null)retryImage=retryHit.gameObject.AddComponent<Image>();
            retryImage.color=new Color(.52f,.47f,.35f,.04f);retryImage.raycastTarget=true;
            var retryButton=retryHit.GetComponent<Button>();if(retryButton==null)retryButton=retryHit.gameObject.AddComponent<Button>();
            retryButton.targetGraphic=retryImage;retryButton.onClick=new Button.ButtonClickedEvent();stage.RetryButton=retryButton;
            var retryText=Label(flow,ending,"回到点灯前","回到点灯前",new Vector2(0,-310),new Vector2(720,34),18);
            retryText.alignment=TextAnchor.MiddleCenter;retryText.color=new Color(.74f,.70f,.60f,1);retryText.raycastTarget=false;
            var underlineNode=Node(ending,"回看下划线",true);var ur=(RectTransform)underlineNode;
            ur.anchorMin=ur.anchorMax=new Vector2(.5f,1);ur.pivot=new Vector2(.5f,1);ur.anchoredPosition=new Vector2(0,-338);ur.sizeDelta=new Vector2(120,1);
            var underline=underlineNode.GetComponent<Image>();if(underline==null)underline=underlineNode.gameObject.AddComponent<Image>();
            underline.color=new Color(.72f,.64f,.46f,.30f);underline.raycastTarget=false;
            var note=Label(flow,ending,"练习说明","回看会回到点灯前，前三层作品保留。",new Vector2(0,-350),new Vector2(720,22),14);
            note.alignment=TextAnchor.MiddleCenter;note.color=new Color(.55f,.58f,.65f,1);note.raycastTarget=false;
            ending.gameObject.SetActive(false);
        }
        private static void LayoutEndingButton(Button button,float x,float y,float width,float height,int fontSize,Color background,Color caption)
        {
            var rect=button.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);
            rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,height);
            var image=button.GetComponent<Image>();if(image!=null)image.color=background;
            var text=button.GetComponentInChildren<Text>();text.fontSize=fontSize;text.color=caption;text.alignment=TextAnchor.MiddleCenter;
        }
        private static void SaveOwnedControls(GameObject root,string path)
        {
            // 本工具拥有的两个独立UI母版需要实际更新，不能只留下未记录的场景实例改动。
            foreach(var component in root.GetComponentsInChildren<Component>(true))
            {
                if(component==null)continue;
                EditorUtility.SetDirty(component);
                if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            if(PrefabUtility.IsPartOfPrefabInstance(root))PrefabUtility.SaveAsPrefabAsset(root,path);
            else PrefabUtility.SaveAsPrefabAssetAndConnect(root,path,InteractionMode.AutomatedAction);
        }
        private static Text Label(AtelierFlowGameplay flow,Transform parent,string name,string content,Vector2 position,Vector2 size,int font)
        {
            var node=parent.Find(name);var text=node!=null?node.GetComponent<Text>():Object.Instantiate(flow.References.ObservationText,parent);
            text.name=name;text.text=content;text.fontSize=font;text.alignment=TextAnchor.UpperLeft;text.raycastTarget=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;
            var r=text.rectTransform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=size;
            return text;
        }
        private static Button Button(AtelierFlowGameplay flow,Transform parent,string name,string label,float x,float y,float width)
        {
            var node=parent.Find(name);var b=node!=null?node.GetComponent<Button>():Object.Instantiate(flow.References.ContextAction,parent);
            b.name=name;b.onClick=new Button.ButtonClickedEvent();var r=b.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,48);var text=b.GetComponentInChildren<Text>();text.text=label;text.fontSize=23;
            var caption=text.rectTransform;caption.anchorMin=Vector2.zero;caption.anchorMax=Vector2.one;
            caption.offsetMin=new Vector2(10,4);caption.offsetMax=new Vector2(-10,-4);
            text.alignment=TextAnchor.MiddleCenter;text.horizontalOverflow=HorizontalWrapMode.Wrap;
            return b;
        }
        private static Transform Node(Transform parent,string name,bool ui=false)
        {
            var old=parent.Find(name);if(old!=null)return old;
            var node=ui?new GameObject(name,typeof(RectTransform)):new GameObject(name);node.transform.SetParent(parent,false);return node.transform;
        }
        private static Transform Pose(Transform parent,string name,Vector3 position,Vector3 target)
        {var t=Node(parent,name);t.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));return t;}
        /// <summary>仅显式调用时在新Play第一帧开启检查；SessionState只暂存本次检查路径。</summary>
        public static void StartEditorReview(string output)
        {
            Guard();SessionState.SetString("atelier.stagefour.review",Path.GetFullPath(output));EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod]private static void ObserveReviewStart()
        {
            EditorApplication.playModeStateChanged-=BeginReview;
            EditorApplication.playModeStateChanged+=BeginReview;
        }
        private static void BeginReview(PlayModeStateChange change)
        {
            if(change!=PlayModeStateChange.EnteredPlayMode)return;
            string output=SessionState.GetString("atelier.stagefour.review","");if(output=="")return;
            SessionState.EraseString("atelier.stagefour.review");
            Time.timeScale=4;AtelierOpeningPlayerReview.StartEditorReview(output,true,true);
        }
        /// <summary>队列构建开发副本，不修改正式场景列表。</summary>
        public static void QueueBuild() {EditorApplication.update-=Build;EditorApplication.update+=Build;}
        private static void Build()
        {
            EditorApplication.update-=Build;Directory.CreateDirectory(Logs);
            try
            {
                Guard();const string folder="Builds/AtelierStageFour20261020";Directory.CreateDirectory(folder);
                File.WriteAllText(Logs+"/构建开始.txt","开始");
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[] {Main},locationPathName=folder+"/四阶段完整流程演示.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
                File.Copy("Assets/ThirdParty/NotoSans/OFL.txt",folder+"/NotoSans-OFL.txt",true);
                File.WriteAllText(folder+"/构建摘要.json",JsonUtility.ToJson(new BuildEvidence {result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize,messages=report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Warning||m.type==LogType.Error).Select(m=>m.content).ToArray()},true));
                File.WriteAllText(Logs+"/构建结束.txt",report.summary.result.ToString());
            }
            catch(Exception error) {File.WriteAllText(Logs+"/构建结束.txt",error.ToString());Debug.LogException(error);}
        }
        [Serializable]private sealed class BuildEvidence {public string result;public int errors,warnings;public ulong bytes;public string[] messages;}
    }
}
