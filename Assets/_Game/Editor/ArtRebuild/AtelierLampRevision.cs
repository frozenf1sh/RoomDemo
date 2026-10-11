// =============================================================================
// 文件：AtelierLampRevision.cs
// 职责：增量安装扫描油灯检视、原创火柴工作簇和三维火苗，不重建画室。
// 边界：只使用 Editor API；只保存玩法副本及本工具拥有的资产，不改正式入口。
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Unity.ArtRebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Editor.ArtRebuild
{
    /// <summary>同名拥有节点可重复更新，拒绝覆盖未保存的人工场景。</summary>
    public static class AtelierLampRevision
    {
        private const string Content = AtelierGameplayBuilder.Content;
        /// <summary>在已保存的 AtelierPlayable 上安装实体点灯切片。</summary>
        public static string Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.isDirty || scene.path != AtelierGameplayBuilder.ScenePath)
                throw new InvalidOperationException("请先打开并保存玩法副本；不能覆盖其他场景或未保存修改。");
            var game = Object.FindAnyObjectByType<AtelierGameplay>();
            var old = game.transform.Find("点灯工作簇");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = Node("点灯工作簇", game.transform);
            var lamp = GameObject.Find("独立夜间画室/返工_辅助工作簇/辅助工作台上的小油灯").transform;
            var renderer = lamp.GetComponentsInChildren<Renderer>(true).Single(x => x.name == "vintage_oil_lamp_flame");
            var wickPosition = new Vector3(renderer.bounds.center.x, renderer.bounds.min.y + .0015f, renderer.bounds.center.z);
            renderer.enabled = false;
            var glass = lamp.GetComponentsInChildren<Renderer>(true).Single(x => x.name == "vintage_oil_lamp_glass");
            // 灯罩开局已放在桌面清理，底沿落在 0.78 m 台面；点火不穿过封闭玻璃。
            Vector3 glassShift = new Vector3(-2.76f - glass.bounds.center.x, .782f - glass.bounds.min.y, -.12f - glass.bounds.center.z);
            glass.transform.position += glassShift;
            var light = GameObject.Find("独立夜间画室/照明/辅助油灯的局部暖光").GetComponent<Light>();
            light.transform.position = wickPosition + Vector3.up * .025f;
            light.color = new Color(1, .66f, .32f); light.range = 1.45f;
            light.intensity = 0; light.enabled = false; light.shadows = LightShadows.Soft;
            light.shadowBias = .025f; light.shadowNormalBias = .035f;
            var presenter = root.gameObject.AddComponent<AtelierLampPresenter>();
            presenter.LampLight = light;
            var charcoal = Lit("灯芯_碳化棉线", new Color(.035f,.027f,.019f), .05f);
            charcoal.EnableKeyword("_EMISSION"); charcoal.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(charcoal);
            var wickMesh = AtelierMeshLibrary.Lathe(new[] {new Vector2(.0016f,0),new Vector2(.0023f,.002f),new Vector2(.0019f,.007f),new Vector2(.0011f,.009f)},48);
            var wick = MeshNode("Visual_裸露棉灯芯",root,SaveMesh("裸露棉灯芯",wickMesh),charcoal);
            wick.transform.position = wickPosition - Vector3.up*.006f;
            presenter.WickSurface = wick;
            presenter.Wick = Node("灯芯接触点",root); presenter.Wick.position=wickPosition;
            var flameMaterial = new Material(Shader.Find("RoomDemo/Atelier/WickFlame"));
            flameMaterial=SaveMaterial("油灯_程序热流",flameMaterial);
            var flame = MeshNode("Visual_锚定灯芯的三维热流",root,SaveMesh("油灯热流连续曲面",FlameMesh(.0063f,.036f)),flameMaterial);
            flame.transform.position=wickPosition; presenter.LampFlame=flame;
            flame.shadowCastingMode=ShadowCastingMode.Off; flame.receiveShadows=false;
            Target("Logic_左侧油灯",root,new Vector3(-2.55f,.94f,-.10f),new Vector3(.17f,.30f,.17f),AtelierTargetKind.OilLamp,StudioRules.LampBody,"左边的油灯");
            Target("Logic_裸露灯芯",root,wickPosition+Vector3.up*.006f,new Vector3(.033f,.035f,.033f),AtelierTargetKind.Wick,StudioRules.LampBody,"灯芯");
            BuildMatches(root,presenter);
            var focus = Node("检视_左侧油灯机位",game.transform);
            focus.position=new Vector3(-2.28f,1.40f,-.97f);
            focus.LookAt(new Vector3(-2.48f,.985f,-.12f));
            game.Lamp=presenter; game.LampFocusPose=focus;
            var temporary=new GameObject("画中灯定位临时镜头");
            var camera=temporary.AddComponent<Camera>();camera.enabled=false;camera.aspect=1.28f;camera.fieldOfView=58;
            camera.transform.position=new Vector3(-.15f,1.98f,-3);camera.transform.LookAt(new Vector3(.65f,1.65f,1.6f));
            var mainLamp=GameObject.Find("独立夜间画室/返工_扫描工作簇/扫描维多利亚旧油灯");
            var mainFlame=mainLamp.GetComponentsInChildren<Renderer>(true).Single(x=>x.name=="vintage_oil_lamp_flame");
            Vector3 uv=camera.WorldToViewportPoint(mainFlame.bounds.center);
            game.Painting.LampUV=new Vector2(uv.x,uv.y);
            Object.DestroyImmediate(temporary);
            FinishPresentation(game);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return "已安装实体点灯；灯芯="+wickPosition.ToString("F4")+"，画中灯="+game.Painting.LampUV.ToString("F4");
        }

        private static void BuildMatches(Transform root,AtelierLampPresenter presenter)
        {
            var box=Node("Visual_旧纸火柴盒",root);box.position=new Vector3(-2.33f,.781f,-.245f);
            var paper=Lit("火柴盒_磨损纸板",new Color(.49f,.38f,.23f),.03f);
            var darkPaper=Lit("火柴盒_旧内衬",new Color(.26f,.19f,.11f),.02f);
            var sand=Lit("火柴盒_砂纸",new Color(.15f,.09f,.052f),.01f);
            var wood=Lit("火柴_细木杆",new Color(.59f,.43f,.24f),.08f);
            var head=Lit("火柴_褐红药头",new Color(.25f,.065f,.034f),.015f);
            // 纸盒由有厚度的套筒与拉出的空心抽屉组成，露出的火柴有实际杆和药头。
            Box("套筒顶纸",box,new Vector3(.068f,.001f,.043f),new Vector3(0,.012f,0),paper);
            Box("套筒底纸",box,new Vector3(.068f,.001f,.043f),Vector3.zero,paper);
            foreach(int sign in new[]{-1,1})
                Box("折边纸板_"+sign,box,new Vector3(.068f,.011f,.001f),new Vector3(0,.006f,sign*.021f),paper);
            var tray=Node("拉出的纸抽屉",box);tray.localPosition=new Vector3(.009f,.0012f,0);
            Box("抽屉底",tray,new Vector3(.063f,.001f,.039f),Vector3.zero,darkPaper);
            foreach(int sign in new[]{-1,1})
            {
                Box("抽屉长边_"+sign,tray,new Vector3(.063f,.008f,.0008f),new Vector3(0,.004f,sign*.0195f),darkPaper);
                Box("抽屉短边_"+sign,tray,new Vector3(.0008f,.008f,.039f),new Vector3(sign*.0315f,.004f,0),darkPaper);
            }
            var shaft=SaveMesh("火柴木杆_真实倒角",AtelierMeshLibrary.Timber(new Vector3(.043f,.0017f,.0017f),.00032f));
            var bulb=SaveMesh("火柴药头_连续曲面",AtelierMeshLibrary.Lathe(new[]{new Vector2(0,0),new Vector2(.0011f,.0006f),new Vector2(.0016f,.002f),new Vector2(.0014f,.0037f),new Vector2(0,.0044f)},32));
            for(int i=0;i<6;i++)
            {
                var stick=MeshNode("盒内火柴_"+i,tray,shaft,wood);stick.transform.localPosition=new Vector3(.004f,.003f,(i-2.5f)*.0045f);
                var cap=MeshNode("药头_"+i,tray,bulb,head);cap.transform.localPosition=stick.transform.localPosition+Vector3.left*.021f;cap.transform.localRotation=Quaternion.Euler(0,0,90);
            }
            Box("砂纸摩擦面",box,new Vector3(.057f,.007f,.0009f),new Vector3(0,.0062f,-.022f),sand);
            // 可见压纹和旧红色双线是项目原创印刷，不读取外部图像。
            var ink=Lit("火柴盒_旧红印刷",new Color(.27f,.085f,.038f),.04f);
            foreach(int sign in new[]{-1,1})
                Box("印刷边线_"+sign,box,new Vector3(.055f,.00008f,.0006f),new Vector3(0,.01256f,sign*.014f),ink);
            for(int i=0;i<13;i++)
                Box("砂纸压痕_"+i,box,new Vector3(.00036f,.006f,.0001f),new Vector3((i-6)*.0038f,.0062f,-.0225f),darkPaper);
            Target("Logic_取火柴",root,box.position+new Vector3(.008f,.0065f,0),new Vector3(.079f,.014f,.042f),AtelierTargetKind.Matches,AtelierRules.Matches,"火柴盒里的火柴");
            var strike=Target("Logic_砂纸侧面",root,box.position+new Vector3(0,.0062f,-.023f),new Vector3(.061f,.009f,.004f),AtelierTargetKind.MatchStriker,AtelierRules.Matches,"火柴盒侧面的砂纸");
            presenter.Striker=Node("砂纸接触路径",root);presenter.Striker.position=strike.position+Vector3.back*.003f;
            var held=Node("Visual_手持火柴头",root);held.position=presenter.Striker.position;
            var heldRod=MeshNode("手持木杆",held,shaft,wood);
            heldRod.transform.localRotation=Quaternion.FromToRotation(Vector3.right,new Vector3(1,-.16f,-.72f).normalized);
            heldRod.transform.localPosition=new Vector3(1,-.16f,-.72f).normalized*.021f;
            var heldHead=MeshNode("手持药头",held,bulb,head);heldHead.transform.localPosition=Vector3.down*.002f;
            presenter.MatchHead=heldHead;
            var material=new Material(Shader.Find("RoomDemo/Atelier/WickFlame"));material=SaveMaterial("火柴_程序热流",material);
            var flame=MeshNode("火柴三维小火苗",held,SaveMesh("火柴热流连续曲面",FlameMesh(.003f,.017f)),material);
            flame.shadowCastingMode=ShadowCastingMode.Off;flame.receiveShadows=false;presenter.MatchFlame=flame;
            var lamp=Node("火柴接触小光",held).gameObject.AddComponent<Light>();lamp.type=LightType.Point;lamp.color=new Color(1,.54f,.19f);lamp.range=.30f;lamp.intensity=0;lamp.enabled=false;presenter.MatchLight=lamp;
            presenter.MatchTip=held;held.gameObject.SetActive(false);
        }

        private static Mesh FlameMesh(float radius,float height)
        {
            const int rings=48,segments=32;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int y=0;y<=rings;y++)
            {
                float t=(float)y/rings;
                // 浮点 sin(pi) 可能略小于零；分数次幂前夹紧，防止顶端产生 NaN 顶点。
                float r=radius*Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.75f)*(1-t*.55f);
                for(int x=0;x<=segments;x++)
                {
                    float a=x*Mathf.PI*2/segments;
                    vertices.Add(new Vector3(Mathf.Cos(a)*r,height*t,Mathf.Sin(a)*r));
                    uv.Add(new Vector2((float)x/segments,t));
                    if(y<rings&&x<segments)
                    {int i=y*(segments+1)+x;triangles.AddRange(new[]{i,i+segments+1,i+1,i+1,i+segments+1,i+segments+2});}
                }
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        /// <summary>只更新本工具拥有的火苗网格，不重建场景或再移动玻璃罩。</summary>
        public static string RepairFlameMeshes()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlaying||scene.isDirty||scene.path!=AtelierGameplayBuilder.ScenePath)
                throw new InvalidOperationException("请先保存玩法副本；修订只更新自身视觉节点。");
            SaveMesh("油灯热流连续曲面",FlameMesh(.0063f,.036f));
            SaveMesh("火柴热流连续曲面",FlameMesh(.003f,.017f));
            var game=Object.FindAnyObjectByType<AtelierGameplay>();
            FinishPresentation(game);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "已修正两处火苗曲面的端点";
        }
        private static void FinishPresentation(AtelierGameplay game)
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Content+"Materials"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!System.IO.Path.GetFileNameWithoutExtension(path).StartsWith("火柴盒_"))continue;
                material.name=System.IO.Path.GetFileNameWithoutExtension(path);
                material.shader=Shader.Find("RoomDemo/Atelier/HandledPaper");EditorUtility.SetDirty(material);
            }
            game.LampFocusPose.LookAt(new Vector3(-2.48f,.985f,-.12f));
            var fill=Node("窗边反射落到左侧案面",game.Lamp.transform);
            fill.position=new Vector3(-2.12f,1.48f,.32f);
            var light=fill.GetComponent<Light>();if(light==null)light=fill.gameObject.AddComponent<Light>();
            light.type=LightType.Point;light.color=new Color(.45f,.62f,1);light.intensity=.24f;light.range=2.3f;
            light.shadows=LightShadows.Soft;light.shadowBias=.02f;light.shadowNormalBias=.04f;
            var box=game.Lamp.transform.Find("Visual_旧纸火柴盒");
            if(box.Find("纸盒旧印字")==null)
            {
                var print=new GameObject("纸盒旧印字",typeof(RectTransform),typeof(Canvas));
                print.transform.SetParent(box,false);print.transform.localPosition=new Vector3(0,.01265f,0);
                print.transform.localRotation=Quaternion.Euler(90,0,0);print.transform.localScale=Vector3.one*.00018f;
                var canvas=print.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                print.GetComponent<RectTransform>().sizeDelta=new Vector2(250,100);
                var ink=new GameObject("火柴中文印刷",typeof(RectTransform),typeof(UnityEngine.UI.Text));ink.transform.SetParent(print.transform,false);
                ink.GetComponent<RectTransform>().sizeDelta=new Vector2(250,100);
                var text=ink.GetComponent<UnityEngine.UI.Text>();
                text.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSans/NotoSansCJKsc-Regular.otf");
                text.text="火 柴";text.fontSize=58;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(.23f,.063f,.031f,.9f);text.raycastTarget=false;
            }
        }
        private static Transform Node(string name,Transform parent)
        {
            var existing=parent.Find(name);if(existing!=null)return existing;
            var node=new GameObject(name).transform;node.SetParent(parent,false);return node;
        }
        private static Transform Target(string name,Transform parent,Vector3 at,Vector3 size,AtelierTargetKind kind,ObjectId id,string label)
        {
            var node=Node(name,parent);node.position=at;node.gameObject.layer=2;
            var collider=node.gameObject.AddComponent<BoxCollider>();collider.size=size;
            var target=node.gameObject.AddComponent<AtelierInteractionTarget>();target.Kind=kind;target.ObjectId=id.Value;target.DisplayName=label;return node;
        }
        private static Renderer MeshNode(string name,Transform parent,Mesh mesh,Material material)
        {
            var node=Node(name,parent);node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;return renderer;
        }
        private static void Box(string name,Transform parent,Vector3 size,Vector3 at,Material material)
        {
            var renderer=MeshNode(name,parent,SaveMesh("纸盒_"+name,AtelierMeshLibrary.Timber(size,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.2f)),material);
            renderer.transform.localPosition=at;
        }
        private static Material Lit(string name,Color colour,float smoothness)
        {
            string path=Content+"Materials/"+name+".mat";
            var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old!=null)return old;
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",colour);material.SetFloat("_Smoothness",smoothness);SaveMaterial(name,material);return material;
        }
        private static Material SaveMaterial(string name,Material material)
        {
            material.name=name;
            string path=Content+"Materials/"+name+".mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(old==null){AssetDatabase.CreateAsset(material,path);return material;}
            EditorUtility.CopySerialized(material,old);Object.DestroyImmediate(material);EditorUtility.SetDirty(old);return old;
        }
        private static Mesh SaveMesh(string name,Mesh mesh)
        {
            string path=Content+"Meshes/"+name+".asset";mesh.name=name;
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;
        }
    }
}
