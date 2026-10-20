// 文件：AtelierFlowRemediationBuilder.cs
// 职责：增量配置流程整改所需的中央结壳、三个湿色落点与分段擦除清单。
// 边界：只保存可玩副本和指定UI母版，不重建房间，不改正式构建或用户美术。
using System;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Unity;
using Game.Unity.ArtRebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace Game.Editor.ArtRebuild
{
    /// <summary>重复执行更新自有节点；场景未保存时停止以保护人工编辑。</summary>
    public static class AtelierFlowRemediationBuilder
    {
        private const string Main="Assets/_Game/Content/Scenes/AtelierPlayable.unity";
        private const string Root="Assets/_Game/Content/ArtRebuild/FourStage";
        /// <summary>将本轮规则对应的世界交互与纯表现对象接入已有副本。</summary>
        [MenuItem("游戏/新四阶段流程/应用第三四阶段流程整改")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出运行。");
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("场景尚未保存，未应用整改。");
            var scene=EditorSceneManager.OpenScene(Main);
            var flow=Object.FindAnyObjectByType<AtelierFlowGameplay>();var stage=flow.StageFour;var third=flow.StageThree;
            var root=Node(stage.transform,"流程整改视觉与落点");
            var camera=flow.References.ViewCamera;
            var plane=new Plane(Vector3.up,new Vector3(0,4.205f,0));
            plane.Raycast(camera.ViewportPointToRay(new Vector3(.47f,.93f,0)),out float distance);
            stage.RoofOrigin=camera.ViewportPointToRay(new Vector3(.47f,.93f,0)).GetPoint(distance);
            stage.RoofOrigin=new Vector3(Mathf.Clamp(stage.RoofOrigin.x,-1.8f,1.8f),4.205f,Mathf.Clamp(stage.RoofOrigin.z,-1.5f,2.2f));
            stage.RoofSize=new Vector2(2.4f,1.8f);
            stage.RoofTarget.transform.position=stage.RoofOrigin;
            stage.RoofTarget.GetComponent<BoxCollider>().size=new Vector3(2.35f,.025f,1.75f);stage.RoofTarget.DisplayName="屋顶的旧表层";
            stage.RoofFocus.SetPositionAndRotation(stage.RoofOrigin+new Vector3(0,-1.6f,-1.1f),Quaternion.LookRotation(new Vector3(0,1.6f,1.1f)));
            var coat=Surface(root,"屋顶中央干颜料",Mesh("中央结壳",1.13f,.83f),Material("屋顶结壳",Shader.Find("RoomDemo/Atelier/KnifePalette")));
            coat.transform.position=stage.RoofOrigin+Vector3.down*.045f;coat.sharedMaterial.SetFloat("_ProbeRoof",2);coat.sharedMaterial.SetFloat("_AtelierEraseGroup",1);
            coat.sharedMaterial.SetVector("_ProbeOrigin",stage.RoofOrigin);coat.sharedMaterial.SetVector("_ProbeScale",new Vector4(2.4f,1.8f,0,0));
            stage.RoofCoat=coat;coat.gameObject.SetActive(false);
            var floorPlane=new Plane(Vector3.up,new Vector3(0,.025f,0));
            floorPlane.Raycast(camera.ViewportPointToRay(new Vector3(.76f,.12f,0)),out float floorDistance);
            var floor=camera.ViewportPointToRay(new Vector3(.76f,.12f,0)).GetPoint(floorDistance);
            floor=new Vector3(Mathf.Clamp(floor.x,-2.9f,2.9f),.025f,Mathf.Clamp(floor.z,-2.6f,1.8f));
            stage.LandingPoints=new[] {new Vector3(1.35f,1.13f,2.05f),new Vector3(.70f,.951f,-1.25f),floor};
            // 桌面湿色近景：落定后自动看一眼，玩家仍要亲点这团湿颜料才会移近地板。
            var wetFocus=Node(root,"湿色桌面近景");var wetTarget=stage.LandingPoints[1];
            wetFocus.position=wetTarget+new Vector3(-.05f,.55f,-.95f);
            wetFocus.rotation=Quaternion.LookRotation(wetTarget-wetFocus.position);
            stage.WetDeskFocus=wetFocus;
            var wet=Material("屋顶落下的湿色",Shader.Find("RoomDemo/Atelier/WetDeposit"));
            stage.WetPuddles=new Renderer[3];stage.FallingDrops=new Renderer[3];
            for(int i=0;i<3;i++)
            {
                var puddle=Surface(root,"湿颜料落点_"+i,Mesh("湿色形状_"+i,i==2?.62f:.31f,i==2?.47f:.16f),wet);
                puddle.transform.position=stage.LandingPoints[i];puddle.gameObject.SetActive(false);stage.WetPuddles[i]=puddle;
                var drop=Node(root,"下落湿色_"+i);
                var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);var sphereMesh=sphere.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(sphere);
                if(drop.GetComponent<MeshFilter>()==null)drop.gameObject.AddComponent<MeshFilter>();drop.GetComponent<MeshFilter>().sharedMesh=sphereMesh;
                var renderer=drop.GetComponent<MeshRenderer>();if(renderer==null)renderer=drop.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=wet;
                drop.localScale=new Vector3(.035f,.22f,.035f);drop.gameObject.SetActive(false);stage.FallingDrops[i]=renderer;
            }
            var entry=Node(root,"Logic_桌面落下的湿颜料");entry.gameObject.layer=stage.RoomTarget.gameObject.layer;
            entry.position=stage.LandingPoints[1]+Vector3.up*.022f;
            var entryBox=entry.GetComponent<BoxCollider>();if(entryBox==null)entryBox=entry.gameObject.AddComponent<BoxCollider>();
            entryBox.size=new Vector3(.65f,.04f,.35f);entryBox.enabled=false;
            var entryTarget=entry.GetComponent<AtelierInteractionTarget>();if(entryTarget==null)entryTarget=entry.gameObject.AddComponent<AtelierInteractionTarget>();
            entryTarget.Kind=AtelierTargetKind.Room;entryTarget.ObjectId=StudioRules.Room.Value;entryTarget.DisplayName="散落的湿颜料";
            var hover=entry.GetComponent<Game.Unity.TechnicalArt.HoverGold.HoverGoldTarget>();
            if(hover==null)hover=entry.gameObject.AddComponent<Game.Unity.TechnicalArt.HoverGold.HoverGoldTarget>();
            hover.Surfaces=new[]{stage.WetPuddles[1]};entryTarget.HoverVisual=hover;stage.RoomEntryTarget=entryTarget;
            stage.RoomTarget.transform.position=floor+Vector3.up*.02f;stage.RoomTarget.DisplayName="落在地板上的湿颜料";
            stage.RoomTarget.GetComponent<BoxCollider>().size=new Vector3(1.3f,.025f,1.0f);
            stage.RoomFocus.SetPositionAndRotation(floor+new Vector3(0,1.35f,-1.45f),Quaternion.LookRotation(new Vector3(0,-1.35f,1.45f)));
            stage.SkyFocus=Node(root,"天空固定视角");stage.SkyFocus.SetPositionAndRotation(camera.transform.position,Quaternion.LookRotation(new Vector3(.12f,.65f,1)));
            stage.SkyTarget.transform.SetPositionAndRotation(camera.transform.position+camera.transform.forward*8,Quaternion.identity);
            stage.SkyTarget.GetComponent<BoxCollider>().size=new Vector3(75,65,.08f);
            stage.SkyTarget.DisplayName="天空";
            var near=third.WindowNear.GetComponent<AtelierInteractionTarget>();near.ObjectId=AtelierFlowRules.NearWindowLight.Value;near.DisplayName="近处的窗灯";
            var far=third.WindowFar.GetComponent<AtelierInteractionTarget>();far.ObjectId=AtelierFlowRules.FarWindowLight.Value;far.DisplayName="远处的窗灯";
            // 保留原来的窗前投影，缩小到窗灯附近并给点击少量容错。
            near.GetComponent<BoxCollider>().size=new Vector3(.15f,.15f,.025f);
            far.GetComponent<BoxCollider>().size=new Vector3(.12f,.12f,.025f);
            var houses=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="返工_完整旧屋");
            Material glow=null;
            for(int i=0;i<houses.childCount;i++)
            {
                var house=houses.GetChild(i).GetComponentInChildren<MeshRenderer>();
                var materials=house.sharedMaterials;
                int slot=Array.FindIndex(materials,m=>m!=null&&(m.name=="旧屋局部窗灯"||m.name=="近远窗灯连续擦除"));
                if(slot<0)continue;
                if(glow==null)
                {
                    const string glowPath=Root+"/Materials/近远窗灯连续擦除.mat";
                    glow=AssetDatabase.LoadAssetAtPath<Material>(glowPath);
                    if(glow==null){glow=new Material(materials[slot]){name="近远窗灯连续擦除"};AssetDatabase.CreateAsset(glow,glowPath);}
                    // 让构建直接看见发光变体，运行时换Shader不能保证未引用的变体不会被剔除。
                    glow.shader=flow.References.Finale.ErasureLit;glow.EnableKeyword("_EMISSION");
                    glow.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
                    glow.SetFloat("_AtelierEraseGroup",3);EditorUtility.SetDirty(glow);
                }
                materials[slot]=glow;house.sharedMaterials=materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(house);
                if(i>1)continue;
                var mesh=house.GetComponent<MeshFilter>().sharedMesh;
                var lightPoint=house.transform.TransformPoint(mesh.GetSubMesh(slot).bounds.center);
                Vector3 from=third.WindowFocus.position;
                var projected=Vector3.LerpUnclamped(from,lightPoint,(2.04f-from.z)/(lightPoint.z-from.z));
                (i==0?near:far).transform.position=projected;
            }
            foreach(var panel in new[] {third.Panel,stage.Panel})
            {foreach(Transform child in panel.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);panel.SetActive(false);}
            stage.RoofTrace.gameObject.SetActive(false);
            flow.Draft.RoofObjects=flow.References.Finale.RoofSurfaces.Concat(new[] {coat}).ToArray();
            var finale=flow.References.Finale;
            finale.InteriorOverlays=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Canvas>(true))
                .Where(c=>c.renderMode==RenderMode.WorldSpace).Select(c=>c.gameObject).ToArray();
            var retained=finale.ArtRoot.GetComponentsInChildren<Renderer>(true).Where(r=>r.bounds.center.z>=5
                ||r.transform.parent.name=="绷布与木框"||r.transform.parent.name=="细木作画架").ToArray();
            finale.FinalSurfaces=retained.Append(flow.CanvasPort.Surface).Distinct().ToArray();
            finale.AdditionalSurfaces=finale.AdditionalSurfaces.Concat(stage.WetPuddles).Concat(stage.FallingDrops).Append(coat).Distinct().ToArray();
            stage.InteriorColliders=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Collider>(true))
                .Where(c=>c!=stage.SkyTarget.GetComponent<Collider>()&&c.bounds.center.z<5
                    &&c.GetComponent<AtelierInteractionTarget>()?.Kind!=AtelierTargetKind.Painting
                    &&!c.transform.IsChildOf(flow.CanvasPort.Surface.transform.parent)
                    &&c.transform.parent!=null&&c.transform.parent.name!="细木作画架").ToArray();
            AtelierNotebookLayoutPolish.Apply(flow.References.Notebook);
            EditorUtility.SetDirty(flow.References.Notebook);
            EditorUtility.SetDirty(flow.References.Notebook.CombinationCanvas);
            // 历史试色小板不参与四阶段；它的拾取盒不能挡住新的可见湿色入口。
            foreach(var legacy in scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<AtelierInteractionTarget>(true))
                .Where(t=>t.Kind==AtelierTargetKind.Trial||t.Kind==AtelierTargetKind.TrialCloth))
                foreach(var collider in legacy.GetComponents<Collider>()){collider.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(collider);}
            // 旧湿色木板、蓝颜料与擦笔布与桌面新湿色落点重叠，只保留隐藏对象供旧母版取材质，不参与画面。
            var wetCluster=flow.References.transform.Find("湿色与整体擦除工作簇");
            if(wetCluster!=null)
                foreach(var legacyName in new[]{"沾湿色的旧木板","木纹上的蓝灰湿颜料","可以拿起的擦笔布"})
                {
                    var legacVisual=wetCluster.Find(legacyName);
                    if(legacVisual==null)continue;
                    legacVisual.gameObject.SetActive(false);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(legacVisual.gameObject);
                }
            WhiteSubmitAndHelp();
            foreach(var text in flow.References.Notebook.GetComponentsInChildren<Text>(true))if(new[] {"合成","解释","试着联系","联想"}.Contains(text.text))text.color=Color.white;
            foreach(var component in new Component[] {flow,stage,third,flow.Draft,finale,near,far,stage.RoofTarget,stage.RoomTarget,stage.SkyTarget})
            {EditorUtility.SetDirty(component);if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);}
            EditorUtility.SetDirty(coat.sharedMaterial);Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("流程整改场景保存失败。");
            AssetDatabase.SaveAssets();
        }
        /// <summary>显式构建整改副本，场景列表仅用于本次调用。</summary>
        public static void QueueBuild(){EditorApplication.update-=Build;EditorApplication.update+=Build;}
        private static void Build()
        {
            EditorApplication.update-=Build;
            const string folder="Builds/AtelierRemediation20261021";
            Directory.CreateDirectory(folder);
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出运行再构建。");
                File.WriteAllText(folder+"/构建开始.txt",DateTime.Now.ToString("O"));
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Main},locationPathName=folder+"/流程整改演示.app",
                    target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
                File.Copy("Assets/ThirdParty/NotoSans/OFL.txt",folder+"/NotoSans-OFL.txt",true);
                File.WriteAllText(folder+"/构建摘要.json",JsonUtility.ToJson(new BuildEvidence{result=report.summary.result.ToString(),
                    errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize},true));
            }
            catch(Exception error){File.WriteAllText(folder+"/构建错误.txt",error.ToString());Debug.LogException(error);}
        }
        [Serializable]private sealed class BuildEvidence{public string result;public int errors,warnings;public ulong bytes;}
        private static void WhiteSubmitAndHelp()
        {
            const string path="Assets/_Game/Content/Prefabs/UI/NotebookPanel.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var text in root.GetComponentsInChildren<Text>(true))if(new[] {"合成","解释","试着联系","联想"}.Contains(text.text))text.color=Color.white;
                var notebook=root.GetComponentInChildren<NotebookView>(true);
                if(notebook!=null&&notebook.GetComponent<AtelierNotebookHelp>()==null)notebook.gameObject.AddComponent<AtelierNotebookHelp>();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static Transform Node(Transform parent,string name)
        {var old=parent.Find(name);if(old!=null)return old;var node=new GameObject(name);node.transform.SetParent(parent,false);return node.transform;}
        private static Material Material(string name,Shader shader)
        {
            string path=Root+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}else material.shader=shader;return material;
        }
        private static Mesh Mesh(string name,float rx,float rz)
        {
            string path=Root+"/Meshes/"+name+".asset";if(!AssetDatabase.IsValidFolder(Root+"/Meshes"))AssetDatabase.CreateFolder(Root,"Meshes");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
            mesh=new Mesh{name=name};var vertices=new Vector3[49];var triangles=new int[48*3];
            for(int i=0;i<48;i++)
            {
                float angle=i*Mathf.PI*2/48;float rough=1+.09f*Mathf.Sin(i*2.73f)+.045f*Mathf.Cos(i*4.1f);
                vertices[i+1]=new Vector3(Mathf.Cos(angle)*rx*rough,0,Mathf.Sin(angle)*rz*rough);
                triangles[i*3]=0;triangles[i*3+1]=(i+1)%48+1;triangles[i*3+2]=i+1;
            }
            mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        private static Renderer Surface(Transform parent,string name,Mesh mesh,Material material)
        {
            var node=Node(parent,name);var filter=node.GetComponent<MeshFilter>();if(filter==null)filter=node.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
            var renderer=node.GetComponent<MeshRenderer>();if(renderer==null)renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;return renderer;
        }
    }
}
