// 文件：Painterly3DBenchmarkBuilder.cs
// 职责：通过 Editor API 创建独立普通 3D 静物 Benchmark、专用管线和普通材质。
// 边界：只写本工具拥有的资源；不覆盖主场景、既有美术、全局 URP 设置或游戏路线。
using System.IO;
using Game.Unity.TechnicalArt.RealtimePainterly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Game.Editor.TechnicalArt.RealtimePainterly
{
    /// <summary>生成实验场景；拒绝覆盖任意尚未保存的人工场景。</summary>
    public static class Painterly3DBenchmarkBuilder
    {
        public const string ScenePath = "Assets/_Game/Content/Scenes/Painterly3DBenchmark.unity";
        private const string Root = "Assets/_Game/Content/TechnicalArt/RealtimePainterly/";
        private const string PlantModels = "Assets/_Game/Content/Art/Models/ArtistPlant/";
        private static Font font;

        /// <summary>创建或重新生成工具拥有的独立场景和资源，完成后保存并打开场景。</summary>
        [MenuItem("游戏/技术美术/生成实时绘画3D Benchmark")]
        public static void Build()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                if (EditorSceneManager.GetSceneAt(i).isDirty) throw new System.InvalidOperationException("当前有未保存的场景；请先保存，避免实验生成器覆盖人工工作。");
            }
            Directory.CreateDirectory(Root+"Materials");
            Directory.CreateDirectory(Root+"Rendering");
            AssetDatabase.Refresh();
            var pipeline = Pipeline();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.69f,.73f,.79f);
            RenderSettings.ambientEquatorColor = new Color(.56f,.58f,.62f);
            RenderSettings.ambientGroundColor = new Color(.30f,.27f,.25f);
            RenderSettings.fog = false;
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSans/NotoSansCJKsc-Regular.otf");

            var wood = Lit("木桌_真实木纹",new Color(1,.94f,.86f),.25f,
                "Assets/ThirdParty/PolyHaven/OldWoodFloor/Diffuse.jpg","Assets/ThirdParty/PolyHaven/OldWoodFloor/nor_gl.jpg");
            wood.SetTextureScale("_BaseMap",new Vector2(1.8f,1));
            var wall = Lit("墙面_暖石膏",new Color(.79f,.74f,.64f),.05f);
            wall.SetTexture("_BaseMap",null);
            var floor = Lit("地面_哑光暖灰",new Color(.48f,.45f,.39f),.04f);
            var ceramic = Lit("球体_赭红陶釉",new Color(.55f,.16f,.075f),.25f);
            var cubeMat = Lit("方块_象牙白石材",new Color(.82f,.81f,.67f),.12f);
            var leafA = Lit("植物_深绿叶",new Color(.12f,.28f,.10f),.15f);
            var leafB = Lit("植物_浅绿叶",new Color(.28f,.42f,.13f),.14f);
            foreach (var leaf in new[]{leafA,leafB})
            {
                leaf.SetFloat("_Cull",0);
                EditorUtility.SetDirty(leaf);
            }
            var pot = Lit("植物_陶土盆",new Color(.53f,.28f,.15f),.07f);
            var soil = Lit("植物_泥土与枝条",new Color(.15f,.12f,.07f),.02f);
            var water = Lit("水面_普通静水",new Color(.19f,.35f,.38f),.58f);
            var basin = Lit("水盘_深色石材",new Color(.25f,.29f,.25f),.12f);

            var root = new GameObject("实时绘画3D实验_独立于游戏");
            Box(root.transform,"普通地面",new Vector3(0,-.1f,0),new Vector3(13,.2f,12),floor);
            Box(root.transform,"普通墙面",new Vector3(0,3.5f,-2),new Vector3(22,7,.12f),wall);
            var table = new GameObject("普通木桌").transform;
            table.SetParent(root.transform);
            for (int plank = 0; plank < 3; plank++)
            {
                Box(table,"木桌面_"+plank,new Vector3(-1.28f+plank*1.28f,1.16f,0),new Vector3(1.27f,.12f,1.85f),wood);
            }
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                Box(table,"木桌腿",new Vector3(x*1.62f,.54f,z*.65f),new Vector3(.17f,1.08f,.17f),wood);
            Box(table,"桌前围板",new Vector3(0,.96f,.72f),new Vector3(3.3f,.20f,.1f),wood);
            var sphere = Primitive(root.transform,"普通球体",PrimitiveType.Sphere,new Vector3(-.35f,1.61f,.26f),Vector3.one*.78f,ceramic);
            sphere.transform.rotation = Quaternion.Euler(0,20,0);
            var cube = Box(root.transform,"普通方块",new Vector3(.48f,1.53f,-.34f),Vector3.one*.62f,cubeMat);
            cube.transform.rotation = Quaternion.Euler(0,18,0);
            Plant(root.transform,new Vector3(-1.26f,1.22f,-.35f),pot,soil,leafA,leafB);
            var waterRoot = new GameObject("普通水盘").transform;
            waterRoot.SetParent(root.transform);
            waterRoot.position = new Vector3(1.02f,1.245f,.43f);
            Box(waterRoot,"水盘底",Vector3.zero,new Vector3(.84f,.055f,.67f),basin);
            Box(waterRoot,"静水平面",new Vector3(0,.042f,0),new Vector3(.73f,.006f,.56f),water);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(waterRoot,"水盘长边",new Vector3(0,.052f,side*.31f),new Vector3(.84f,.06f,.06f),basin);
                Box(waterRoot,"水盘短边",new Vector3(side*.39f,.052f,0),new Vector3(.06f,.06f,.62f),basin);
            }

            var light = new GameObject("侧上方_柔和日光").AddComponent<Light>();
            light.transform.SetParent(root.transform);
            light.type = LightType.Directional;
            light.color = new Color(1,.90f,.77f);
            light.intensity = 2.8f;
            light.transform.rotation = Quaternion.Euler(48,140,0);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = .72f;
            light.shadowBias = .04f;
            RenderSettings.sun = light;
            var fill = new GameObject("背侧_冷色柔和补光").AddComponent<Light>();
            fill.transform.SetParent(root.transform);
            fill.type = LightType.Directional;
            fill.color = new Color(.72f,.82f,1);
            fill.intensity = .8f;
            fill.transform.rotation = Quaternion.Euler(30,-45,0);
            fill.shadows = LightShadows.None;
            var camera = new GameObject("Benchmark摄像机").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(.8f,4.4f,5.7f);
            camera.transform.LookAt(new Vector3(0,1.65f,0));
            camera.fieldOfView = 39;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 40;
            camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.72f,.65f,.52f);
            var additional = camera.GetUniversalAdditionalCameraData();
            additional.renderPostProcessing = true;
            additional.requiresDepthOption = CameraOverrideOption.On;
            additional.antialiasing = AntialiasingMode.TemporalAntiAliasing;
            additional.taaSettings.quality = TemporalAAQuality.High;
            additional.taaSettings.baseBlendFactor = .65f;
            additional.taaSettings.jitterScale = .5f;
            additional.taaSettings.contrastAdaptiveSharpening = .12f;
            var effect = camera.gameObject.AddComponent<PainterlyCamera>();
            effect.DirectionalLight = light;
            var benchmark = root.AddComponent<Painterly3DBenchmark>();
            Controls(root.transform,benchmark,out var canvas,out var label,out var slider);
            benchmark.Configure(pipeline,camera,effect,canvas,label,slider);
            var events = new GameObject("实验面板输入",typeof(EventSystem),typeof(InputSystemUIInputModule));
            events.transform.SetParent(root.transform);
            var volume = new GameObject("静物色调映射").AddComponent<Volume>();
            volume.transform.SetParent(root.transform);
            volume.isGlobal = true;
            var profile = Store(Root+"Rendering/BenchmarkVolume.asset",ScriptableObject.CreateInstance<VolumeProfile>());
            profile.components.Clear();
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            AssetDatabase.AddObjectToAsset(tone,profile);
            EditorUtility.SetDirty(profile);
            volume.sharedProfile = profile;
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene,ScenePath);
            Selection.activeGameObject = root;
            Debug.Log("已生成实时绘画3D Benchmark："+ScenePath+"；普通材质与专用 RenderGraph 管线已保存。");
        }

        /// <summary>复制现有管线到实验目录，全部编辑只发生在副本内。</summary>
        private static UniversalRenderPipelineAsset Pipeline()
        {
            string pipelinePath = Root+"Rendering/PainterlyBenchmarkPipeline.asset";
            string rendererPath = Root+"Rendering/PainterlyBenchmarkRenderer.asset";
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath)==null)
                AssetDatabase.CopyAsset("Assets/Settings/PC_RPAsset.asset",pipelinePath);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            var serializedPipeline = new SerializedObject(pipeline);
            var renderers = serializedPipeline.FindProperty("m_RendererDataList");
            if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath)==null)
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(renderers.GetArrayElementAtIndex(0).objectReferenceValue),rendererPath);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            renderer.rendererFeatures.Clear();
            var feature = Store(Root+"Rendering/RealtimePainterlyFeature.asset",ScriptableObject.CreateInstance<RealtimePainterlyFeature>());
            feature.name = "实时绘画_Kuwahara与世界笔触";
            feature.Configure(Shader.Find("Hidden/RoomDemo/RealtimePainterly/Kuwahara"),Shader.Find("Hidden/RoomDemo/RealtimePainterly/WorldBrushPatches"));
            renderer.rendererFeatures.Add(feature);
            renderer.SetDirty();
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            pipeline.msaaSampleCount = 1;
            pipeline.supportsCameraDepthTexture = true;
            pipeline.shadowDistance = 15;
            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(feature);
            return pipeline;
        }

        /// <summary>创建普通 URP Lit 材质，木纹与石膏来自已有 CC0 普通纹理。</summary>
        private static Material Lit(string name, Color colour, float smoothness, string albedo = null, string normal = null)
        {
            var material = Store(Root+"Materials/"+name+".mat",new Material(Shader.Find("Universal Render Pipeline/Lit")));
            material.name = name;
            material.SetColor("_BaseColor",colour);
            material.SetFloat("_Metallic",0);
            material.SetFloat("_Smoothness",smoothness);
            if (albedo!=null) material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
            if (normal!=null)
            {
                material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
                material.SetFloat("_BumpScale",.3f);
                material.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>复用现有陶盆与弯叶网格，只保存普通的静态株形。</summary>
        private static void Plant(Transform parent,Vector3 position,Material pot,Material soil,Material dark,Material light)
        {
            var plant = new GameObject("普通盆栽").transform;
            plant.SetParent(parent);
            plant.localPosition = position;
            var potNode = MeshNode(plant,"陶盆",AssetDatabase.LoadAssetAtPath<Mesh>(PlantModels+"PotProfile.asset"),pot);
            potNode.localScale = Vector3.one*.64f;
            potNode.localPosition = new Vector3(0,.32f,0);
            Primitive(plant,"泥土",PrimitiveType.Cylinder,new Vector3(0,.61f,0),new Vector3(.35f,.012f,.35f),soil);
            var leafMesh = AssetDatabase.LoadAssetAtPath<Mesh>(PlantModels+"FoldedLeaf.asset");
            for (int stem = 0; stem < 3; stem++)
            {
                var stemMesh = AssetDatabase.LoadAssetAtPath<Mesh>(PlantModels+"BaseStem"+stem+".asset");
                var branch = MeshNode(plant,"静态曲枝_"+stem,stemMesh,soil);
                branch.localPosition = new Vector3(0,.62f,0);
                branch.localScale = new Vector3(1,1.3f,1);
                float angle = stem*2.399f+.4f;
                for (int leaf = 0; leaf < 5; leaf++)
                {
                    float t = .23f+leaf*.145f;
                    float leafAngle = angle+(leaf%2==0?.95f:-1.25f);
                    var blade = MeshNode(plant,"普通叶片_"+stem+"_"+leaf,leafMesh,leaf%2==0?dark:light);
                    blade.localPosition = new Vector3(Mathf.Cos(angle)*(.06f+t*.22f),.63f+t*.91f,Mathf.Sin(angle)*(.06f+t*.22f));
                    blade.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(leafAngle),-.10f,Mathf.Sin(leafAngle)),Vector3.up);
                    blade.localScale = new Vector3(.39f,.7f,.30f+leaf*.024f);
                }
            }
        }

        /// <summary>为一个网格建立独立的静态 Renderer，不附加任何游戏组件。</summary>
        private static Transform MeshNode(Transform parent,string name,Mesh mesh,Material material)
        {
            var node = new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            node.transform.SetParent(parent,false);
            node.GetComponent<MeshFilter>().sharedMesh = mesh;
            node.GetComponent<MeshRenderer>().sharedMaterial = material;
            return node.transform;
        }

        private static GameObject Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
            => Primitive(parent,name,PrimitiveType.Cube,position,scale,material);

        /// <summary>建立普通几何；去掉不参与本实验的 Collider。</summary>
        private static GameObject Primitive(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var node = GameObject.CreatePrimitive(type);
            node.name = name;
            node.transform.SetParent(parent,false);
            node.transform.localPosition = position;
            node.transform.localScale = scale;
            node.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(node.GetComponent<Collider>());
            return node;
        }

        /// <summary>固定 GUID 更新工具拥有的资产，不删除其他目录资源。</summary>
        private static T Store<T>(string path,T candidate) where T:Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing==null) { AssetDatabase.CreateAsset(candidate,path); return candidate; }
            EditorUtility.CopySerialized(candidate,existing);
            Object.DestroyImmediate(candidate);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>建立可缩放的中文实验面板；H 隐藏面板，截图保持完整画面。</summary>
        private static void Controls(Transform parent,Painterly3DBenchmark benchmark,out Canvas canvas,out Text label,out Slider slider)
        {
            var root = new GameObject("中文实验控制面板",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(parent,false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,900);
            scaler.matchWidthOrHeight = .5f;
            var panel = Rect(root.transform,"底部控制条",new Vector2(0,0),new Vector2(1,0),new Vector2(0,0),new Vector2(0,110));
            panel.pivot = Vector2.zero;
            var image = panel.gameObject.AddComponent<Image>();
            image.color = new Color(.08f,.09f,.08f,.93f);
            label = Label(panel,"状态",new Vector2(24,70),new Vector2(770,30),"实时绘画 · 世界空间笔触",21);
            Label(panel,"快捷键",new Vector2(800,70),new Vector2(780,30),"O 原图   P 绘画   空格 环绕   ← → 移动   H 隐藏面板",18);
            Button(panel,"普通原图",24,benchmark.ShowOriginal);
            Button(panel,"完整绘画",170,benchmark.ShowPainted);
            Button(panel,"环绕／冻结",316,benchmark.ToggleOrbit);
            Button(panel,"世界锚点",462,benchmark.ToggleDebug);
            Label(panel,"强度名",new Vector2(650,23),new Vector2(210,32),"PainterlyIntensity",18);
            var track = Rect(panel,"绘画强度",Vector2.zero,Vector2.zero,new Vector2(875,36),new Vector2(370,22));
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(.3f,.34f,.30f);
            slider = track.gameObject.AddComponent<Slider>();
            var handle = Rect(track,"强度滑块",Vector2.zero,Vector2.one,Vector2.zero,new Vector2(18,4));
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(.84f,.78f,.59f);
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.SetValueWithoutNotify(1);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(slider.onValueChanged,benchmark.SetIntensity);
        }

        private static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max,Vector2 position,Vector2 size)
        {
            var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0,.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Text Label(Transform parent,string name,Vector2 position,Vector2 size,string text,int fontSize)
        {
            var label = Rect(parent,name,Vector2.zero,Vector2.zero,position,size).gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.text = text;
            label.color = new Color(.88f,.86f,.78f);
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            return label;
        }

        private static void Button(Transform parent,string text,float x,UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(parent,text,Vector2.zero,Vector2.zero,new Vector2(x,31),new Vector2(130,36));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.22f,.26f,.22f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick,action);
            var label = Label(rect,"按钮文字",new Vector2(8,18),new Vector2(114,32),text,19);
            label.alignment = TextAnchor.MiddleCenter;
        }
    }
}
