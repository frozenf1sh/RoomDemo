// 文件：HoverGoldBenchmarkBuilder.cs
// 职责：通过 Editor API 生成独立悬停金光实验和专用 URP 资产。
// 边界：不打开或改写正式画室，不修改 Build Settings，不覆盖未保存场景。
using System;
using System.Collections.Generic;
using System.IO;
using Game.Unity.TechnicalArt.HoverGold;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Game.Editor.TechnicalArt.HoverGold
{
    /// <summary>原创代码及既有项目模型构成的可重复 TA 样例。</summary>
    public static class HoverGoldBenchmarkBuilder
    {
        public const string Root = "Assets/_Game/Content/TechnicalArt/HoverGold/";
        public const string ScenePath = "Assets/_Game/Content/Scenes/Experiments/TechnicalArt/HoverGoldBenchmark.unity";
        private const string Art = "Assets/_Game/Content/ArtRebuild/";
        private const string Matter = "Assets/_Game/Content/TechnicalArt/LivingMatter/";

        /// <summary>生成并打开独立实验；已有脏场景会阻止操作。</summary>
        [MenuItem("游戏/技术美术/悬停金光/生成独立测试场景")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式再生成实验。");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("存在未保存场景，实验生成已停止。");
            Directory.CreateDirectory(Root + "Rendering");
            Directory.CreateDirectory(Root + "Materials");
            AssetDatabase.Refresh();
            var pipeline = CreatePipeline();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.19f, .22f, .29f);
            var wood = Lit("沉静深木", new Color(.12f, .075f, .046f));
            var wall = Lit("灰蓝背景", new Color(.105f, .145f, .19f));
            var porcelain = Lit("暖灰陶釉", new Color(.52f, .55f, .52f), .27f);
            var paper = Lit("旧纸页", new Color(.58f, .51f, .37f));
            var cover = Lit("灰绿布书封", new Color(.10f, .17f, .14f));
            Box("试验桌面", new Vector3(0, -.1f, 0), new Vector3(4.9f, .18f, 2.7f), wood);
            Box("后方灰蓝墙", new Vector3(0, 1.6f, 1.55f), new Vector3(7, 4, .15f), wall);

            var camera = new GameObject("固定观察镜头").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 2.05f, -5.5f);
            camera.transform.LookAt(new Vector3(0, .56f, 0));
            camera.fieldOfView = 38;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.045f, .065f, .09f);
            camera.allowHDR = true;
            camera.allowMSAA = false;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var pointer = camera.gameObject.AddComponent<HoverGoldPointer>();
            pointer.View = camera;

            var key = new GameObject("左上柔暖主光").AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1, .85f, .65f);
            key.intensity = 1.6f;
            key.transform.rotation = Quaternion.Euler(43, -27, 0);
            key.shadows = LightShadows.Soft;
            RenderSettings.sun = key;
            var fill = new GameObject("右侧冷色补光").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.transform.position = new Vector3(2.3f, 2.6f, -1.5f);
            fill.color = new Color(.56f, .7f, 1);
            fill.intensity = 7;
            fill.range = 7;

            var cup = new GameObject("陶杯_多零件整体");
            cup.transform.position = new Vector3(-1.38f, .01f, -.15f);
            cup.transform.localScale = Vector3.one * .65f;
            cup.transform.rotation = Quaternion.Euler(0, 20, 0);
            Surface(cup.transform, "杯体", Mesh(Matter + "Meshes/开口陶杯.asset"), porcelain);
            Surface(cup.transform, "杯把", Mesh(Matter + "Meshes/圆润杯把.asset"), porcelain);
            Surface(cup.transform, "细杯沿", Mesh(Matter + "Meshes/细陶釉边.asset"), porcelain);
            var cupTarget = Target(cup);

            var book = new GameObject("旧书_封面与书页整体");
            book.transform.position = new Vector3(0, .14f, -.25f);
            book.transform.rotation = Quaternion.Euler(0, -17, 0);
            Box("纸页", Vector3.zero, new Vector3(.85f, .19f, .63f), paper, book.transform);
            Box("下书封", new Vector3(0, -.11f, 0), new Vector3(.9f, .035f, .68f), cover, book.transform);
            Box("上书封", new Vector3(0, .11f, 0), new Vector3(.9f, .035f, .68f), cover, book.transform);
            Box("书脊", new Vector3(-.44f, 0, 0), new Vector3(.055f, .22f, .68f), cover, book.transform);
            var bookTarget = Target(book);

            var lamp = new GameObject("旧油灯_金属与玻璃整体");
            var lampVisual = new GameObject("视觉_Z轴模型转为竖直").transform;
            lampVisual.SetParent(lamp.transform, false);
            lampVisual.localRotation = Quaternion.Euler(-90, 0, 0);
            Surface(lampVisual, "金属结构", Mesh(Art + "Meshes/室内光照副本_vintage_oil_lamp_3557.asset"),
                CopyLit("旧油灯金属", Art + "Materials/扫描油灯_vintage_oil_lamp.mat"));
            Surface(lampVisual, "玻璃罩", Mesh(Art + "Meshes/室内光照副本_vintage_oil_lamp_glass_891.asset"),
                CopyLit("旧油灯玻璃", Art + "Materials/扫描油灯_vintage_oil_lamp_glass.mat"));
            var renderers = lamp.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float scale = 1.28f / bounds.size.y;
            lamp.transform.localScale = Vector3.one * scale;
            lamp.transform.position = new Vector3(1.36f, .01f, .08f) -
                new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
            var lampTarget = Target(lamp);
            // 用真实前景立板遮住半个油灯；O 可开关，立板本身没有悬停身份。
            var occluder = Box("遮挡验证立板", new Vector3(1.12f, .67f, -.58f), new Vector3(.53f, 1.34f, .09f), wall);
            occluder.SetActive(false);
            var benchmark = new GameObject("悬停金光实验控制").AddComponent<HoverGoldBenchmark>();
            benchmark.Pipeline = pipeline;
            benchmark.Pointer = pointer;
            benchmark.Targets = new[] { cupTarget, bookTarget, lampTarget };
            benchmark.Occluder = occluder;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("悬停金光测试场景已生成；正式画室与构建列表未修改。");
        }

        private static UniversalRenderPipelineAsset CreatePipeline()
        {
            string rendererPath = Root + "Rendering/HoverGoldRenderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var feature = renderer.rendererFeatures.Find(x => x is HoverGoldFeature) as HoverGoldFeature;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<HoverGoldFeature>();
                feature.name = "外侧疏松断续金色游丝";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            // 生成器同时同步已存在的实验资产，避免序列化旧参数盖住新的默认效果。
            feature.WidthAt1080 = 3;
            feature.Strength = .9f;
            feature.Gold = new Color(.97f, .68f, .22f, 1);
            feature.BraidOffsetAt1080 = 8;
            feature.BraidAmplitudeAt1080 = 7;
            feature.FlowSpeedAt1080 = 22;
            feature.StrandLapSeconds = 64;
            feature.BreathAmount = .16f;
            feature.BreathPeriodSeconds = 4.8f;
            feature.Configure(AssetDatabase.LoadAssetAtPath<Shader>(Root + "Shaders/HoverGold.shader"));
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(feature);
            string path = Root + "Rendering/HoverGoldPipeline.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, path);
            }
            pipeline.supportsCameraDepthTexture = true;
            pipeline.msaaSampleCount = 1;
            pipeline.renderScale = 1;
            pipeline.supportsHDR = true;
            var pipelineSettings = new SerializedObject(pipeline);
            pipelineSettings.FindProperty("m_SoftShadowsSupported").boolValue = true;
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            return pipeline;
        }

        private static HoverGoldTarget Target(GameObject root)
        {
            var surfaces = root.GetComponentsInChildren<Renderer>();
            var bounds = surfaces[0].bounds;
            foreach (var surface in surfaces) bounds.Encapsulate(surface.bounds);
            var logic = new GameObject("逻辑拾取");
            logic.transform.SetParent(root.transform, false);
            var collider = logic.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = new Vector3(bounds.size.x / root.transform.lossyScale.x,
                bounds.size.y / root.transform.lossyScale.y, bounds.size.z / root.transform.lossyScale.z);
            var target = root.AddComponent<HoverGoldTarget>();
            target.Surfaces = surfaces;
            return target;
        }

        private static Mesh Mesh(string path)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) throw new InvalidOperationException("缺少实验模型：" + path);
            return mesh;
        }

        private static void Surface(Transform parent, string name, Mesh mesh, Material material)
        {
            var node = new GameObject(name);
            node.transform.SetParent(parent, false);
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static GameObject Box(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            var node = GameObject.CreatePrimitive(PrimitiveType.Cube);
            node.name = name;
            node.transform.SetParent(parent, false);
            node.transform.localPosition = position;
            node.transform.localScale = scale;
            node.GetComponent<Renderer>().sharedMaterial = material;
            // 物品使用独立逻辑拾取子节点，桌面和遮挡立板仍保留阻挡碰撞体。
            if (parent != null) Object.DestroyImmediate(node.GetComponent<Collider>());
            return node;
        }

        private static Material Lit(string name, Color color, float smoothness = .12f)
        {
            string path = Root + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CopyLit(string name, string sourcePath)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null) throw new InvalidOperationException("缺少实验材质来源：" + sourcePath);
            var material = Lit(name, Color.white);
            material.CopyPropertiesFromMaterial(source);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>只构建已经保存的独立场景，不写 EditorBuildSettings。</summary>
        /// <exception cref="InvalidOperationException">构建失败时抛出；报告保留在本地日志目录。</exception>
        [MenuItem("游戏/技术美术/悬停金光/构建独立 macOS 预览")]
        public static void BuildMac()
        {
            const string directory = "Builds/HoverGoldBenchmark";
            Directory.CreateDirectory(directory);
            Directory.CreateDirectory("Logs/HoverGold");
            // Unity 构建会回写序列化升级及 Shader 裁剪设置，精确恢复构建前的源文件字节。
            // 快照包含用户原有修改；本工具不把正式资产升级混入独立实验。
            var snapshots = CaptureBuildInputs();
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = directory + "/HoverGoldBenchmark.app",
                    target = BuildTarget.StandaloneOSX,
                    options = BuildOptions.Development
                });
            }
            finally
            {
                foreach (var snapshot in snapshots) File.WriteAllBytes(snapshot.Key, snapshot.Value);
                AssetDatabase.Refresh();
            }
            var s = report.summary;
            File.WriteAllText("Logs/HoverGold/构建报告.txt",
                $"结果={s.result}\n错误={s.totalErrors}\n警告={s.totalWarnings}\n大小={s.totalSize}\n耗时={s.totalTime.TotalSeconds:F1}s\n");
            File.Copy("Docs/TechnicalArt/Production/悬停金光实验.md", directory + "/使用说明.md", true);
            File.Copy("Assets/ThirdParty/NotoSans/OFL.txt", directory + "/NotoSans-OFL.txt", true);
            if (s.result != BuildResult.Succeeded) throw new InvalidOperationException("悬停金光独立构建失败，请查看 Logs/HoverGold/构建报告.txt。");
            Debug.Log("悬停金光独立预览构建成功：" + directory);
        }

        /// <summary>保留构建可能回写的已有配置；不读取或重建场景 YAML。</summary>
        private static Dictionary<string, byte[]> CaptureBuildInputs()
        {
            var snapshots = new Dictionary<string, byte[]>();
            foreach (var path in Directory.GetFiles("ProjectSettings", "*.asset"))
                snapshots[path] = File.ReadAllBytes(path);
            foreach (string type in new[] { "RenderPipelineAsset", "RenderPipelineGlobalSettings", "VolumeProfile" })
            {
                foreach (string guid in AssetDatabase.FindAssets("t:" + type, new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (File.Exists(path)) snapshots[path] = File.ReadAllBytes(path);
                }
            }
            return snapshots;
        }
    }
}
