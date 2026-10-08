// 文件：ProductionResourceBuilder.cs
// 职责：通过 Editor API 生成隔离的生产资源、可直接拖入的 Prefab 与接入 Benchmark。
// 边界：只写 Production 目录；不打开、生成或覆盖正式画室和已有 Hero 场景。
using System;
using System.IO;
using Game.Unity.TechnicalArt.RealtimePainterly;
using Game.Unity.TechnicalArt.RealtimePainterly.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Editor.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>独立资源生成与明确选择的 Renderer 安装工具。</summary>
    public static class ProductionResourceBuilder
    {
        public const string Root = "Assets/_Game/Content/TechnicalArt/Production/";
        public const string ScenePath = Root + "Scenes/TechnicalArtProductionBenchmark.unity";
        private const string Matter = "Assets/_Game/Content/TechnicalArt/LivingMatter/";
        private const string Painting = "Assets/_Game/Content/TechnicalArt/LivingPainting/";
        private const string Core = "Assets/_Game/Content/TechnicalArt/RealtimePainterly/";

        /// <summary>只生成独立资源，任何未保存场景都会阻止本次操作。</summary>
        [MenuItem("游戏/技术美术/生产接入/生成独立资源与 Benchmark")]
        public static void Build()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("存在未保存场景，生产资源生成已停止。");
            foreach (string d in new[] { "Prefabs", "Profiles", "Rendering", "Materials", "Scenes" }) Directory.CreateDirectory(Root + d);
            AssetDatabase.Refresh();
            Profiles();
            var pipeline = Pipeline();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreatePrefabs();
            Benchmark(pipeline);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("生产接入资源已生成；仅写 Production 目录，核心算法与正式画室未修改。");
        }

        private static void Profiles()
        {
            foreach (string name in new[] { "Reality", "Touched", "Dream", "Final" })
                Copy(Painting + "Profiles/" + name + ".asset", Root + "Profiles/" + name + ".asset");
        }

        private static RealtimePainterlyFeature Feature()
        {
            var feature = Store(Root + "Rendering/ProductionPainterlyFeature.asset", ScriptableObject.CreateInstance<RealtimePainterlyFeature>());
            ConfigureFeature(feature);
            EditorUtility.SetDirty(feature);
            AssetDatabase.SaveAssetIfDirty(feature);
            return feature;
        }

        private static UniversalRenderPipelineAsset Pipeline()
        {
            string path = Root + "Rendering/ProductionPipeline.asset", rendererPath = Root + "Rendering/ProductionRenderer.asset";
            Copy(Matter + "Rendering/MatterPipeline.asset", path);
            Copy(Matter + "Rendering/MatterRenderer.asset", rendererPath);
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            renderer.rendererFeatures.Clear(); renderer.rendererFeatures.Add(Feature()); renderer.SetDirty();
            var settings = new SerializedObject(asset); var list = settings.FindProperty("m_RendererDataList");
            list.arraySize = 1; list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            settings.ApplyModifiedPropertiesWithoutUndo();
            asset.supportsCameraOpaqueTexture = true; asset.msaaSampleCount = 1;
            EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>供主开发显式执行的一次性接入：保留所选 Renderer 的其他 Feature，不修改 Scene。</summary>
        [MenuItem("游戏/技术美术/生产接入/给所选 URP Renderer 安装绘画 Feature")]
        public static void InstallSelectedRenderer()
        {
            var renderer = Selection.activeObject as UniversalRendererData;
            if (renderer == null) throw new InvalidOperationException("请在 Project 中选择主摄像机实际使用的 Universal Renderer Data。");
            var feature = renderer.rendererFeatures.Find(x => x is RealtimePainterlyFeature) as RealtimePainterlyFeature;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<RealtimePainterlyFeature>();
                feature.name = "统一绘画渲染";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            ConfigureFeature(feature);
            renderer.SetDirty(); EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssetIfDirty(renderer);
        }

        private static void ConfigureFeature(RealtimePainterlyFeature feature)
        {
            // 直接保存资产引用，避免首次导入或域重载时 Shader.Find 缓存尚未完整。
            feature.Configure(RequiredShader(Core + "Shaders/AnisotropicKuwahara.shader"),
                RequiredShader(Core + "Shaders/WorldBrushPatches.shader"));
            feature.ConfigureInfluence(RequiredShader(Painting + "Shaders/InfluenceComposite.shader"));
        }

        private static Shader RequiredShader(string path)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null) throw new InvalidOperationException("缺少必要绘画 Shader，生成已停止：" + path);
            return shader;
        }

        /// <summary>供主开发显式执行：配置所选摄像机，不修改其他对象或游戏逻辑。</summary>
        [MenuItem("游戏/技术美术/生产接入/给所选摄像机安装控制组件")]
        public static void InstallSelectedCamera()
        {
            var node = Selection.activeGameObject;
            var camera = node != null ? node.GetComponent<Camera>() : null;
            if (camera == null) throw new InvalidOperationException("请在 Hierarchy 中选择需要绘画化的摄像机。");
            Undo.RecordObject(camera, "配置绘画摄像机");
            ConfigureCamera(camera);
            var paint = camera.GetComponent<PainterlyCamera>();
            if (paint == null) paint = Undo.AddComponent<PainterlyCamera>(node);
            paint.DirectionalLight = RenderSettings.sun;
            var style = camera.GetComponent<PainterlyStyleController>();
            if (style == null) style = Undo.AddComponent<PainterlyStyleController>(node);
            AssignProfiles(style);
            EditorUtility.SetDirty(camera); EditorUtility.SetDirty(paint); EditorUtility.SetDirty(style);
            // 保存由主开发自行执行，避免安装工具覆盖未保存的人工美术。
        }

        private static void CreatePrefabs()
        {
            var rig = new GameObject("绘画摄像机_生产组件");
            var camera = rig.AddComponent<Camera>(); camera.tag = "MainCamera"; ConfigureCamera(camera);
            var painterly = rig.AddComponent<PainterlyCamera>(); painterly.PainterlyIntensity = .06f;
            AssignProfiles(rig.AddComponent<PainterlyStyleController>());
            SavePrefab(rig, "PainterlyCameraRig");

            var sky = new GameObject("局部至完整星夜_生产组件");
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.name = "世界方向天空球";
            sphere.transform.SetParent(sky.transform, false); sphere.transform.localScale = Vector3.one * 90;
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            var skyMaterial = Store(Root + "Materials/LivingSkySurface.mat", new Material(RequiredShader(Root + "Shaders/LivingSkySurface.shader")));
            var skyRenderer = sphere.GetComponent<Renderer>(); skyRenderer.sharedMaterial = skyMaterial;
            skyRenderer.shadowCastingMode = ShadowCastingMode.Off; skyRenderer.receiveShadows = false;
            var pigment = Pigment(sphere, 0, skyRenderer, 1024, 576);
            var protection = sphere.AddComponent<PainterlyInfluence>(); protection.Targets = new[] { skyRenderer }; protection.PainterlyIntensity = 0; protection.PreserveOriginal = true;
            var skyControl = sky.AddComponent<LivingSkyController>(); skyControl.Pigment = pigment; skyControl.Surface = skyRenderer; skyControl.Protection = protection;
            SavePrefab(sky, "LivingSky");

            var waterRoot = new GameObject("杯水与湿痕_生产组件");
            var cup = Node(waterRoot.transform, "开口陶杯", MatterMesh("开口陶杯"), MatterMaterial("象牙白陶杯"));
            cup.transform.localPosition = new Vector3(-.55f, 0, 0); cup.transform.localRotation = Quaternion.Euler(0, 55, 0);
            Node(cup.transform, "杯把", MatterMesh("圆润杯把"), MatterMaterial("象牙白陶杯"));
            Node(cup.transform, "薄釉杯沿", MatterMesh("细陶釉边"), MatterMaterial("陶杯蓝绿釉边"));
            var water = Node(waterRoot.transform, "自由液面", MatterMesh("有径向分辨率的自由液面"), MatterMaterial("水与悬浮颜料"));
            water.transform.localPosition = new Vector3(-.55f, 1.035f, 0);
            var cupPigment = Pigment(water.gameObject, 1, water, 512, 512);
            var stream = Node(waterRoot.transform, "有圆截面的落水", null, MatterMaterial("出杯液体_薄而透明"));
            stream.enabled = false;
            var wet = Node(waterRoot.transform, "湿痕_保留木纹", MatterMesh("桌面湿膜载体"), MatterMaterial("桌面湿润与颜料沉积"));
            wet.transform.localPosition = new Vector3(.90f, .010f, .56f);
            var wetPigment = Pigment(wet.gameObject, 2, wet, 512, 512); wetPigment.Feed = cupPigment;
            var lip = Child(waterRoot.transform, "杯沿接触点", new Vector3(.07f, 1.17f, .29f));
            var landing = Child(waterRoot.transform, "桌面落水点", new Vector3(.47f, .009f, .52f));
            var drops = new Transform[3];
            for (int i = 0; i < drops.Length; i++)
            {
                var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere); drop.name = "少量落水滴_" + i;
                drop.transform.SetParent(waterRoot.transform, false); Object.DestroyImmediate(drop.GetComponent<Collider>());
                drop.GetComponent<Renderer>().sharedMaterial = MatterMaterial("少量真实落水滴");
                drop.SetActive(false); drops[i] = drop.transform;
            }
            var waterControl = waterRoot.AddComponent<LivingWaterController>();
            waterControl.Cup = cupPigment; waterControl.Wet = wetPigment; waterControl.Water = water; waterControl.Stream = stream;
            waterControl.LipAnchor = lip; waterControl.LandingAnchor = landing; waterControl.Drops = drops;
            SavePrefab(waterRoot, "LivingWaterCup");

            var local = new GameObject("局部绘画_生产组件");
            local.AddComponent<PainterlyInfluence>().PainterlyIntensity = 0;
            local.AddComponent<PainterlyLocalController>(); SavePrefab(local, "PainterlyLocalInfluence");

            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad); panel.name = "笔势擦开_生产组件";
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.GetComponent<Renderer>().sharedMaterial = CloneMaterial(Painting + "Materials/局部墙面_笔势擦开.mat", "RewriteWall", "RoomDemo/LivingPainting/Opening");
            panel.transform.localRotation = Quaternion.Euler(0,180,0);
            panel.AddComponent<PainterlyOpening>(); panel.AddComponent<PainterlyRewriteController>();
            SavePrefab(panel, "PainterlyRewritePanel");
        }

        private static void Benchmark(UniversalRenderPipelineAsset pipeline)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.62f,.68f,.72f); RenderSettings.ambientEquatorColor = new Color(.46f,.44f,.36f); RenderSettings.ambientGroundColor = new Color(.25f,.20f,.16f);
            var day = Store(Root + "Materials/RealitySky.mat", new Material(Shader.Find("Skybox/Procedural")));
            day.SetFloat("_AtmosphereThickness", .7f); RenderSettings.skybox = day;
            var root = new GameObject("生产接入 Benchmark_无游戏路线");
            var scope = root.AddComponent<ProductionBenchmark>(); scope.Pipeline = pipeline;
            var rig = Instantiate("PainterlyCameraRig", root.transform); var camera = rig.GetComponent<Camera>();
            camera.transform.position = new Vector3(3.1f,3.4f,5.2f); camera.transform.LookAt(new Vector3(-.15f,1.1f,-.25f)); camera.fieldOfView = 48;
            scope.View = camera; scope.Style = rig.GetComponent<PainterlyStyleController>();
            var sky = Instantiate("LivingSky", root.transform).GetComponent<LivingSkyController>();
            sky.FollowCamera = camera; sky.RevealDirection = camera.transform.forward;
            sky.SkyYawDegrees = Mathf.Atan2(camera.transform.forward.x, camera.transform.forward.z) * Mathf.Rad2Deg + 9.9f;
            sky.SkyElevationDegrees = Mathf.Asin(camera.transform.forward.y) * Mathf.Rad2Deg + 1.51875f;
            sky.Protection.OnlyCamera = camera; scope.Sky = sky;
            var wood = CloneMaterial(Matter + "Materials/木桌_保留真实木纹.mat", "BenchmarkWood", "Universal Render Pipeline/Lit");
            wood.SetTextureScale("_BaseMap", new Vector2(2.4f,2.0f)); EditorUtility.SetDirty(wood);
            Box(root.transform, "普通木桌", new Vector3(0,-.08f,0), new Vector3(10,.16f,8), wood);
            var water = Instantiate("LivingWaterCup", root.transform); water.transform.localPosition = new Vector3(-.45f,0,.45f); scope.Water = water.GetComponent<LivingWaterController>();
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.name = "局部先被触碰的普通球";
            sphere.transform.SetParent(root.transform, false); sphere.transform.localPosition = new Vector3(.65f,.46f,.25f); sphere.transform.localScale = Vector3.one * .85f;
            sphere.GetComponent<Renderer>().sharedMaterial = Lit("暖赭球",new Color(.55f,.24f,.08f),.20f); Object.DestroyImmediate(sphere.GetComponent<Collider>());
            var local = Instantiate("PainterlyLocalInfluence", root.transform); scope.Local = local.GetComponent<PainterlyLocalController>();
            local.GetComponent<PainterlyInfluence>().Targets = new[] { sphere.GetComponent<Renderer>() }; local.GetComponent<PainterlyInfluence>().OnlyCamera = camera;
            Box(root.transform,"保持纸张身份的方块",new Vector3(1.65f,.31f,-.2f),new Vector3(.55f,.62f,.55f),Lit("纸白方块",new Color(.78f,.76f,.64f),.03f));
            Plant(root.transform);
            var region = Instantiate("PainterlyLocalInfluence", root.transform); region.transform.localPosition = new Vector3(.45f,.12f,.98f);
            scope.Region = region.GetComponent<PainterlyLocalController>(); scope.Region.MaximumRadii = new Vector3(2,.85f,1.7f); scope.Region.InitialExtent = .02f;
            region.GetComponent<PainterlyInfluence>().OnlyCamera = camera;
            var wall = Instantiate("PainterlyRewritePanel",root.transform); wall.transform.localPosition = new Vector3(0,1.75f,-2.7f); wall.transform.localScale = new Vector3(7,3.5f,1);
            scope.Rewrite = wall.GetComponent<PainterlyRewriteController>();
            var sun = new GameObject("窗侧暖光").AddComponent<Light>(); sun.transform.SetParent(root.transform,false); sun.type = LightType.Directional;
            sun.color = new Color(1,.94f,.86f); sun.intensity = 2.1f; sun.transform.rotation = Quaternion.Euler(43,135,0); sun.shadows = LightShadows.Soft; sun.shadowStrength = .45f;
            rig.GetComponent<PainterlyCamera>().DirectionalLight = sun; RenderSettings.sun = sun;
            var fill = new GameObject("柔和补光").AddComponent<Light>(); fill.transform.SetParent(root.transform,false); fill.type = LightType.Directional;
            fill.color = new Color(.8f,.89f,1); fill.intensity = 1.3f; fill.transform.rotation = Quaternion.Euler(28,-45,0);
            var volume = new GameObject("绘画色彩映射").AddComponent<Volume>(); volume.transform.SetParent(root.transform,false); volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Matter + "Rendering/SkyVolume.asset");
            Controls(root.transform,scope);
        }

        private static void Plant(Transform parent)
        {
            Box(parent,"安静陶盆",new Vector3(1.25f,.20f,-1.3f),new Vector3(.45f,.4f,.45f),Lit("陶盆",new Color(.34f,.25f,.18f),.1f));
            var leaf = Lit("叶片_保留绿色",new Color(.12f,.27f,.10f),.1f);
            for(int i=0;i<9;i++)
            {
                var node=GameObject.CreatePrimitive(PrimitiveType.Sphere);node.name="植物叶片_"+i;node.transform.SetParent(parent,false);
                float a=i*2.4f;node.transform.localPosition=new Vector3(1.25f+Mathf.Sin(a)*.24f,.5f+i*.08f,-1.3f+Mathf.Cos(a)*.22f);
                node.transform.localScale=new Vector3(.14f,.42f,.085f);node.transform.localRotation=Quaternion.Euler(35,i*137,30);
                node.GetComponent<Renderer>().sharedMaterial=leaf;Object.DestroyImmediate(node.GetComponent<Collider>());
            }
        }

        private static void Controls(Transform parent,ProductionBenchmark benchmark)
        {
            var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSans/NotoSansCJKsc-Regular.otf");
            var canvas=new GameObject("仅 Benchmark 的中文控制",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(parent,false);canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            benchmark.Controls=canvas;
            var panel=new GameObject("生产组件控制面板",typeof(RectTransform),typeof(Image));panel.transform.SetParent(canvas.transform,false);
            var rect=(RectTransform)panel.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,14);rect.sizeDelta=new Vector2(1080,172);
            panel.GetComponent<Image>().color=new Color(.045f,.055f,.075f,.90f);
            Text(panel.transform,"标题","生产接入验证 · 独立组件，不改游戏路线",font,18,new Vector2(14,136),new Vector2(1052,34));
            benchmark.Status=Text(panel.transform,"状态","全局 0.06  局部 0.00  天空 0.00  水流 0.00",font,17,new Vector2(14,99),new Vector2(1052,34));
            string[] labels={"轻现实","全局触碰","梦境","完整绘画","局部触碰","区域传播","局部天空","天空流动","完整天空","水中旋流","开始溢出","停止杯水","笔势擦开","清空重播","慢转镜头","暂停／继续"};
            benchmark.Buttons=new Button[labels.Length];
            for(int i=0;i<labels.Length;i++)
            {
                var node=new GameObject(labels[i],typeof(RectTransform),typeof(Image),typeof(Button));node.transform.SetParent(panel.transform,false);
                var buttonRect=(RectTransform)node.transform;buttonRect.anchorMin=buttonRect.anchorMax=Vector2.zero;buttonRect.pivot=Vector2.zero;
                buttonRect.anchoredPosition=new Vector2(14+(i%8)*132,55-(i/8)*41);buttonRect.sizeDelta=new Vector2(124,34);
                node.GetComponent<Image>().color=new Color(.19f,.23f,.28f,.95f);benchmark.Buttons[i]=node.GetComponent<Button>();
                var label=Text(node.transform,"按钮文字",labels[i],font,17,Vector2.zero,new Vector2(124,34));label.alignment=TextAnchor.MiddleCenter;
            }
            var events=new GameObject("独立 UI 事件",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(parent,false);
        }

        private static Text Text(Transform parent,string name,string text,Font font,int size,Vector2 position,Vector2 dimensions)
        {
            var node=new GameObject(name,typeof(RectTransform),typeof(Text));node.transform.SetParent(parent,false);
            var rect=(RectTransform)node.transform;rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;rect.anchoredPosition=position;rect.sizeDelta=dimensions;
            var label=node.GetComponent<Text>();label.font=font;label.fontSize=size;label.text=text;label.color=new Color(.89f,.92f,.95f);label.raycastTarget=false;
            label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Truncate;return label;
        }

        private static void ConfigureCamera(Camera camera)
        {
            camera.nearClipPlane=.05f;camera.farClipPlane=120;camera.allowHDR=true;camera.allowMSAA=false;camera.clearFlags=CameraClearFlags.Skybox;
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;
            data.requiresDepthOption=CameraOverrideOption.On;data.requiresColorOption=CameraOverrideOption.On;
            data.antialiasing=AntialiasingMode.TemporalAntiAliasing;data.taaSettings.quality=TemporalAAQuality.High;data.taaSettings.baseBlendFactor=.60f;data.taaSettings.jitterScale=.5f;
        }
        private static void AssignProfiles(PainterlyStyleController style)
        {
            style.Reality=AssetDatabase.LoadAssetAtPath<PainterlyStyleProfile>(Root+"Profiles/Reality.asset");style.Touched=AssetDatabase.LoadAssetAtPath<PainterlyStyleProfile>(Root+"Profiles/Touched.asset");
            style.Dream=AssetDatabase.LoadAssetAtPath<PainterlyStyleProfile>(Root+"Profiles/Dream.asset");style.Final=AssetDatabase.LoadAssetAtPath<PainterlyStyleProfile>(Root+"Profiles/Final.asset");
        }
        private static GpuPigment Pigment(GameObject node,int mode,Renderer target,int width,int height)
        {
            var p=node.AddComponent<GpuPigment>();p.Mode=mode;p.Flow=mode==0?1:0;p.Target=target;p.Width=width;p.Height=height;p.AutomaticTime=false;
            p.SimulationShader=RequiredShader(Matter+"Shaders/PigmentSimulation.shader");return p;
        }
        private static Transform Child(Transform parent,string name,Vector3 position)
        { var node=new GameObject(name);node.transform.SetParent(parent,false);node.transform.localPosition=position;return node.transform; }
        private static Renderer Node(Transform parent,string name,Mesh mesh,Material material)
        {
            var node=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));node.transform.SetParent(parent,false);
            node.GetComponent<MeshFilter>().sharedMesh=mesh;var r=node.GetComponent<Renderer>();r.sharedMaterial=material;return r;
        }
        private static Mesh MatterMesh(string name)=>AssetDatabase.LoadAssetAtPath<Mesh>(Matter+"Meshes/"+name+".asset");
        private static Material MatterMaterial(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Matter+"Materials/"+name+".mat");
        private static void SavePrefab(GameObject root,string name)
        { PrefabUtility.SaveAsPrefabAsset(root,Root+"Prefabs/"+name+".prefab");Object.DestroyImmediate(root); }
        private static GameObject Instantiate(string name,Transform parent)
        { var node=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/"+name+".prefab"));node.transform.SetParent(parent,false);return node; }
        private static Material Lit(string name,Color colour,float smoothness)
        { var m=Store(Root+"Materials/"+name+".mat",new Material(Shader.Find("Universal Render Pipeline/Lit")));m.SetColor("_BaseColor",colour);m.SetFloat("_Smoothness",smoothness);EditorUtility.SetDirty(m);return m; }
        private static Material CloneMaterial(string source,string name,string shader)
        { var original=AssetDatabase.LoadAssetAtPath<Material>(source);return Store(Root+"Materials/"+name+".mat",original!=null?new Material(original):new Material(Shader.Find(shader))); }
        private static void Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var node=GameObject.CreatePrimitive(PrimitiveType.Cube);node.name=name;node.transform.SetParent(parent,false);node.transform.localPosition=position;node.transform.localScale=scale;
            node.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(node.GetComponent<Collider>());
        }
        private static void Copy(string source,string destination)
        { if(AssetDatabase.LoadMainAssetAtPath(destination)==null&&!AssetDatabase.CopyAsset(source,destination))throw new InvalidOperationException("无法复制生产资源："+source); }
        private static T Store<T>(string path,T candidate) where T:Object
        {
            candidate.name=Path.GetFileNameWithoutExtension(path);var previous=AssetDatabase.LoadAssetAtPath<T>(path);
            if(previous==null){AssetDatabase.CreateAsset(candidate,path);return candidate;}
            EditorUtility.CopySerialized(candidate,previous);Object.DestroyImmediate(candidate);EditorUtility.SetDirty(previous);return previous;
        }
    }
}
