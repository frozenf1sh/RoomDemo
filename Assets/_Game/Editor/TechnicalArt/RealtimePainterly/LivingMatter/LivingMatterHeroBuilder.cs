// 文件：LivingMatterHeroBuilder.cs
// 职责：生成独立的程序颜料天空与杯水静物 Hero 场景及其专用资源。
// 边界：仅写 LivingMatter 和两个新场景，不覆盖已通过的 Renderer、擦开或主房间。
using System;
using System.Collections.Generic;
using System.IO;
using Game.Unity.TechnicalArt.RealtimePainterly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace Game.Editor.TechnicalArt.RealtimePainterly
{
    /// <summary>艺术效果实验生成器；不重建正式美术场景。</summary>
    public static class LivingMatterHeroBuilder
    {
        public const string Root="Assets/_Game/Content/TechnicalArt/LivingMatter/";
        public const string SkyPath="Assets/_Game/Content/Scenes/Experiments/TechnicalArt/LivingStarryNightHero.unity";
        public const string WaterPath="Assets/_Game/Content/Scenes/Experiments/TechnicalArt/LivingWaterPigmentHero.unity";
        private const string Base="Assets/_Game/Content/TechnicalArt/RealtimePainterly/";

        /// <summary>通过 Editor API 保存专用资源；任何未保存场景都阻止生成。</summary>
        [MenuItem("游戏/技术美术/生成颜料天空与杯水 Hero")]
        public static void Build()
        {
            for(int n=0;n<EditorSceneManager.sceneCount;n++)
                if(EditorSceneManager.GetSceneAt(n).isDirty) throw new InvalidOperationException("当前存在未保存场景，Hero 生成器不会覆盖。");
            foreach(var directory in new[]{"Meshes","Materials","Rendering"}) Directory.CreateDirectory(Root+directory);
            AssetDatabase.Refresh();var pipeline=Pipeline();
            BuildSky(pipeline);BuildWater(pipeline);
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(SkyPath);
            Debug.Log("已保存独立颜料天空与杯水 Hero；冻结核心与笔势擦开保持原值。");
        }

        private static UniversalRenderPipelineAsset Pipeline()
        {
            string path=Root+"Rendering/MatterPipeline.asset",rendererPath=Root+"Rendering/MatterRenderer.asset";
            if(!AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path)) AssetDatabase.CopyAsset(Base+"Rendering/PainterlyBenchmarkPipeline.asset",path);
            if(!AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath)) AssetDatabase.CopyAsset(Base+"Rendering/PainterlyBenchmarkRenderer.asset",rendererPath);
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            renderer.rendererFeatures.Clear();
            var feature=Store(Root+"Rendering/MatterPainterlyFeature.asset",ScriptableObject.CreateInstance<RealtimePainterlyFeature>());
            feature.Configure(Shader.Find("Hidden/RoomDemo/RealtimePainterly/Kuwahara"),Shader.Find("Hidden/RoomDemo/RealtimePainterly/WorldBrushPatches"));
            renderer.rendererFeatures.Add(feature);renderer.SetDirty();
            var settings=new SerializedObject(pipeline);var list=settings.FindProperty("m_RendererDataList");
            list.arraySize=1;list.GetArrayElementAtIndex(0).objectReferenceValue=renderer;settings.ApplyModifiedPropertiesWithoutUndo();
            pipeline.supportsCameraOpaqueTexture=true;pipeline.msaaSampleCount=1;
            EditorUtility.SetDirty(pipeline);EditorUtility.SetDirty(renderer);return pipeline;
        }

        private static void BuildSky(UniversalRenderPipelineAsset pipeline)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.skybox=null;RenderSettings.fog=false;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.white;
            var root=new GameObject("星夜颜料_独立 Hero");
            var camera=Camera(root.transform,new Vector3(0,0,-10),Vector3.zero,true);
            camera.orthographicSize=10;
            var paint=camera.gameObject.AddComponent<PainterlyCamera>();
            paint.PainterlyIntensity=.90f;paint.PatchSize=.095f;paint.PatchBlend=.50f;paint.HighlightStrength=.12f;
            var plane=Node(root.transform,"被流场重绘的天空",Quad(36,20.25f),Material("活着的蓝紫颜料","RoomDemo/LivingMatter/StarryNight"));
            var pigment=plane.AddComponent<GpuPigment>();pigment.Target=plane.GetComponent<Renderer>();
            pigment.SimulationShader=Shader.Find("Hidden/RoomDemo/LivingMatter/PigmentSimulation");
            pigment.Width=1024;pigment.Height=576;pigment.Mode=0;pigment.Flow=1;
            var director=root.AddComponent<LivingMatterHero>();director.Pipeline=pipeline;director.Sky=pigment;director.View=camera;
            Volume(root.transform,true);EditorSceneManager.SaveScene(scene,SkyPath);
        }

        private static void BuildWater(UniversalRenderPipelineAsset pipeline)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.62f,.68f,.72f);RenderSettings.ambientEquatorColor=new Color(.46f,.44f,.36f);RenderSettings.ambientGroundColor=new Color(.25f,.20f,.16f);
            var root=new GameObject("杯水与颜料_独立 Hero");
            var camera=Camera(root.transform,new Vector3(2.30f,3.40f,3.30f),new Vector3(-.10f,.52f,.10f),false);
            camera.fieldOfView=34;
            camera.backgroundColor=new Color(.47f,.44f,.36f);
            var paint=camera.gameObject.AddComponent<PainterlyCamera>();paint.PainterlyIntensity=.23f;paint.HighlightStrength=.16f;
            var wood=Material("木桌_保留真实木纹","Universal Render Pipeline/Lit");
            wood.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/PolyHaven/OldWoodFloor/Diffuse.jpg"));
            wood.SetColor("_BaseColor",new Color(1,.94f,.80f));wood.SetFloat("_Smoothness",.15f);wood.SetTextureScale("_BaseMap",new Vector2(7.24f,7.5f));
            Box(root.transform,"普通木桌",new Vector3(0,-.08f,0),new Vector3(30,.16f,30),wood);
            var plaster=Lit("安静暖灰背景",new Color(.47f,.44f,.36f),.05f);
            Box(root.transform,"背景",new Vector3(0,3,-3.6f),new Vector3(18,7,.15f),plaster);
            var ceramic=Lit("象牙白陶杯",new Color(.84f,.84f,.78f),.44f);
            var blueRim=Lit("陶杯蓝绿釉边",new Color(.018f,.12f,.13f),.37f);
            var cup=Node(root.transform,"真实开口的陶杯",CupMesh(),ceramic);cup.transform.localPosition=new Vector3(-.55f,0,0);
            cup.transform.localRotation=Quaternion.Euler(0,55,0);
            var handle=Node(cup.transform,"陶杯把手",HandleMesh(),ceramic);
            Node(cup.transform,"杯口薄釉",Torus(.670f,.012f,1.185f),blueRim);
            var waterMat=Material("水与悬浮颜料","RoomDemo/LivingMatter/CupWater");
            var water=Node(root.transform,"旋转自由液面",WaterMesh(),waterMat);water.transform.position=new Vector3(-.55f,1.035f,0);
            var cupPigment=water.AddComponent<GpuPigment>();cupPigment.Target=water.GetComponent<Renderer>();
            cupPigment.Mode=1;cupPigment.Width=512;cupPigment.Height=512;cupPigment.Flow=0;
            cupPigment.SimulationShader=Shader.Find("Hidden/RoomDemo/LivingMatter/PigmentSimulation");
            var sheetMat=Material("出杯液体_薄而透明","RoomDemo/LivingMatter/CupWater");sheetMat.SetFloat("_Sheet",1);sheetMat.SetFloat("_Opacity",.68f);
            var sheet=Node(root.transform,"杯沿接触与重力流束",null,sheetMat);
            var wetMat=Material("桌面湿润与颜料沉积","RoomDemo/LivingMatter/WetPigment");
            var wet=Node(root.transform,"不规则湿膜",HorizontalQuad(2.4f,2.2f),wetMat);
            wet.transform.localPosition=new Vector3(.90f,.010f,.56f);
            var wetPigment=wet.AddComponent<GpuPigment>();wetPigment.Target=wet.GetComponent<Renderer>();
            wetPigment.Mode=2;wetPigment.Width=512;wetPigment.Height=512;wetPigment.SimulationShader=Shader.Find("Hidden/RoomDemo/LivingMatter/PigmentSimulation");
            wetPigment.Feed=cupPigment;
            var effect=root.AddComponent<LivingWaterPigment>();effect.Cup=cupPigment;effect.Wet=wetPigment;
            effect.Water=water.GetComponent<Renderer>();effect.Sheet=sheet.GetComponent<Renderer>();
            var drops=new List<Transform>();var dropMat=Material("少量真实落水滴","RoomDemo/LivingMatter/CupWater");
            dropMat.SetFloat("_Sheet",1);dropMat.SetFloat("_Opacity",.76f);
            for(int n=0;n<3;n++)
            {
                var drop=GameObject.CreatePrimitive(PrimitiveType.Sphere);drop.name="落水滴_"+n;drop.transform.SetParent(root.transform,false);
                Object.DestroyImmediate(drop.GetComponent<Collider>());drop.GetComponent<Renderer>().sharedMaterial=dropMat;drop.SetActive(false);drops.Add(drop.transform);
            }
            effect.Drops=drops.ToArray();
            var sun=new GameObject("左侧窗光").AddComponent<Light>();sun.transform.SetParent(root.transform);
            sun.type=LightType.Directional;sun.color=new Color(1,.94f,.86f);sun.intensity=2.10f;
            sun.transform.rotation=Quaternion.Euler(43,135,0);sun.shadows=LightShadows.Soft;sun.shadowStrength=.45f;sun.shadowBias=.035f;
            RenderSettings.sun=sun;paint.DirectionalLight=sun;
            var fill=new GameObject("安静的窗侧补光").AddComponent<Light>();fill.transform.SetParent(root.transform);
            fill.type=LightType.Directional;fill.color=new Color(.80f,.89f,1);fill.intensity=1.30f;fill.transform.rotation=Quaternion.Euler(28,-45,0);
            var director=root.AddComponent<LivingMatterHero>();director.Pipeline=pipeline;director.Water=effect;director.View=camera;
            Volume(root.transform,false);EditorSceneManager.SaveScene(scene,WaterPath);
        }

        private static Camera Camera(Transform parent,Vector3 position,Vector3 target,bool orthographic)
        {
            var camera=new GameObject("固定 Hero 摄像机").AddComponent<Camera>();camera.transform.SetParent(parent,false);
            camera.tag="MainCamera";camera.transform.position=position;camera.transform.LookAt(target);
            camera.orthographic=orthographic;camera.nearClipPlane=.05f;camera.farClipPlane=50;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.018f,.029f,.052f);camera.allowHDR=true;
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;
            data.requiresDepthOption=CameraOverrideOption.On;data.requiresColorOption=CameraOverrideOption.On;
            data.antialiasing=AntialiasingMode.TemporalAntiAliasing;data.taaSettings.quality=TemporalAAQuality.High;
            data.taaSettings.baseBlendFactor=.60f;data.taaSettings.jitterScale=.5f;return camera;
        }

        private static Mesh CupMesh()
        {
            var profile=new[]{new Vector2(.37f,0),new Vector2(.45f,.05f),new Vector2(.53f,.20f),new Vector2(.61f,.55f),
                new Vector2(.665f,1.04f),new Vector2(.687f,1.15f),new Vector2(.683f,1.185f),new Vector2(.651f,1.19f),
                new Vector2(.637f,1.15f),new Vector2(.619f,1.02f),new Vector2(.566f,.55f),new Vector2(.495f,.20f),new Vector2(.41f,.10f),new Vector2(0,.10f)};
            return Lathe(profile,"开口陶杯");
        }

        private static Mesh Lathe(Vector2[] profile,string name)
        {
            const int slices=192;var v=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int y=0;y<profile.Length;y++) for(int a=0;a<=slices;a++)
            {
                float angle=a*Mathf.PI*2/slices;v.Add(new Vector3(Mathf.Cos(angle)*profile[y].x,profile[y].y,Mathf.Sin(angle)*profile[y].x));
                uv.Add(new Vector2((float)a/slices,profile[y].y));
                if(y==profile.Length-1 || a==slices) continue;
                int n=y*(slices+1)+a;triangles.AddRange(new[]{n,n+slices+1,n+1,n+1,n+slices+1,n+slices+2});
            }
            return Mesh(name,v,uv,triangles);
        }

        private static Mesh Torus(float radius,float tube,float y)
        {
            var profile=new Vector2[17];for(int i=0;i<profile.Length;i++)
            { float a=i*2*Mathf.PI/(profile.Length-1);profile[i]=new Vector2(radius+Mathf.Cos(a)*tube,y+Mathf.Sin(a)*tube); }
            return Lathe(profile,"细陶釉边");
        }

        private static Mesh HandleMesh()
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();const int length=64,cross=16;
            for(int n=0;n<=length;n++)
            {
                float a=n*Mathf.PI/length;
                Vector3 centre=new Vector3(-.61f-.35f*Mathf.Sin(a),.69f+.39f*Mathf.Cos(a),0);
                Vector3 across=new Vector3(-Mathf.Sin(a),Mathf.Cos(a),0).normalized;
                for(int c=0;c<=cross;c++)
                {
                    float b=c*2*Mathf.PI/cross;v.Add(centre+(across*Mathf.Cos(b)+Vector3.forward*Mathf.Sin(b))*.067f);uv.Add(new Vector2((float)n/length,(float)c/cross));
                    if(n==length || c==cross) continue;int i=n*(cross+1)+c;
                    triangles.AddRange(new[]{i,i+1,i+cross+1,i+1,i+cross+2,i+cross+1});
                }
            }
            return Mesh("圆润杯把",v,uv,triangles);
        }

        private static Mesh WaterMesh()
        {
            const int rings=48,slices=128;var v=new List<Vector3>{Vector3.zero};var uv=new List<Vector2>{Vector2.one*.5f};var t=new List<int>();
            for(int r=1;r<=rings;r++) for(int a=0;a<slices;a++)
            {
                float radius=(float)r/rings*.628f,angle=a*2*Mathf.PI/slices;
                var p=new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);v.Add(p);uv.Add(new Vector2(p.x/1.28f+.5f,p.z/1.28f+.5f));
                int current=1+(r-1)*slices+a,next=1+(r-1)*slices+(a+1)%slices;
                if(r==1) t.AddRange(new[]{0,next,current});
                else { int old=current-slices,oldNext=next-slices;t.AddRange(new[]{old,next,current,old,oldNext,next}); }
            }
            return Mesh("有径向分辨率的自由液面",v,uv,t);
        }

        private static Mesh Quad(float width,float height)
        {
            return Mesh("天空画面载体",new List<Vector3>{new Vector3(-width/2,-height/2,0),new Vector3(width/2,-height/2,0),new Vector3(-width/2,height/2,0),new Vector3(width/2,height/2,0)},
                new List<Vector2>{Vector2.zero,Vector2.right,Vector2.up,Vector2.one},new List<int>{0,2,1,1,2,3});
        }
        private static Mesh HorizontalQuad(float width,float depth)
        {
            return Mesh("桌面湿膜载体",new List<Vector3>{new Vector3(-width/2,0,-depth/2),new Vector3(width/2,0,-depth/2),new Vector3(-width/2,0,depth/2),new Vector3(width/2,0,depth/2)},
                new List<Vector2>{Vector2.zero,Vector2.right,Vector2.up,Vector2.one},new List<int>{0,2,1,1,2,3});
        }
        private static Mesh Mesh(string name,List<Vector3> vertices,List<Vector2> uv,List<int> triangles)
        {
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return Store(Root+"Meshes/"+name+".asset",mesh);
        }
        private static GameObject Node(Transform parent,string name,Mesh mesh,Material material)
        {
            var node=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));node.transform.SetParent(parent,false);
            node.GetComponent<MeshFilter>().sharedMesh=mesh;node.GetComponent<Renderer>().sharedMaterial=material;return node;
        }
        private static void Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var node=GameObject.CreatePrimitive(PrimitiveType.Cube);node.name=name;node.transform.SetParent(parent,false);node.transform.localPosition=position;node.transform.localScale=scale;
            node.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(node.GetComponent<Collider>());
        }
        private static Material Material(string name,string shader)
        { return Store(Root+"Materials/"+name+".mat",new Material(Shader.Find(shader))); }
        private static Material Lit(string name,Color colour,float smoothness)
        { var mat=Material(name,"Universal Render Pipeline/Lit");mat.SetColor("_BaseColor",colour);mat.SetFloat("_Smoothness",smoothness);EditorUtility.SetDirty(mat);return mat; }
        private static T Store<T>(string path,T candidate) where T:Object
        {
            candidate.name=Path.GetFileNameWithoutExtension(path);var previous=AssetDatabase.LoadAssetAtPath<T>(path);
            if(previous==null) { AssetDatabase.CreateAsset(candidate,path);return candidate; }
            EditorUtility.CopySerialized(candidate,previous);Object.DestroyImmediate(candidate);EditorUtility.SetDirty(previous);return previous;
        }
        private static void Volume(Transform parent,bool sky)
        {
            string path=Root+"Rendering/"+(sky ? "SkyVolume.asset" : "WaterVolume.asset");
            var profile=Store(path,ScriptableObject.CreateInstance<VolumeProfile>());
            foreach(var old in AssetDatabase.LoadAllAssetsAtPath(path)) if(old is VolumeComponent) Object.DestroyImmediate(old,true);
            profile.components.Clear();var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);AssetDatabase.AddObjectToAsset(tone,profile);
            var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(sky ? .18f : .04f);bloom.threshold.Override(sky ? .7f : 1.2f);bloom.scatter.Override(.55f);AssetDatabase.AddObjectToAsset(bloom,profile);
            var node=new GameObject("Hero色彩与微光").AddComponent<Volume>();node.transform.SetParent(parent,false);node.isGlobal=true;node.sharedProfile=profile;
            EditorUtility.SetDirty(profile);
        }
    }
}
