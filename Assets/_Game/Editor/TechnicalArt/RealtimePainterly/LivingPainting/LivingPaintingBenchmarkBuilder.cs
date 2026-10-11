// 文件：LivingPaintingBenchmarkBuilder.cs
// 职责：通过 Editor API 生成独立桌面演出与星夜水面 Hero 场景。
// 边界：复用已冻结的普通材质和 renderer，只写 LivingPainting 目录及两个新场景。
using System;
using System.Collections.Generic;
using System.IO;
using Game.Unity.TechnicalArt.RealtimePainterly;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Game.Editor.TechnicalArt.RealtimePainterly
{
    /// <summary>以两个独立场景完成状态传播和最终画面，完全不调用游戏场景生成器。</summary>
    public static class LivingPaintingBenchmarkBuilder
    {
        public const string Root="Assets/_Game/Content/TechnicalArt/LivingPainting/";
        public const string TablePath="Assets/_Game/Content/Scenes/Experiments/TechnicalArt/LivingPaintingBenchmark.unity";
        public const string HeroPath="Assets/_Game/Content/Scenes/Experiments/TechnicalArt/PainterlyFinalHero.unity";
        private const string BaseRoot="Assets/_Game/Content/TechnicalArt/RealtimePainterly/";
        private static Font font;
        private static Material celestial;

        /// <summary>保存独立实验；遇到未保存人工场景直接停止，禁止覆盖。</summary>
        [MenuItem("游戏/技术美术/生成缓慢生长与星夜实验")]
        public static void Build()
        {
            for (int i=0;i<EditorSceneManager.sceneCount;i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("存在未保存场景，生成器不会覆盖人工工作。");
            foreach (var folder in new[]{"Materials","Rendering","Profiles","Meshes"}) Directory.CreateDirectory(Root+folder);
            AssetDatabase.Refresh();
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSans/NotoSansCJKsc-Regular.otf");
            var pipeline=Pipeline();
            var profiles=Profiles();
            celestial=Material("三维星夜_顶点底色","RoomDemo/LivingPainting/CelestialPigment",Color.white);
            BuildTable(pipeline,profiles);
            BuildHero(pipeline,profiles);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(TablePath);
            Debug.Log("已保存独立 18 秒桌面演出和 Final 星夜水面；核心 Shader 保持冻结。");
        }

        private static PainterlyStyleProfile[] Profiles()
        {
            var names=new[]{"Reality","Touched","Dream","Final"};
            var labels=new[]{"现实 / Reality","触碰 / Touched","梦境 / Dream","终章 / Final"};
            var strengths=new[]{.06f,.27f,.62f,1f};
            var result=new PainterlyStyleProfile[4];
            for (int i=0;i<4;i++)
            {
                result[i]=Store(Root+"Profiles/"+names[i]+".asset",ScriptableObject.CreateInstance<PainterlyStyleProfile>());
                result[i].DisplayName=labels[i];
                result[i].PainterlyIntensity=strengths[i];
                EditorUtility.SetDirty(result[i]);
            }
            return result;
        }

        private static UniversalRenderPipelineAsset Pipeline()
        {
            var path=Root+"Rendering/LivingPaintingPipeline.asset";
            if (!AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path)) AssetDatabase.CopyAsset(BaseRoot+"Rendering/PainterlyBenchmarkPipeline.asset",path);
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            var rendererPath=Root+"Rendering/LivingPaintingRenderer.asset";
            if (!AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath)) AssetDatabase.CopyAsset(BaseRoot+"Rendering/PainterlyBenchmarkRenderer.asset",rendererPath);
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            renderer.rendererFeatures.Clear();
            var feature=Store(Root+"Rendering/LivingPaintingFeature.asset",ScriptableObject.CreateInstance<RealtimePainterlyFeature>());
            feature.Configure(Shader.Find("Hidden/RoomDemo/RealtimePainterly/Kuwahara"),Shader.Find("Hidden/RoomDemo/RealtimePainterly/WorldBrushPatches"));
            feature.ConfigureInfluence(Shader.Find("Hidden/RoomDemo/LivingPainting/InfluenceComposite"));
            renderer.rendererFeatures.Add(feature);
            renderer.SetDirty();
            var so=new SerializedObject(pipeline);
            var list=so.FindProperty("m_RendererDataList");
            list.arraySize=1;
            list.GetArrayElementAtIndex(0).objectReferenceValue=renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(feature);
            return pipeline;
        }

        private static void BuildTable(UniversalRenderPipelineAsset pipeline,PainterlyStyleProfile[] profiles)
        {
            // 读取已通过场景，另存新场景；原资源和原场景不修改。
            var scene=EditorSceneManager.OpenScene(Painterly3DBenchmarkBuilder.ScenePath);
            var old=Object.FindAnyObjectByType<Painterly3DBenchmark>();
            var root=old.gameObject;
            Object.DestroyImmediate(old);
            foreach (var canvas in Object.FindObjectsByType<Canvas>()) Object.DestroyImmediate(canvas.gameObject);
            root.name="缓慢生长实验_独立表现层";
            var camera=Camera.main;
            var paint=camera.GetComponent<PainterlyCamera>();
            paint.PainterlyIntensity=.06f;
            var benchmark=root.AddComponent<LivingPaintingBenchmark>();
            benchmark.Pipeline=pipeline;
            benchmark.View=camera;
            benchmark.Painterly=paint;
            benchmark.Profiles=profiles;
            GameObject.Find("普通球体").transform.position=new Vector3(-.47f,1.61f,-.43f);
            GameObject.Find("普通方块").transform.position=new Vector3(.47f,1.53f,-.54f);
            GameObject.Find("普通水盘").transform.position=new Vector3(1.2f,1.245f,-.14f);
            var early=GameObject.Find("普通球体").AddComponent<PainterlyInfluence>();
            early.Targets=new[]{early.GetComponent<Renderer>()};
            early.PainterlyIntensity=0;
            early.OnlyCamera=camera;
            benchmark.EarlyObject=early;
            var strokeMaterial=Material("桌面_青蓝颜料","RoomDemo/LivingPainting/Stroke",new Color(.08f,.38f,.46f));
            var path=new Vector3[129];
            for (int i=0;i<path.Length;i++)
            {
                float t=i/(float)(path.Length-1);
                path[i]=new Vector3(-1.30f+2.67f*t,1.225f,.59f+.105f*Mathf.Sin(t*5.2f)+.026f*Mathf.Sin(t*11));
            }
            var strokeNode=MeshNode(root.transform,"桌面已有笔触",Ribbon("桌面路径",path,.038f,Color.white,Color.white,true),strokeMaterial);
            var stroke=strokeNode.gameObject.AddComponent<LivingStroke>();
            stroke.Length=ArcLength(path);
            stroke.Extend=.42f;
            stroke.AutomaticTime=false;
            benchmark.Stroke=stroke;
            var protectedStroke=strokeNode.gameObject.AddComponent<PainterlyInfluence>();
            protectedStroke.Targets=new[]{strokeNode.GetComponent<Renderer>()};
            protectedStroke.PainterlyIntensity=0;
            protectedStroke.PreserveOriginal=true;
            protectedStroke.OnlyCamera=camera;
            var influences=new List<PainterlyInfluence>();
            for (int i=0;i<6;i++)
            {
                var node=new GameObject("笔触周围局部传播_"+i);
                node.transform.SetParent(root.transform,false);
                node.transform.position=path[Mathf.RoundToInt(i/5f*(path.Length-1))];
                var influence=node.AddComponent<PainterlyInfluence>();
                influence.PainterlyIntensity=0;
                influence.Radii=new Vector3(.07f,.025f,.06f);
                influence.Feather=.55f;
                influence.OnlyCamera=camera;
                influences.Add(influence);
            }
            benchmark.Regions=influences.ToArray();
            var openingMat=Material("局部墙面_笔势擦开","RoomDemo/LivingPainting/Opening",new Color(.79f,.74f,.64f));
            Object.DestroyImmediate(GameObject.Find("普通墙面"));
            var wallMesh=new Mesh { name="完整墙体_局部笔势坐标" };
            var wallVertices=new[]{new Vector3(-11,0,-1.94f),new Vector3(11,0,-1.94f),new Vector3(-11,7,-1.94f),new Vector3(11,7,-1.94f)};
            var wallUV=new Vector2[4];
            for (int i=0;i<4;i++) wallUV[i]=new Vector2(wallVertices[i].x/3.35f+.5f,(wallVertices[i].y-1.85f)/1.35f+.5f);
            wallMesh.vertices=wallVertices;wallMesh.uv=wallUV;wallMesh.triangles=new[]{0,1,2,1,3,2};wallMesh.RecalculateNormals();
            // 完整墙体遮住完整后景；不存在浮在墙前的面板边缘或后景露边。
            var panel=MeshNode(root.transform,"可被擦开的普通墙面",Store(Root+"Meshes/局部擦开的完整墙体.asset",wallMesh),openingMat);
            benchmark.Opening=panel.gameObject.AddComponent<PainterlyOpening>();
            Backdrop(root.transform,new Vector3(0,1.85f,-2.10f),3.35f,1.35f);
            Controls(root.transform,benchmark);
            EditorSceneManager.SaveScene(scene,TablePath);
        }

        private static void BuildHero(UniversalRenderPipelineAsset pipeline,PainterlyStyleProfile[] profiles)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.skybox=null;
            RenderSettings.fog=false;
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.10f,.17f,.24f);
            var root=new GameObject("Final星夜与安静水面_独立实验");
            var camera=new GameObject("星夜摄像机").AddComponent<Camera>();
            camera.transform.SetParent(root.transform,false);
            camera.tag="MainCamera";
            camera.transform.position=new Vector3(0,2.5f,-8);
            camera.transform.LookAt(new Vector3(0,5.3f,28));
            camera.fieldOfView=43;
            camera.nearClipPlane=.1f;
            camera.farClipPlane=100;
            camera.allowHDR=true;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.025f,.045f,.09f);
            var additional=camera.GetUniversalAdditionalCameraData();
            additional.renderPostProcessing=true;
            additional.antialiasing=AntialiasingMode.TemporalAntiAliasing;
            additional.taaSettings.quality=TemporalAAQuality.High;
            additional.taaSettings.baseBlendFactor=.65f;
            additional.taaSettings.jitterScale=.5f;
            additional.taaSettings.contrastAdaptiveSharpening=.12f;
            var moonlight=new GameObject("温柔月光").AddComponent<Light>();
            moonlight.transform.SetParent(root.transform,false);
            moonlight.type=LightType.Directional;
            moonlight.intensity=.75f;
            moonlight.color=new Color(.74f,.81f,1);
            moonlight.transform.rotation=Quaternion.Euler(28,-30,0);
            moonlight.shadows=LightShadows.Soft;
            RenderSettings.sun=moonlight;
            var paint=camera.gameObject.AddComponent<PainterlyCamera>();
            paint.DirectionalLight=moonlight;
            var benchmark=root.AddComponent<LivingPaintingBenchmark>();
            benchmark.Pipeline=pipeline;benchmark.View=camera;benchmark.Painterly=paint;
            benchmark.Profiles=profiles;benchmark.IsHero=true;
            benchmark.Regions=Array.Empty<PainterlyInfluence>();
            var motion=SkyGeometry(root.transform);
            benchmark.Sky=motion;
            var waterMat=Material("安静水面_真实倒影","RoomDemo/LivingPainting/QuietWater",new Color(.018f,.052f,.077f));
            var water=Primitive(root.transform,"水平静水_排除镜像递归",PrimitiveType.Plane,new Vector3(0,.15f,12),new Vector3(7,1,7),waterMat);
            water.layer=4;
            var reflect=root.AddComponent<QuietPlanarReflection>();
            reflect.Source=camera;reflect.Water=water.GetComponent<Renderer>();reflect.Height=.15f;
            reflect.VisualClock=motion;
            var stone=Material("岸边_暗灰岩石","Universal Render Pipeline/Lit",new Color(.075f,.105f,.12f));
            stone.SetFloat("_Smoothness",.06f);
            for (int i=0;i<7;i++)
            {
                float x=-10+i*1.30f;
                var rock=Primitive(root.transform,"左岸岩石_"+i,PrimitiveType.Sphere,new Vector3(x,-.10f,-1+i*.19f),new Vector3(2.15f,.55f,1.35f),stone);
                rock.transform.rotation=Quaternion.Euler(i*7,i*23,i*11);
            }
            Volume(root.transform);
            Controls(root.transform,benchmark);
            var events=new GameObject("Hero面板输入",typeof(EventSystem),typeof(InputSystemUIInputModule));
            events.transform.SetParent(root.transform,false);
            EditorSceneManager.SaveScene(scene,HeroPath);
        }

        private static QuietSkyMotion SkyGeometry(Transform parent)
        {
            var sky=new GameObject("三维星夜_画面由实时渲染生成").transform;
            sky.SetParent(parent,false);
            // 顶点色只是普通底色渐变；不携带任何预生成画作。
            var gradient=new Mesh { name="夜空普通底色" };
            var verts=new List<Vector3>();var colours=new List<Color>();var tris=new List<int>();
            for (int i=0;i<5;i++)
            {
                float y=-2+i*9;
                var c=Color.Lerp(new Color(.038f,.10f,.15f),new Color(.010f,.021f,.055f),i/4f);
                verts.Add(new Vector3(-60,y,40));verts.Add(new Vector3(60,y,40));colours.Add(c);colours.Add(c);
                if (i<4) tris.AddRange(new[]{i*2,i*2+2,i*2+1,i*2+1,i*2+2,i*2+3});
            }
            gradient.SetVertices(verts);gradient.SetColors(colours);gradient.SetTriangles(tris,0);gradient.RecalculateNormals();
            MeshNode(sky,"普通深蓝渐变底",Store(Root+"Meshes/夜空底色.asset",gradient),celestial);
            var bands=new List<Transform>();
            // 沿同一方向场布置三尺度的短笔势，先宽后窄；合并成九个网格减少绘制开销。
            // 这属于三维场景创作，冻结 renderer 继续决定画风，不引入油画纹理。
            var brushRandom=new System.Random(912);
            for (int group=0;group<9;group++)
            {
                var v=new List<Vector3>();var c=new List<Color>();var indices=new List<int>();
                int scale=group/3;
                int count=new[]{22,45,75}[scale];
                float length=new[]{5.8f,2.7f,1.10f}[scale];
                float halfWidth=new[]{.38f,.16f,.055f}[scale];
                for (int stroke=0;stroke<count;stroke++)
                {
                    float x=(float)brushRandom.NextDouble()*60-30;
                    float lane=(float)brushRandom.NextDouble();
                    float y=5.4f+lane*5.5f+1.2f*Mathf.Sin(x*.071f+lane*.8f);
                    float l=length*(.55f+(float)brushRandom.NextDouble()*.7f);
                    float w=halfWidth*(.5f+(float)brushRandom.NextDouble()*.8f);
                    float tilt=.085f*Mathf.Cos(x*.071f+lane*.8f)+(float)(brushRandom.NextDouble()-.5)*.10f;
                    float hue=(float)brushRandom.NextDouble();
                    var pigment=Color.Lerp(new Color(.038f,.102f,.16f),new Color(.065f,.153f,.196f),hue);
                    if (scale==2 && stroke%17==0) pigment=new Color(.12f,.17f,.175f);
                    var background=Color.Lerp(new Color(.032f,.081f,.134f),new Color(.025f,.061f,.112f),lane);
                    int start=v.Count;
                    const int steps=10;
                    for (int i=0;i<=steps;i++)
                    {
                        float t=i/(float)steps;
                        float dx=(t-.5f)*l;
                        float curve=.08f*Mathf.Sin(t*Mathf.PI*1.4f+stroke*.6f)*w;
                        float taper=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.42f);
                        float dry=.8f+.11f*Mathf.Sin(t*18+stroke)+.09f*Mathf.Sin(t*31+stroke*.7f);
                        for (int side=-1;side<=1;side++)
                        {
                            v.Add(new Vector3(x+dx,y+dx*tilt+curve+side*w*taper*dry,30+group*.055f));
                            float weight=side==0 ? .67f : .12f;
                            c.Add(Color.Lerp(background,pigment,weight*(.75f+.25f*Mathf.Sin(t*Mathf.PI))));
                        }
                        if (i<steps) for (int side=0;side<2;side++)
                        {
                            int n=start+i*3+side;
                            indices.AddRange(new[]{n,n+3,n+1,n+1,n+3,n+4});
                        }
                    }
                }
                var brushMesh=new Mesh { name="星夜多尺度笔势_"+group };
                brushMesh.SetVertices(v);brushMesh.SetColors(c);brushMesh.SetTriangles(indices,0);brushMesh.RecalculateNormals();
                bands.Add(MeshNode(sky,"缓慢长笔势_"+group,Store(Root+"Meshes/星夜多尺度笔势_"+group+".asset",brushMesh),celestial));
            }
            // 星点为普通固定网格。只有三颗沿共同笔势缓移，没有随机闪烁。
            var random=new System.Random(417);
            for (int i=0;i<68;i++)
            {
                float x=(float)random.NextDouble()*46-23;
                float y=3.8f+(float)random.NextDouble()*15.5f;
                float size=.025f+(float)random.NextDouble()*.057f;
                var colour=Color.Lerp(new Color(.36f,.47f,.55f),new Color(.88f,.79f,.55f),(float)random.NextDouble());
                if (i%13==0) { size*=1.45f; colour*=1.1f; }
                MeshNode(sky,"安静固定星点_"+i,Disc("星点_"+i,new Vector3(x,y,29.6f),size,colour,12),celestial);
            }
            var moonMat=Material("温暖月面_普通陶质光照","Universal Render Pipeline/Lit",new Color(.88f,.80f,.54f));
            moonMat.SetFloat("_Smoothness",.03f);
            moonMat.SetColor("_EmissionColor",new Color(.75f,.65f,.35f));
            moonMat.EnableKeyword("_EMISSION");
            Primitive(sky,"温暖的月亮",PrimitiveType.Sphere,new Vector3(-7.4f,12.8f,29),Vector3.one*1.56f,moonMat);
            // 远山剪影形成地平线，和水面共享真实透视与遮挡。
            for (int layer=0;layer<3;layer++)
            {
                var ridge=new Vector3[85];
                for (int i=0;i<ridge.Length;i++)
                {
                    float x=-45+i*90f/(ridge.Length-1);
                    ridge[i]=new Vector3(x,.50f+layer*.20f+.50f*Mathf.Sin(x*.21f+layer)+.27f*Mathf.Sin(x*.53f+layer*2),26-layer*2.2f);
                }
                var m=new Mesh { name="远山层_"+layer };
                var v=new List<Vector3>();var c=new List<Color>();var t=new List<int>();
                for (int i=0;i<ridge.Length;i++)
                {
                    v.Add(ridge[i]);v.Add(new Vector3(ridge[i].x,-1,ridge[i].z));
                    var colour=new Color(.023f-layer*.006f,.06f-layer*.014f,.084f-layer*.019f);
                    c.Add(colour);c.Add(colour*.65f);
                    if (i<ridge.Length-1) t.AddRange(new[]{i*2,i*2+2,i*2+1,i*2+1,i*2+2,i*2+3});
                }
                m.SetVertices(v);m.SetColors(c);m.SetTriangles(t,0);m.RecalculateNormals();
                MeshNode(sky,"远山剪影_"+layer,Store(Root+"Meshes/远山_"+layer+".asset",m),celestial);
            }
            var travelers=new List<Transform>();
            for (int i=0;i<3;i++) travelers.Add(MeshNode(sky,"沿笔势缓移星光_"+i,Disc("移动星光_"+i,Vector3.zero,.068f,new Color(.72f,.74f,.54f),12),celestial));
            var motion=sky.gameObject.AddComponent<QuietSkyMotion>();
            motion.Bands=bands.ToArray();motion.Travelers=travelers.ToArray();
            return motion;
        }

        private static void Backdrop(Transform parent,Vector3 centre,float width,float height)
        {
            var backCentre=new Vector3(centre.x,3.5f,centre.z);
            var path=new Vector3[]{backCentre+new Vector3(-11,-3.5f,0),backCentre+new Vector3(11,-3.5f,0),backCentre+new Vector3(-11,3.5f,0),backCentre+new Vector3(11,3.5f,0)};
            var mesh=new Mesh { name="墙后真实星夜底面" };
            mesh.vertices=path;mesh.triangles=new[]{0,2,1,1,2,3};mesh.colors=new[]{new Color(.035f,.09f,.14f),new Color(.035f,.09f,.14f),new Color(.015f,.033f,.08f),new Color(.015f,.033f,.08f)};mesh.RecalculateNormals();
            MeshNode(parent,"墙后星夜底面",Store(Root+"Meshes/墙后底面.asset",mesh),celestial);
            for (int j=0;j<3;j++)
            {
                var curve=new Vector3[61];
                for (int i=0;i<curve.Length;i++)
                {
                    float x=-width*.48f+width*.96f*i/(curve.Length-1);
                    curve[i]=centre+new Vector3(x,-.17f+j*.14f+.13f*Mathf.Sin(x*1.5f),.025f+j*.006f);
                }
                MeshNode(parent,"墙后笔势_"+j,Ribbon("墙后笔势_"+j,curve,.055f,new Color(.10f,.20f,.24f),new Color(.035f,.08f,.13f),false),celestial);
            }
            for (int i=0;i<15;i++)
            {
                float x=-width*.44f+(i*.61803f%1)*width*.88f;
                float y=-height*.34f+(i*.38196f%1)*height*.68f;
                MeshNode(parent,"墙后星点_"+i,Disc("墙后星点_"+i,centre+new Vector3(x,y,.055f),.010f+i%3*.003f,new Color(.8f,.73f,.5f),10),celestial);
            }
        }

        private static Mesh Disc(string name,Vector3 centre,float radius,Color colour,int segments)
        {
            var mesh=new Mesh { name=name };
            var vertices=new Vector3[segments+1];var colours=new Color[segments+1];var indices=new int[segments*3];
            vertices[0]=centre;colours[0]=colour;
            for (int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments;
                vertices[i+1]=centre+new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);colours[i+1]=colour;
                indices[i*3]=0;indices[i*3+1]=i+1;indices[i*3+2]=(i+1)%segments+1;
            }
            mesh.vertices=vertices;mesh.colors=colours;mesh.triangles=indices;mesh.RecalculateNormals();
            return Store(Root+"Meshes/"+name+".asset",mesh);
        }

        private static float ArcLength(Vector3[] path)
        {
            float total=0;
            for (int i=1;i<path.Length;i++) total+=Vector3.Distance(path[i-1],path[i]);
            return total;
        }

        /// <summary>弧长 UV 用米表达；三列顶点保留笔势中心和两侧宽度，不依赖相机。</summary>
        private static Mesh Ribbon(string name,Vector3[] path,float halfWidth,Color centre,Color edge,bool horizontal)
        {
            var mesh=new Mesh { name=name };
            var v=new List<Vector3>();var uv=new List<Vector2>();var colours=new List<Color>();var triangles=new List<int>();
            float arc=0;
            for (int i=0;i<path.Length;i++)
            {
                if (i>0) arc+=Vector3.Distance(path[i-1],path[i]);
                var tangent=(path[Mathf.Min(i+1,path.Length-1)]-path[Mathf.Max(i-1,0)]).normalized;
                var cross=Vector3.Cross(horizontal ? Vector3.up : Vector3.back,tangent).normalized;
                float width=halfWidth*(.84f+.11f*Mathf.Sin(i*.083f)+.05f*Mathf.Sin(i*.27f));
                for (int side=-1;side<=1;side++)
                {
                    v.Add(path[i]+cross*width*side);uv.Add(new Vector2(arc,side));colours.Add(side==0 ? centre : edge);
                }
                if (i<path.Length-1)
                    for (int side=0;side<2;side++)
                    {
                        int n=i*3+side;
                        triangles.AddRange(new[]{n,n+3,n+1,n+1,n+3,n+4});
                    }
            }
            mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetColors(colours);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();
            return Store(Root+"Meshes/"+name+".asset",mesh);
        }

        private static Transform MeshNode(Transform parent,string name,Mesh mesh,Material material)
        {
            var node=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            node.transform.SetParent(parent,false);node.GetComponent<MeshFilter>().sharedMesh=mesh;node.GetComponent<Renderer>().sharedMaterial=material;
            return node.transform;
        }
        private static GameObject Primitive(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var node=GameObject.CreatePrimitive(type);node.name=name;node.transform.SetParent(parent,false);node.transform.localPosition=position;node.transform.localScale=scale;
            node.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(node.GetComponent<Collider>());return node;
        }
        private static Material Material(string name,string shader,Color colour)
        {
            var material=Store(Root+"Materials/"+name+".mat",new Material(Shader.Find(shader)));
            material.SetColor("_BaseColor",colour);EditorUtility.SetDirty(material);return material;
        }
        private static T Store<T>(string path,T candidate) where T:Object
        {
            // 重建时保持资产名与文件名一致，避免复制材质默认 Shader 名产生导入警告。
            candidate.name=Path.GetFileNameWithoutExtension(path);
            var previous=AssetDatabase.LoadAssetAtPath<T>(path);
            if (previous==null) { AssetDatabase.CreateAsset(candidate,path);return candidate; }
            EditorUtility.CopySerialized(candidate,previous);Object.DestroyImmediate(candidate);EditorUtility.SetDirty(previous);return previous;
        }
        private static void Volume(Transform parent)
        {
            var node=new GameObject("Hero色调映射").AddComponent<Volume>();node.transform.SetParent(parent,false);node.isGlobal=true;
            var profile=Store(Root+"Rendering/HeroVolume.asset",ScriptableObject.CreateInstance<VolumeProfile>());
            // 删除仅属于此实验 Profile 的旧子资产，重复生成不累积废弃的后处理配置。
            foreach (var old in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(profile)))
                if (old is VolumeComponent) Object.DestroyImmediate(old,true);
            profile.components.Clear();
            var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);AssetDatabase.AddObjectToAsset(tone,profile);
            var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.10f);bloom.threshold.Override(1.15f);bloom.scatter.Override(.55f);AssetDatabase.AddObjectToAsset(bloom,profile);
            node.sharedProfile=profile;EditorUtility.SetDirty(profile);
        }
        private static void Controls(Transform parent,LivingPaintingBenchmark benchmark)
        {
            var node=new GameObject("独立实验中文面板",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));node.transform.SetParent(parent,false);
            benchmark.Controls=node.GetComponent<Canvas>();benchmark.Controls.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=node.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            var panel=Rect(node.transform,"底部面板",new Vector2(0,0),new Vector2(1600,96));panel.pivot=Vector2.zero;
            panel.gameObject.AddComponent<Image>().color=new Color(.035f,.065f,.08f,.94f);
            benchmark.Label=Label(panel,new Vector2(24,68),new Vector2(1120,42),benchmark.IsHero ? "终章 · 缓慢三维星夜与实时水面倒影" : "一幅画正在慢慢活过来",20);
            Label(panel,new Vector2(24,22),new Vector2(535,28),"1–4 档位  空格 冻结  R 重播  C 慢镜头  H 隐藏",17);
            Button(panel,"现实",560,benchmark.Reality);Button(panel,"触碰",680,benchmark.Touched);Button(panel,"梦境",800,benchmark.Dream);Button(panel,"终章",920,benchmark.Final);
            Button(panel,"重播 / R",1040,benchmark.Restart);
            if (benchmark.IsHero) Button(panel,"冻结 / 空格",1160,benchmark.TogglePlay);
            else Button(panel,"打开 / O",1160,benchmark.ShowOpening);
            Button(panel,benchmark.IsHero ? "桌面 / F" : "星夜 / F",1280,benchmark.SwitchScene);
        }
        private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(0,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }
        private static Text Label(Transform parent,Vector2 position,Vector2 size,string text,int fontSize)
        {
            var label=Rect(parent,"中文说明",position,size).gameObject.AddComponent<Text>();label.font=font;label.fontSize=fontSize;label.text=text;label.color=new Color(.86f,.86f,.79f);label.alignment=TextAnchor.MiddleLeft;label.raycastTarget=false;return label;
        }
        private static void Button(Transform parent,string text,float x,UnityEngine.Events.UnityAction action)
        {
            var rect=Rect(parent,text,new Vector2(x,25),new Vector2(108,33));var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.15f,.22f,.25f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;UnityEventTools.AddPersistentListener(button.onClick,action);
            Label(rect,new Vector2(6,16),new Vector2(96,31),text,17);
        }
    }
}
