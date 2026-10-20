// 文件：AtelierFlowStageThreeBuilder.cs
// 职责：增量装配第三阶段窗景、独立干颜料板和刮刀Prefab、局部显色与功能占位。
// 边界：只保存开发副本，不重建房间、不替换UI母版、不修改正式构建列表和星空。
using System;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Unity.ArtRebuild;
using Game.Unity.TechnicalArt.HoverGold;
using Game.Unity.TechnicalArt.RealtimePainterly.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace Game.Editor.ArtRebuild
{
    /// <summary>第三批安装器，复用已验收TA04的局部表面算法与项目既有美术。</summary>
    public static class AtelierFlowStageThreeBuilder
    {
        private const string Root="Assets/_Game/Content/ArtRebuild/FourStage";
        private const string Main="Assets/_Game/Content/Scenes/AtelierPlayable.unity";
        private static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出Play。");
            for(int n=0;n<UnityEngine.SceneManagement.SceneManager.sceneCount;n++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(n).isDirty)
                    throw new InvalidOperationException("存在未保存场景，停止第三阶段安装。");
        }
        /// <summary>只增量装配本批对象，保留既有物件和人工美术。</summary>
        [MenuItem("游戏/新四阶段流程/接入第三阶段窗景与刮刀")]
        public static void Install()
        {
            Guard();
            ResizePalettePrefab();
            var scene=EditorSceneManager.OpenScene(Main);
            var flow=Object.FindAnyObjectByType<AtelierFlowGameplay>();
            if (flow==null || flow.Mirror==null) throw new InvalidOperationException("请先安装前两阶段。");
            var node=Child(flow.transform,"四阶段第三阶段_Logic与近景");
            var third=node.GetComponent<AtelierFlowStageThree>();
            if(third==null) third=node.AddComponent<AtelierFlowStageThree>();
            third.Flow=flow;
            flow.StageThree=third;
            var palette=node.GetComponent<AtelierFlowPalette>();
            if(palette==null) palette=node.AddComponent<AtelierFlowPalette>();
            third.Palette=palette;
            palette.Flow=flow;
            palette.PaletteShader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/刮刀干湿颜料.shader");
            var oldBoard=flow.References.Trial.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="沾湿色的旧木板");
            var wood=oldBoard.sharedMaterial;
            var paint=Material("第三阶段干湿颜料",palette.PaletteShader,new Color(.4f,.35f,.28f));
            var steel=Material("第三阶段刮刀钢面",Shader.Find("Universal Render Pipeline/Lit"),new Color(.35f,.39f,.43f));
            steel.SetFloat("_Metallic",.55f);steel.SetFloat("_Smoothness",.25f);
            var grip=Material("第三阶段刮刀木柄",Shader.Find("Universal Render Pipeline/Lit"),new Color(.14f,.075f,.035f));
            var board=Visual(node.transform,"Visual_第三阶段干颜料板","PF_干颜料板",()=>{
                var root=new GameObject("Visual_第三阶段干颜料板");
                Cube(root.transform,"有厚度的木板",Vector3.zero,new Vector3(.50f,.028f,.32f),wood);
                Cube(root.transform,"左干壳与右软色",new Vector3(0,.0155f,0),new Vector3(.46f,.003f,.28f),paint);
                return root;
            });
            board.transform.position=new Vector3(.80f,.954f,-.60f);
            // 清出工具台右侧：只移动原本无交互的笔筒与其中三支装饰笔，洗笔工作簇不动。
            var props=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="桌面与创作痕迹");
            var pot=props.Find("厚口陶瓷水杯");
            if(pot!=null) { var p=pot.position;p.x=1.85f;pot.position=p;RecordTransform(pot); }
            for(int n=0;n<3;n++)
            {
                var pen=props.Find("使用过的画笔_"+n);
                if(pen==null)continue;
                var p=pen.position;p.x=1.85f+(n-1)*.025f;pen.position=p;RecordTransform(pen);
            }
            palette.Surface=board.transform.Find("左干壳与右软色").GetComponent<Renderer>();
            var knife=Visual(node.transform,"Visual_桌上调色刮刀","PF_调色刮刀",()=>{
                var root=new GameObject("Visual_桌上调色刮刀");
                Cube(root.transform,"薄钢刀面",Vector3.zero,new Vector3(.055f,.0035f,.09f),steel);
                Cube(root.transform,"木柄",new Vector3(0,.005f,-.095f),new Vector3(.022f,.018f,.13f),grip);
                return root;
            });
            knife.transform.SetPositionAndRotation(new Vector3(1.10f,.9445f,-.45f),Quaternion.Euler(0,-20,0));
            palette.RestKnife=knife.transform;
            var pointer=Child(node.transform,"Visual_接触中的刮刀");
            if(pointer.transform.childCount==0)
            {
                var copy=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/PF_调色刮刀.prefab"),pointer.transform);
                copy.transform.localPosition=Vector3.zero;
                copy.transform.localRotation=Quaternion.identity;
            }
            palette.PointerKnife=pointer.transform;
            pointer.SetActive(false);
            var window=Object.FindObjectsByType<AtelierInteractionTarget>().Single(t=>t.Kind==AtelierTargetKind.Window);
            int layer=window.gameObject.layer;
            third.WindowPicking=window.GetComponent<Collider>();
            var boardTarget=Target(node.transform,"Logic_干颜料板",AtelierTargetKind.DryPalette,AtelierFlowRules.DryPalette.Value,
                "干颜料板",palette.Surface.bounds,board.GetComponentsInChildren<Renderer>(),layer);
            palette.SurfacePicking=boardTarget.GetComponent<Collider>();
            Target(node.transform,"Logic_调色刮刀",AtelierTargetKind.PaletteKnife,AtelierFlowRules.PaletteKnife.Value,"调色刮刀",
                Bounds(knife),knife.GetComponentsInChildren<Renderer>(),layer);
            third.PaletteFocus=Pose(node.transform,"干颜料板固定近景",new Vector3(.80f,1.55f,-1.35f),palette.Surface.bounds.center);
            third.WindowFocus=Pose(node.transform,"窗景近远调查机位",new Vector3(.40f,2.08f,-.50f),new Vector3(13,1.4f,45));
            third.WindowCompareFocus=Pose(node.transform,"窗景离远看看机位",new Vector3(.08f,2.08f,-1.20f),new Vector3(15,1.4f,45));
            // 热点放在窗洞前的对应投影上，不跨越窗框或用整段台词代领近远两份证据。
            Vector3 Project(Vector3 point)
            {
                Vector3 from=third.WindowFocus.position;
                return Vector3.LerpUnclamped(from,point,(2.04f-from.z)/(point.z-from.z));
            }
            var houses=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="返工_完整旧屋");
            var nearVisual=houses.GetChild(0).GetComponentsInChildren<Renderer>(true);
            var farVisual=houses.GetChild(1).GetComponentsInChildren<Renderer>(true);
            third.WindowNear=Target(node.transform,"Logic_近处屋檐",AtelierTargetKind.WindowNear,StudioRules.Window.Value,"近处屋檐",
                new Bounds(Project(new Vector3(9,2.1f,33)),new Vector3(.50f,.27f,.025f)),nearVisual,layer).gameObject;
            third.WindowFar=Target(node.transform,"Logic_远处灯火",AtelierTargetKind.WindowFar,StudioRules.Window.Value,"远处灯火",
                new Bounds(Project(new Vector3(27,2.5f,49.3f)),new Vector3(.34f,.27f,.025f)),farVisual,layer).gameObject;
            third.WindowNear.SetActive(false);third.WindowFar.SetActive(false);
            var exterior=Child(node.transform,"窗外连续景物显色范围");
            // 同时容纳远裁剪面上的既有无星天空。最近边界仍是Z=6，不跨入房间或屋面。
            exterior.transform.position=new Vector3(0,0,5003);
            flow.Draft.RevealVolumes=new[] { new AtelierDraftRevealVolume { Anchor=exterior.transform,Stage=3,Size=new Vector3(20000,20000,9994) } };
            flow.Draft.StageThreeObjects=window.HoverVisual.Surfaces.Concat(board.GetComponentsInChildren<Renderer>())
                .Concat(knife.GetComponentsInChildren<Renderer>()).Concat(pointer.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
            var anchor=Child(node.transform,"干颜料板周围桌面");
            anchor.transform.position=new Vector3(.88f,.94f,-.60f);
            flow.Draft.ContactRegions=flow.Draft.ContactRegions.Where(r=>r.Anchor!=anchor.transform)
                .Append(new AtelierDraftContactRegion { Anchor=anchor.transform,Stage=3,Radii=new Vector2(.38f,.22f) }).ToArray();
            Controls(third);
            foreach(var value in new Object[] {flow,third,palette,flow.Draft}) EditorUtility.SetDirty(value);
            foreach(var t in new[] {board.transform,knife.transform,pointer.transform})
                if(PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new IOException("第三阶段保存失败。");
            AssetDatabase.SaveAssets();
        }
        private static void ResizePalettePrefab()
        {
            string path=Root+"/Prefabs/PF_干颜料板.prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null)return;
            var contents=PrefabUtility.LoadPrefabContents(path);
            try
            {
                contents.transform.Find("有厚度的木板").localScale=new Vector3(.50f,.028f,.32f);
                contents.transform.Find("左干壳与右软色").localScale=new Vector3(.46f,.003f,.28f);
                PrefabUtility.SaveAsPrefabAsset(contents,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        private static void Controls(AtelierFlowStageThree third)
        {
            var parent=third.Flow.References.ContextPanel.transform.parent;
            var panel=parent.Find("第三阶段功能占位");
            if(panel==null)
            {
                var node=new GameObject("第三阶段功能占位",typeof(RectTransform),typeof(Image));
                node.transform.SetParent(parent,false);
                panel=node.transform;
                node.GetComponent<Image>().color=new Color(.10f,.075f,.055f,.85f);
            }
            var rect=panel.GetComponent<RectTransform>();
            rect.anchorMin=rect.anchorMax=new Vector2(0,.58f);rect.pivot=new Vector2(0,.5f);
            rect.anchoredPosition=new Vector2(28,0);rect.sizeDelta=new Vector2(410,250);
            third.Panel=panel.gameObject;
            var text=panel.Find("操作说明");
            if(text==null)
            {
                var label=Object.Instantiate(third.Flow.References.ObservationText,panel);
                label.name="操作说明";text=label.transform;
            }
            third.Instruction=text.GetComponent<Text>();
            third.Instruction.fontSize=22;third.Instruction.alignment=TextAnchor.UpperLeft;
            third.Instruction.horizontalOverflow=HorizontalWrapMode.Wrap;
            third.Instruction.verticalOverflow=VerticalWrapMode.Overflow;
            third.Instruction.rectTransform.anchorMin=third.Instruction.rectTransform.anchorMax=new Vector2(0,1);
            third.Instruction.rectTransform.pivot=new Vector2(0,1);
            third.Instruction.rectTransform.anchoredPosition=new Vector2(16,-12);
            third.Instruction.rectTransform.sizeDelta=new Vector2(378,96);
            Button Make(string name,string label,float x,float y)
            {
                var old=panel.Find(name);
                var button=old!=null?old.GetComponent<Button>():Object.Instantiate(third.Flow.References.ContextAction,panel);
                button.name=name;button.onClick=new Button.ButtonClickedEvent();
                var r=button.GetComponent<RectTransform>();
                r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);
                r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(178,48);
                var caption=button.GetComponentInChildren<Text>();caption.text=label;caption.fontSize=23;
                return button;
            }
            third.CompareButton=Make("离远看看","离远看看",16,-178);
            third.StudyButton=Make("细看颜料","细看颜料",16,-108);
            third.TryBrushButton=Make("试着取色","试着取色",210,-108);
            third.EdgeButton=Make("刀刃揭底","刀刃揭底",16,-178);
            third.FaceButton=Make("刀面推色","刀面推色",210,-178);
            third.LocateButton=Make("去颜料板试刀","去颜料板试刀",16,-178);
            third.Panel.SetActive(false);
            if(!PrefabUtility.IsPartOfPrefabInstance(panel.gameObject))
                PrefabUtility.SaveAsPrefabAssetAndConnect(panel.gameObject,Root+"/Prefabs/PF_第三阶段功能占位.prefab",InteractionMode.AutomatedAction);
        }
        private static GameObject Child(Transform parent,string name)
        { var old=parent.Find(name);if(old!=null)return old.gameObject;var value=new GameObject(name);value.transform.SetParent(parent,false);return value; }
        private static void RecordTransform(Transform value)
        {
            EditorUtility.SetDirty(value);
            if(PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
        }
        private static Renderer Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;
            Object.DestroyImmediate(obj.GetComponent<Collider>());obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position;obj.transform.localScale=scale;
            obj.GetComponent<Renderer>().sharedMaterial=material;return obj.GetComponent<Renderer>();
        }
        private static GameObject Visual(Transform parent,string label,string asset,Func<GameObject> create)
        {
            var old=parent.Find(label);if(old!=null)return old.gameObject;
            string path=Root+"/Prefabs/"+asset+".prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var obj=prefab!=null?(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent):create();
            obj.name=label;obj.transform.SetParent(parent,false);
            if(prefab==null) PrefabUtility.SaveAsPrefabAssetAndConnect(obj,path,InteractionMode.AutomatedAction);
            return obj;
        }
        private static Material Material(string name,Shader shader,Color color)
        {
            string path=Root+"/Materials/"+name+".mat";var value=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(value==null) { value=new Material(shader);AssetDatabase.CreateAsset(value,path); }
            value.SetColor("_BaseColor",color);EditorUtility.SetDirty(value);return value;
        }
        private static Bounds Bounds(GameObject obj)
        {
            var rs=obj.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;
            foreach(var r in rs.Skip(1)) b.Encapsulate(r.bounds);return b;
        }
        private static AtelierInteractionTarget Target(Transform parent,string name,AtelierTargetKind kind,string id,string label,Bounds bounds,Renderer[] surfaces,int layer)
        {
            var obj=Child(parent,name);obj.layer=layer;obj.transform.position=bounds.center;
            // Unity Editor缺失内置组件可能返回假空对象，不能用??代替Unity空值判断。
            var collider=obj.GetComponent<BoxCollider>();if(collider==null) collider=obj.AddComponent<BoxCollider>();
            collider.size=bounds.size+Vector3.one*.006f;
            var target=obj.GetComponent<AtelierInteractionTarget>();if(target==null) target=obj.AddComponent<AtelierInteractionTarget>();
            target.Kind=kind;target.ObjectId=id;target.DisplayName=label;
            var hover=obj.GetComponent<HoverGoldTarget>();if(hover==null) hover=obj.AddComponent<HoverGoldTarget>();
            hover.Surfaces=surfaces;target.HoverVisual=hover;
            return target;
        }
        private static Transform Pose(Transform parent,string name,Vector3 position,Vector3 target)
        { var t=Child(parent,name).transform;t.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));return t; }
        /// <summary>排队构建当前开发副本，避免长操作占用CLI响应线程。</summary>
        public static void QueueBuild()
        {
            EditorApplication.delayCall-=Build;
            EditorApplication.update-=RunQueuedBuild;
            EditorApplication.update+=RunQueuedBuild;
        }
        private static void RunQueuedBuild()
        {
            EditorApplication.update-=RunQueuedBuild;
            Directory.CreateDirectory("Logs/AtelierStageThree20261020");
            try
            {
                Guard();
                File.WriteAllText("Logs/AtelierStageThree20261020/队列构建开始.txt","开始");
                Build();
                File.WriteAllText("Logs/AtelierStageThree20261020/队列构建结束.txt","成功");
            }
            catch(Exception error)
            {
                File.WriteAllText("Logs/AtelierStageThree20261020/队列构建结束.txt",error.ToString());
                Debug.LogException(error);
            }
        }
        private static void Build()
        {
            const string folder="Builds/AtelierStageThree20261020";
            Directory.CreateDirectory(folder);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[] {Main},
                locationPathName=folder+"/四阶段前三阶段演示.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
            File.Copy("Assets/ThirdParty/NotoSans/OFL.txt",folder+"/NotoSans-OFL.txt",true);
            File.WriteAllText(folder+"/构建摘要.json",JsonUtility.ToJson(new BuildEvidence {
                result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,
                bytes=report.summary.totalSize,messages=report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Warning||m.type==LogType.Error).Select(m=>m.content).ToArray()
            },true));
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) Debug.LogError("第三阶段构建失败。");
        }
        [Serializable] private sealed class BuildEvidence { public string result;public int errors,warnings;public ulong bytes;public string[] messages; }
    }
}
