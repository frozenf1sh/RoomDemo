// =============================================================================
// 文件：AtelierCurtainRevision.cs
// 职责：向已保存的玩法副本增量安装铜环、亚麻吊带和拉绳，并采集拉帘后的画中色层。
// 边界：仅 Editor API 修改场景；不重建认可美术场景，不改变入口和第三方资产。
// =============================================================================
using System;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Unity.ArtRebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Editor.ArtRebuild
{
    /// <summary>安装本工具拥有的窗帘工作簇，保留既有布料材质、长度与场景构图。</summary>
    public static class AtelierCurtainRevision
    {
        private const string Content = AtelierGameplayBuilder.Content;
        /// <summary>只保存干净的 AtelierPlayable 副本；连续调用替换自身节点。</summary>
        public static string Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.isDirty || scene.path != AtelierGameplayBuilder.ScenePath)
                throw new InvalidOperationException("请先打开并保存玩法副本；拒绝覆盖其他场景或未保存修改。");
            var game = Object.FindAnyObjectByType<AtelierGameplay>();
            var art = GameObject.Find("独立夜间画室").transform;
            var window = art.Find("建筑与木作/旧窗与亚麻帘");
            var old = game.transform.Find("拉帘工作簇");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = Node("拉帘工作簇", game.transform);
            var presenter = root.gameObject.AddComponent<AtelierCurtainPresenter>();
            presenter.Cloth = new[] { window.Find("亚麻帘褶_0").GetComponent<MeshFilter>(), window.Find("亚麻帘褶_1").GetComponent<MeshFilter>() };
            foreach(var cloth in presenter.Cloth) GameObjectUtility.SetStaticEditorFlags(cloth.gameObject,0);
            foreach(var collider in game.GetComponentsInChildren<Collider>(true))
                if(collider.name.StartsWith("遮挡_亚麻帘褶_")||collider.name.StartsWith("遮挡_帘环_")||collider.name=="遮挡_旧铜帘杆")collider.enabled=false;
            var copper = window.Find("旧铜帘杆").GetComponent<Renderer>().sharedMaterial;
            var linen = presenter.Cloth[0].GetComponent<Renderer>().sharedMaterial;
            foreach (var renderer in window.GetComponentsInChildren<Renderer>(true))
                if (renderer.name.StartsWith("帘环_") || renderer.name == "旧铜帘杆") renderer.enabled = false;
            // 铜杆沿 X 轴贯穿全部挂点，原有铜环的轴向在副本中修正。
            var rod = MeshNode("贯穿吊环的旧铜杆", root, SaveMesh("铜帘杆_连续圆截面", AtelierMeshLibrary.Lathe(new[] { new Vector2(.016f,0),new Vector2(.018f,.03f),new Vector2(.016f,4.64f),new Vector2(.018f,4.67f) },48)), copper);
            rod.transform.position = new Vector3(-.99f,3.75f,2.06f); rod.transform.rotation = Quaternion.Euler(0,0,-90);
            presenter.Rings = new Transform[16]; presenter.Straps = new MeshFilter[16];
            presenter.PickingCloth = new MeshCollider[2];
            var ring = SaveMesh("铜帘环_封闭曲面", Ring());
            var strap = new Mesh { name = "亚麻吊带_双面" };
            strap.vertices = new[] { new Vector3(-.009f,3.723f,2.06f),new Vector3(.009f,3.723f,2.06f),new Vector3(-.009f,3.65f,2.05f),new Vector3(.009f,3.65f,2.05f) };
            strap.uv = new[] { Vector2.zero,Vector2.right,Vector2.up,Vector2.one };
            strap.triangles = new[] {0,1,2,1,3,2}; strap.RecalculateNormals();strap.RecalculateBounds();
            strap = SaveMesh("亚麻吊带_连续连接",strap);
            for (int side = 0; side < 2; side++)
            {
                var vertices = presenter.Cloth[side].sharedMesh.vertices;
                var uv = presenter.Cloth[side].sharedMesh.uv;
                for (int j = 0; j < 8; j++)
                {
                    int i = side*8+j; var at = presenter.Cloth[side].transform.TransformPoint(vertices[AtelierCurtainPresenter.TopIndex(uv,j)]);
                    presenter.Rings[i] = MeshNode("铜吊环_"+i,root,ring,copper).transform;
                    presenter.Rings[i].position = new Vector3(at.x,3.75f,2.06f);
                    var band = MeshNode("亚麻连接带_"+i,root,strap,linen);
                    band.transform.position = new Vector3(at.x,0,0); presenter.Straps[i] = band.GetComponent<MeshFilter>();
                }
                var logic=Node("Logic_窗帘_"+side,root);logic.gameObject.layer=2;
                logic.SetPositionAndRotation(presenter.Cloth[side].transform.position+Vector3.back*.004f,presenter.Cloth[side].transform.rotation);
                logic.localScale=presenter.Cloth[side].transform.lossyScale;
                presenter.PickingCloth[side]=logic.gameObject.AddComponent<MeshCollider>();presenter.PickingCloth[side].sharedMesh=presenter.Cloth[side].sharedMesh;
                var picking=logic.gameObject.AddComponent<AtelierInteractionTarget>();picking.Kind=AtelierTargetKind.Curtain;picking.ObjectId=StudioRules.Curtain.Value;picking.DisplayName="窗帘";
            }
            var guide = MeshNode("拉绳的固定导向铜环",root,ring,copper);
            guide.transform.position = new Vector3(-1.11f,3.70f,1.88f); guide.transform.rotation = Quaternion.Euler(0,90,0);
            var bracket = MeshNode("导向环到窗边木作的铜支座",root,SaveMesh("拉绳铜支座_连续圆截面",AtelierMeshLibrary.Lathe(new[] {new Vector2(.011f,0),new Vector2(.008f,.025f),new Vector2(.008f,.31f),new Vector2(.025f,.33f)},40)),copper);
            bracket.transform.position = guide.transform.position;bracket.transform.rotation=Quaternion.Euler(90,0,0);
            presenter.Cord = Node("绕过导向环的闭合亚麻绳",root).gameObject.AddComponent<LineRenderer>();
            presenter.Cord.sharedMaterial = linen; presenter.Cord.useWorldSpace = true;
            presenter.Cord.widthMultiplier = .0048f; presenter.Cord.numCapVertices = 4; presenter.Cord.numCornerVertices = 4;
            presenter.Cord.positionCount = 49;
            for (int i=0;i<=48;i++) { float a=i/48f*Mathf.PI*2;presenter.Cord.SetPosition(i,new Vector3(-1.11f+Mathf.Sin(a)*.021f,2.62f+Mathf.Cos(a)*1.08f,1.88f)); }
            presenter.Pull = MeshNode("绳结上的旧铜拉坠",root,SaveMesh("窗帘铜拉坠_车制曲面",AtelierMeshLibrary.Lathe(new[] {new Vector2(.004f,0),new Vector2(.012f,.008f),new Vector2(.011f,.036f),new Vector2(.004f,.046f)},48)),copper).transform;
            presenter.Pull.position = new Vector3(-1.11f,1.52f,1.88f);
            Target("Logic_窗帘拉绳",root,new Vector3(-1.11f,2.4f,1.82f),new Vector3(.10f,1.8f,.06f),AtelierTargetKind.CurtainCord,"窗帘拉绳");
            var focus = Node("检视_窗帘机位",game.transform);
            focus.position = new Vector3(1.28f,2.35f,-1.30f);focus.LookAt(new Vector3(1.28f,2.28f,2.06f));
            game.Curtain = presenter;game.CurtainFocusPose = focus;
            BakeOpenPainting(art,game.Painting,presenter);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            return "已安装拉帘交互、16 枚受约束铜环及现有亚麻网格收束；已采集画中窗边补笔。";
        }
        private static void BakeOpenPainting(Transform art,AtelierPaintingPresenter painting,AtelierCurtainPresenter curtain)
        {
            var source = Camera.main;var temporary = Object.Instantiate(source.gameObject);var camera = temporary.GetComponent<Camera>();camera.enabled = false;
            camera.transform.position = new Vector3(-.15f,1.98f,-3);camera.transform.LookAt(new Vector3(.65f,1.65f,1.6f));camera.fieldOfView=58;camera.aspect=1.28f;
            var easel = art.Find("室内家具/细木作画架");bool wasActive=easel.gameObject.activeSelf;
            var meshes=curtain.Cloth.Select(x=>x.sharedMesh).ToArray();var clones=new Mesh[2];
            var target=RenderTexture.GetTemporary(1024,800,24,RenderTextureFormat.ARGB32);var oldActive=RenderTexture.active;
            Texture2D capture=null,result=null;
            try
            {
                for(int side=0;side<2;side++)
                {
                    clones[side]=Object.Instantiate(meshes[side]);var vertices=meshes[side].vertices;var uv=meshes[side].uv;
                    var edges=AtelierCurtainPresenter.OuterEdges(vertices,uv,side);
                    var output=new Vector3[vertices.Length];
                    for(int i=0;i<vertices.Length;i++) output[i]=AtelierCurtainPresenter.Gather(vertices[i],edges[i],uv[i],side,6);
                    clones[side].vertices=output;clones[side].RecalculateNormals();clones[side].RecalculateBounds();curtain.Cloth[side].sharedMesh=clones[side];
                    for(int j=0;j<8;j++)
                    {
                        int index=side*8+j;var anchor=curtain.Cloth[side].transform.TransformPoint(output[AtelierCurtainPresenter.TopIndex(uv,j)]);
                        curtain.Rings[index].position=new Vector3(anchor.x,3.75f,2.06f);
                        curtain.Straps[index].transform.position=new Vector3(anchor.x,0,0);
                    }
                }
                easel.gameObject.SetActive(false);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                capture=new Texture2D(1024,800,TextureFormat.RGB24,false);capture.ReadPixels(new Rect(0,0,1024,800),0,0);capture.Apply();
                result=AtelierGameplayBuilder.Repaint(capture);
                string path=Content+"Textures/任务画_拉帘后现场短笔触.png";File.WriteAllBytes(path,result.EncodeToPNG());AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
                var material=painting.Surface.sharedMaterial;material.SetTexture("_OpenMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                var bottom=camera.WorldToViewportPoint(new Vector3(-1.2f,.80f,2.06f));var top=camera.WorldToViewportPoint(new Vector3(3.95f,3.85f,2.06f));
                material.SetVector("_OpenRegion",new Vector4((bottom.x+top.x)*.5f,(bottom.y+top.y)*.5f,Mathf.Abs(top.x-bottom.x)*.53f,Mathf.Abs(top.y-bottom.y)*.53f));EditorUtility.SetDirty(material);
            }
            finally
            {
                for(int i=0;i<2;i++)
                {
                    curtain.Cloth[i].sharedMesh=meshes[i];if(clones[i]!=null)Object.DestroyImmediate(clones[i]);
                    var vertices=meshes[i].vertices;
                    var uv=meshes[i].uv;
                    for(int j=0;j<8;j++) {var at=curtain.Cloth[i].transform.TransformPoint(vertices[AtelierCurtainPresenter.TopIndex(uv,j)]);curtain.Rings[i*8+j].position=new Vector3(at.x,3.75f,2.06f);curtain.Straps[i*8+j].transform.position=new Vector3(at.x,0,0);}
                }
                easel.gameObject.SetActive(wasActive);RenderTexture.active=oldActive;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);
                if(capture!=null)Object.DestroyImmediate(capture);if(result!=null)Object.DestroyImmediate(result);Object.DestroyImmediate(temporary);
            }
        }
        private static Mesh Ring()
        {
            const int around=48,tube=12;var vertices=new Vector3[(around+1)*(tube+1)];var uv=new Vector2[vertices.Length];var triangles=new int[around*tube*6];int k=0;
            for(int i=0;i<=around;i++)for(int j=0;j<=tube;j++)
            {
                float a=i/(float)around*Mathf.PI*2,b=j/(float)tube*Mathf.PI*2;int index=i*(tube+1)+j;
                float r=.026f+Mathf.Cos(b)*.0025f;vertices[index]=new Vector3(Mathf.Sin(b)*.0025f,Mathf.Cos(a)*r,Mathf.Sin(a)*r);uv[index]=new Vector2(i/(float)around,j/(float)tube);
                if(i<around&&j<tube){triangles[k++]=index;triangles[k++]=index+tube+1;triangles[k++]=index+1;triangles[k++]=index+1;triangles[k++]=index+tube+1;triangles[k++]=index+tube+2;}
            }
            var mesh=new Mesh();mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static Transform Node(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        private static Renderer MeshNode(string name,Transform parent,Mesh mesh,Material material){var t=Node(name,parent);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;return r;}
        private static Mesh SaveMesh(string name,Mesh mesh){string path=Content+"Meshes/"+name+".asset";mesh.name=name;var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);return old;}
        private static void Target(string name,Transform parent,Vector3 at,Vector3 size,AtelierTargetKind kind,string label){var t=Node(name,parent);t.position=at;t.gameObject.layer=2;t.gameObject.AddComponent<BoxCollider>().size=size;var target=t.gameObject.AddComponent<AtelierInteractionTarget>();target.Kind=kind;target.ObjectId=StudioRules.Curtain.Value;target.DisplayName=label;}
    }
}
