// 文件：AtelierTAProbeBuilder.cs
// 职责：从已保存画室生成五份独立TA验证副本、测试管线与少量占位视觉。
// 边界：生成资源仅写Experiments，构建写Builds；临时质量设置恢复，不重建正式美术或修改正式构建列表。
using System;
using System.IO;
using System.Linq;
using Game.Unity.ArtRebuild;
using Game.Unity.TechnicalArt.RealtimePainterly.Experiments;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Game.Editor.ArtRebuild
{
    /// <summary>菜单生成器优先复用完整画室，所有保存都通过Editor API执行。</summary>
    public static class AtelierTAProbeBuilder
    {
        public const string Folder = "Assets/_Game/Content/Scenes/Experiments/TechnicalArt/AtelierProbes";
        public const string Resources = "Assets/_Game/Content/TechnicalArt/Experiments/AtelierProbes";
        private const string Source = "Assets/_Game/Content/Scenes/AtelierPlayable.unity";
        private static readonly string[] Names = { "TA01_局部草稿遮挡", "TA02_洗笔反馈", "TA03_手镜比较", "TA04_刮刀干湿表面", "TA05_屋面试刮" };

        /// <summary>首次创建测试资源与副本，已有副本不自动覆盖。</summary>
        [MenuItem("游戏/技术美术/四阶段TA验证/创建缺失验证场景")]
        public static void CreateMissing()
        {
            RequireClean();
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory(Resources + "/Rendering");
            Directory.CreateDirectory(Resources + "/Materials");
            Directory.CreateDirectory(Resources + "/Models");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var sourceScene = EditorSceneManager.OpenScene(Source);
            var sourceGame = Object.FindAnyObjectByType<AtelierGameplay>();
            var pipeline = CreatePipeline();
            var plaster = Material("验证_白石膏", "Universal Render Pipeline/Lit", new Color(.62f, .60f, .56f));
            var gold = Material("验证_镜框占位", "Universal Render Pipeline/Lit", new Color(.42f, .25f, .08f));
            gold.SetFloat("_Metallic", .75f);
            gold.SetFloat("_Smoothness", .75f);
            var mirror = Material("验证_真实手镜", "RoomDemo/TAProbe/RealMirror", Color.white);
            var dark = Material("验证_完整镜面占位", "Universal Render Pipeline/Lit", new Color(.02f, .045f, .075f));
            var paper = Material("验证_无题材纸面占位", "Universal Render Pipeline/Lit", new Color(.62f, .58f, .47f));
            var steel = Material("验证_刮刀钢面", "Universal Render Pipeline/Lit", new Color(.43f, .48f, .49f));
            steel.SetFloat("_Metallic", .85f);
            var wood = Material("验证_刮刀木柄", "Universal Render Pipeline/Lit", new Color(.18f, .085f, .035f));
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Resources + "/Models/策划石膏像.fbx");
            if (model == null) throw new InvalidOperationException("缺少策划提供的石膏FBX，已停止生成。");
            var disc = Ellipse(false);
            var ring = Ellipse(true);
            var shard = Shard();
            for (int index = 0; index < Names.Length; index++)
            {
                string destination = Folder + "/" + Names[index] + ".unity";
                if (File.Exists(destination)) continue;
                var scene = EditorSceneManager.OpenScene(Source);
                var game = Object.FindAnyObjectByType<AtelierGameplay>();
                Camera camera = game.ViewCamera;
                Vector3 home = camera.transform.position;
                Vector3 euler = camera.transform.eulerAngles;
                Vector3 desk = game.DeskFocusPose.position;
                Vector3 deskEuler = game.DeskFocusPose.eulerAngles;
                Renderer water = game.Wash.Water;
                Transform brush = game.Wash.SupportedBrush;
                MeshFilter bristles = game.Wash.BrushSurface;
                var sky = game.Finale.Sky;
                Renderer[] roofs = game.Finale.RoofSurfaces;
                // 只禁用克隆场景的玩法入口；Visual仍保留，验证组件不创建GameSession。
                foreach (var behaviour in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true)))
                {
                    if (behaviour == null) continue;
                    string ns = behaviour.GetType().Namespace ?? "";
                    if (ns.StartsWith("Game.Unity") && !ns.Contains("TechnicalArt")) behaviour.enabled = false;
                }
                foreach (var canvas in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Canvas>(true))) canvas.enabled = false;
                var extras = new GameObject("TA验证_额外视觉与演示对象");
                var bust = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                bust.name = "TA占位_策划石膏像";
                bust.transform.SetParent(extras.transform);
                // 保留FBX根节点的坐标轴修正，不能覆盖导入时的负90度旋转。
                bust.transform.rotation = Quaternion.Euler(0, 180, 0) * model.transform.rotation;
                Fit(bust, .57f, new Vector3(-2.30f, .782f, .05f));
                foreach (var renderer in bust.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = Enumerable.Repeat(plaster, renderer.sharedMaterials.Length).ToArray();
                var mirrorPivot = new GameObject("TA占位_完整手镜与转轴").transform;
                mirrorPivot.SetParent(extras.transform);
                mirrorPivot.position = new Vector3(-1.15f, .98f, -1.12f);
                mirrorPivot.rotation = Quaternion.Euler(90, 0, -20);
                var face = MeshObject("TA镜面_真实反射层", disc, index == 2 ? mirror : dark, mirrorPivot);
                face.gameObject.layer = 30;
                MeshObject("TA镜框_完整椭圆占位", ring, gold, mirrorPivot);
                var handle = Primitive("TA镜柄占位", PrimitiveType.Cube, gold, mirrorPivot);
                handle.transform.localPosition = new Vector3(0, -.26f, -.005f);
                handle.transform.localScale = new Vector3(.032f, .18f, .022f);
                for (int n = 0; n < 4; n++)
                {
                    var fragment = MeshObject("TA占位_已有碎片_" + n, shard, steel, extras.transform);
                    fragment.transform.position = new Vector3(-1.50f + n * .11f, .955f, -1.18f - n % 2 * .1f);
                    fragment.transform.rotation = Quaternion.Euler(0, n * 39, 0);
                }
                AddPaper(extras.transform, paper, wood);
                var probe = camera.gameObject.AddComponent<AtelierTAProbe>();
                probe.Kind = (AtelierProbeKind)index;
                probe.ProbePipeline = pipeline;
                probe.ChineseFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSans/NotoSansCJKsc-Regular.otf");
                probe.RoomPosition = home;
                probe.RoomEuler = euler;
                probe.RoomFov = camera.fieldOfView;
                probe.ClosePosition = desk;
                probe.CloseEuler = deskEuler;
                probe.CloseFov = 48;
                probe.Water = water;
                probe.WaterShader = Shader.Find("RoomDemo/TAProbe/WashContact");
                probe.SupportedBrush = brush;
                probe.BrushSurface = bristles;
                probe.Sky = sky;
                probe.ToolShader = Shader.Find("RoomDemo/TAProbe/ToolSurface");
                probe.MirrorPivot = mirrorPivot;
                if (index == 2)
                {
                    mirrorPivot.position = new Vector3(-1.15f, 1.14f, -1.12f);
                    probe.ClosePosition = new Vector3(-1.65f, 1.60f, -2.60f);
                    probe.CloseEuler = Quaternion.LookRotation(new Vector3(-1.75f, .85f, -.45f) - probe.ClosePosition).eulerAngles;
                    probe.CloseFov = 51;
                    Vector3 normal = (probe.ClosePosition - mirrorPivot.position).normalized + (BoundsOf(bust).center - mirrorPivot.position).normalized;
                    mirrorPivot.rotation = Quaternion.LookRotation(normal.normalized, Vector3.up);
                    var reflection = extras.AddComponent<AtelierMirrorProbe>();
                    reflection.Source = camera;
                    reflection.Surface = face;
                }
                if (index == 3)
                {
                    var surface = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Renderer>(true)).First(x => x.name == "木纹上的蓝灰湿颜料");
                    probe.ToolSurfaces = new[] { surface };
                    probe.SurfaceOrigin = surface.bounds.center;
                    probe.SurfaceSize = new Vector2(surface.bounds.size.x, surface.bounds.size.z);
                    probe.ClosePosition = new Vector3(.35f, 1.52f, -2.05f);
                    probe.CloseEuler = Quaternion.LookRotation(probe.SurfaceOrigin - probe.ClosePosition).eulerAngles;
                    probe.CloseFov = 44;
                }
                if (index == 4)
                {
                    probe.ToolSurfaces = roofs;
                    var plane = new Plane(Vector3.up, new Vector3(0, 4.212f, 0));
                    Ray ray = camera.ViewportPointToRay(new Vector3(.30f, .90f, 0));
                    if (!plane.Raycast(ray, out float distance)) throw new InvalidOperationException("主画幅内找不到屋面验证点。");
                    probe.SurfaceOrigin = ray.GetPoint(distance);
                    probe.SurfaceSize = new Vector2(1.35f, 1.35f);
                    probe.ClosePosition = probe.SurfaceOrigin + new Vector3(0, -1.65f, -1.0f);
                    probe.CloseEuler = Quaternion.LookRotation(probe.SurfaceOrigin - probe.ClosePosition).eulerAngles;
                    probe.CloseFov = 50;
                    var trace = new GameObject("TA屋面_持久局部细痕");
                    trace.transform.SetParent(extras.transform);
                    var line = trace.AddComponent<LineRenderer>();
                    line.sharedMaterial = Material("验证_屋面冷蓝细痕", "Universal Render Pipeline/Unlit", new Color(.12f, .22f, .32f));
                    line.positionCount = 4;
                    line.startWidth = line.endWidth = .006f;
                    for (int n = 0; n < 4; n++) line.SetPosition(n, probe.SurfaceOrigin + new Vector3((n - 1.5f) * .05f, -.004f, Mathf.Sin(n) * .008f));
                    probe.RoofTrace = trace.transform;
                }
                if (index == 3 || index == 4)
                {
                    var knife = new GameObject("TA刮刀_接触方向占位").transform;
                    knife.SetParent(extras.transform);
                    var blade = Primitive("刀面", PrimitiveType.Cube, steel, knife);
                    blade.transform.localScale = new Vector3(.055f, .0035f, .09f);
                    var grip = Primitive("刀柄", PrimitiveType.Cube, wood, knife);
                    grip.transform.localPosition = new Vector3(0, .005f, -.095f);
                    grip.transform.localScale = new Vector3(.022f, .018f, .13f);
                    knife.gameObject.SetActive(false);
                    probe.Knife = knife;
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, destination, false)) throw new IOException("验证副本保存失败：" + destination);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(Folder + "/" + Names[0] + ".unity");
            Debug.Log("[TA验证] 五份独立副本已就绪，正式场景与构建列表保持原样。");
        }

        /// <summary>仅重建本工具生成的五份验证副本与椭圆网格，正式场景不在白名单内。</summary>
        [MenuItem("游戏/技术美术/四阶段TA验证/重建五份验证副本")]
        public static void RebuildProbes()
        {
            RequireClean();
            EditorSceneManager.OpenScene(Source);
            foreach (string name in Names) AssetDatabase.DeleteAsset(Folder + "/" + name + ".unity");
            AssetDatabase.DeleteAsset(Resources + "/Models/椭圆镜框占位.asset");
            AssetDatabase.DeleteAsset(Resources + "/Models/完整椭圆镜面.asset");
            CreateMissing();
        }

        /// <summary>显式构建五个实验场景；不修改项目正式构建列表。</summary>
        [MenuItem("游戏/技术美术/四阶段TA验证/构建macOS验证演示")]
        private static void BuildDemoMenu() => Debug.Log(BuildDemo());

        /// <summary>返回明确构建结果，供CLI调用和人工复建。</summary>
        public static string BuildDemo()
        {
            RequireClean();
            string directory = "Builds/AtelierTAProbes20261019";
            Directory.CreateDirectory(directory);
            var options = new BuildPlayerOptions
            {
                scenes = Names.Select(name => Folder + "/" + name + ".unity").ToArray(),
                locationPathName = directory + "/TA验证演示.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            };
            // 显式收集实验管线的变体；构建后立即归还编辑器原质量管线。
            string qualityPath = "ProjectSettings/QualitySettings.asset";
            byte[] originalQualityBytes = File.ReadAllBytes(qualityPath);
            string previousPipelinePath = AssetDatabase.GetAssetPath(QualitySettings.renderPipeline);
            UnityEditor.Build.Reporting.BuildReport report;
            try
            {
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Resources + "/Rendering/TA验证专用管线.asset");
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                // 构建会卸载原资产，按路径重新读取，不能把已失效的引用写成空管线。
                QualitySettings.renderPipeline = string.IsNullOrEmpty(previousPipelinePath)
                    ? null : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(previousPipelinePath);
                File.WriteAllBytes(qualityPath, originalQualityBytes);
            }
            File.Copy("Assets/ThirdParty/NotoSans/OFL.txt", directory + "/NotoSans-OFL.txt", true);
            string result = "构建结果：" + report.summary.result + "，错误：" + report.summary.totalErrors;
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException(result);
            File.WriteAllText(directory + "/构建结果.txt", result);
            return result;
        }

        [MenuItem("游戏/技术美术/四阶段TA验证/01 局部草稿遮挡")]
        private static void OpenDraft() => Open(0);
        [MenuItem("游戏/技术美术/四阶段TA验证/02 洗笔反馈")]
        private static void OpenWater() => Open(1);
        [MenuItem("游戏/技术美术/四阶段TA验证/03 手镜比较")]
        private static void OpenMirror() => Open(2);
        [MenuItem("游戏/技术美术/四阶段TA验证/04 刮刀干湿表面")]
        private static void OpenPalette() => Open(3);
        [MenuItem("游戏/技术美术/四阶段TA验证/05 屋面试刮")]
        private static void OpenRoof() => Open(4);

        private static void Open(int index)
        {
            RequireClean();
            EditorSceneManager.OpenScene(Folder + "/" + Names[index] + ".unity");
        }

        private static void RequireClean()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出Play，再切换编辑场景。");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("有未保存场景，已停止TA副本操作。");
        }

        private static void ConfigureResidentBatches(UniversalRenderPipelineAsset pipeline)
        {
            var settings = new SerializedObject(pipeline);
            settings.FindProperty("m_GPUResidentDrawerMode").intValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static UniversalRenderPipelineAsset CreatePipeline()
        {
            string path = Resources + "/Rendering/TA验证专用管线.asset";
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (existing != null)
            {
                ConfigureResidentBatches(existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            var source = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            var pipeline = Object.Instantiate(source);
            // 沿用现有GPU批处理变体；演示构建固定使用同一实验管线，避免切场景重建上下文。
            ConfigureResidentBatches(pipeline);
            pipeline.name = "TA验证专用管线";
            AssetDatabase.CreateAsset(pipeline, path);
            var serialized = new SerializedObject(pipeline);
            int defaultIndex = serialized.FindProperty("m_DefaultRendererIndex").intValue;
            var list = serialized.FindProperty("m_RendererDataList");
            var original = (ScriptableRendererData)list.GetArrayElementAtIndex(defaultIndex).objectReferenceValue;
            var renderer = Object.Instantiate(original);
            renderer.name = "TA验证专用Renderer_保留原通道";
            AssetDatabase.CreateAsset(renderer, Resources + "/Rendering/TA验证专用Renderer.asset");
            renderer.rendererFeatures.Clear();
            foreach (var feature in original.rendererFeatures)
            {
                if (feature == null) continue;
                var copy = Object.Instantiate(feature);
                copy.name = feature.name + "_验证副本";
                AssetDatabase.AddObjectToAsset(copy, renderer);
                renderer.rendererFeatures.Add(copy);
            }
            var draft = ScriptableObject.CreateInstance<AtelierDraftProbeFeature>();
            draft.name = "局部底稿验证";
            draft.Configure(Shader.Find("Hidden/RoomDemo/TAProbe/LocalDraft"));
            AssetDatabase.AddObjectToAsset(draft, renderer);
            renderer.rendererFeatures.Add(draft);
            EditorUtility.SetDirty(renderer);
            list.GetArrayElementAtIndex(defaultIndex).objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            return pipeline;
        }

        private static Material Material(string name, string shaderName, Color colour)
        {
            string path = Resources + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("验证Shader未导入：" + shaderName);
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Bounds BoundsOf(GameObject root)
        {
            var list = root.GetComponentsInChildren<Renderer>();
            if (list.Length == 0) throw new InvalidOperationException("模型没有可用Renderer：" + root.name);
            Bounds bounds = list[0].bounds;
            foreach (var renderer in list) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void Fit(GameObject root, float height, Vector3 bottom)
        {
            Bounds bounds = BoundsOf(root);
            root.transform.localScale *= height / Mathf.Max(.001f, bounds.size.y);
            bounds = BoundsOf(root);
            root.transform.position += bottom - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        private static MeshRenderer MeshObject(string name, Mesh mesh, Material material, Transform parent)
        {
            var node = new GameObject(name);
            node.transform.SetParent(parent, false);
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Material material, Transform parent)
        {
            var node = GameObject.CreatePrimitive(type);
            node.name = name;
            node.transform.SetParent(parent, false);
            node.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(node.GetComponent<Collider>());
            return node;
        }

        private static Mesh Ellipse(bool ring)
        {
            string path = Resources + "/Models/" + (ring ? "椭圆镜框占位" : "完整椭圆镜面") + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            const int count = 64;
            var vertices = new Vector3[count * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count;
                var point = new Vector3(Mathf.Cos(a) * .145f, Mathf.Sin(a) * .19f, 0) * (ring ? 1 : .90f);
                vertices[i * 2] = point * (ring ? .91f : 0);
                vertices[i * 2 + 1] = point;
                int next = (i + 1) % count;
                int at = i * 6;
                triangles[at] = i * 2; triangles[at + 1] = i * 2 + 1; triangles[at + 2] = next * 2 + 1;
                triangles[at + 3] = i * 2; triangles[at + 4] = next * 2 + 1; triangles[at + 5] = next * 2;
                uv[i * 2 + 1] = new Vector2(point.x / .29f + .5f, point.y / .38f + .5f);
            }
            var mesh = new Mesh { name = ring ? "椭圆镜框占位" : "完整椭圆镜面", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Mesh Shard()
        {
            string path = Resources + "/Models/已有碎片占位.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var mesh = new Mesh { name = "已有碎片占位", vertices = new[] { new Vector3(-.03f, 0, -.025f), new Vector3(.04f, 0, -.01f), new Vector3(-.015f, 0, .04f) }, triangles = new[] { 0, 2, 1 } };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void AddPaper(Transform parent, Material paper, Material wood)
        {
            var root = new GameObject("TA占位_窗台蜡笔画位置").transform;
            root.SetParent(parent);
            root.position = new Vector3(2.24f, 1.205f, 1.91f);
            var frame = Primitive("纸模占位框", PrimitiveType.Cube, wood, root);
            frame.transform.localScale = new Vector3(.27f, .31f, .02f);
            var sheet = Primitive("正面仅彩色笔触占位", PrimitiveType.Cube, paper, root);
            sheet.transform.localPosition = new Vector3(0, 0, -.013f);
            sheet.transform.localScale = new Vector3(.24f, .28f, .004f);
            Color[] colours = { new Color(.06f, .18f, .42f), new Color(.55f, .25f, .05f), new Color(.16f, .32f, .13f) };
            for (int n = 0; n < colours.Length; n++)
            {
                var node = new GameObject("无叙事题材的蜡笔线_" + n);
                node.transform.SetParent(root, false);
                var line = node.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.sharedMaterial = Material("验证_彩色笔触_" + n, "Universal Render Pipeline/Unlit", colours[n]);
                line.startWidth = line.endWidth = .004f;
                line.positionCount = 16;
                for (int i = 0; i < 16; i++) line.SetPosition(i, new Vector3(-.09f + i * .012f, Mathf.Sin(i * .8f + n) * .035f + (n - 1) * .045f, -.018f));
            }
        }
    }
}
