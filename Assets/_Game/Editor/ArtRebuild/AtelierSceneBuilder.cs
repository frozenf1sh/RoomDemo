// 文件：AtelierSceneBuilder.cs
// 职责：通过 Unity Editor API 分阶段搭建独立夜间画室，保存可编辑场景与专属美术资源。
// 架构边界：不实例化旧场景、不接入旧玩法，不修改领域状态、交互身份或碰撞。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Unity.TechnicalArt.RealtimePainterly;
using Game.Unity.TechnicalArt.RealtimePainterly.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Game.Editor.ArtRebuild
{
    /// <summary>新美术场景的阶段入口；所有新增资源均由独立目录持有。</summary>
    public static class AtelierSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Content/Scenes/AtelierNight.unity";
        private const string Art = "Assets/_Game/Content/ArtRebuild/";
        private const string Existing = "Assets/_Game/Content/Studio/Art/Imports/";
        private static Material wood, floor, plaster, linen, paper, brass, ceramic, canvas, slate, soil, leaves, lampShade;
        private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        /// <summary>建立首轮建筑、主镜头和真实家具；存在目标场景时拒绝覆盖。</summary>
        [MenuItem("游戏/独立美术画室/01 建立空间与家具")]
        public static void CreateRoom()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式。");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("独立画室已存在，请使用增量阶段入口，不能重建覆盖。");
            Prepare();
            // Editor 刚启动时的未命名默认场景不能 additive；仅在确认未修改时替换。
            var previous = EditorSceneManager.GetActiveScene();
            bool emptySession = string.IsNullOrEmpty(previous.path) && !previous.isDirty;
            if (string.IsNullOrEmpty(previous.path) && previous.isDirty)
                throw new InvalidOperationException("存在未保存的人工场景，请先保存；本工具没有覆盖该场景。");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, emptySession ? NewSceneMode.Single : NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var root = new GameObject("独立夜间画室").transform;
            var shell = Group(root, "建筑与木作");
            Architecture(shell);
            var furniture = Group(root, "室内家具");
            Furniture(furniture);
            var camera = new GameObject("画室主镜头", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.transform.SetParent(root);
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 1.92f, -5.00f);
            camera.transform.rotation = Quaternion.Euler(-5.4f, 0, 0);
            camera.fieldOfView = 61;
            camera.nearClipPlane = .06f;
            camera.farClipPlane = 700;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.012f, .021f, .046f);
            camera.allowHDR = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            Lights(Group(root, "照明"));
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.21f, .26f, .37f);
            RenderSettings.ambientEquatorColor = new Color(.18f, .16f, .14f);
            RenderSettings.ambientGroundColor = new Color(.075f, .065f, .055f);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = camera.gameObject;
            Debug.Log("独立美术画室：第一阶段建筑、家具和主镜头已保存；尚未设置构建入口。");
        }

        /// <summary>增量摆放桌面工作簇和背景物品；不重建第一阶段家具。</summary>
        [MenuItem("游戏/独立美术画室/02 摆放工作簇")]
        public static void DressRoom()
        {
            Prepare();
            var root = Root();
            if (root.Find("桌面与创作痕迹") != null) throw new InvalidOperationException("工作簇已存在，请直接编辑场景中的实例。");
            var props = Group(root, "桌面与创作痕迹");
            WorkCluster(props);
            StorageProps(props);
            Save();
            Debug.Log("独立美术画室：第二阶段桌面、陶器、笔刷与纸张已保存。");
        }

        /// <summary>独立建立真实纵深的外景；房屋和树木不进入任何旧表现层退场列表。</summary>
        [MenuItem("游戏/独立美术画室/03 建立山谷村落")]
        public static void CreateLandscape()
        {
            Prepare();
            var root = Root();
            if (root.Find("窗外山谷") != null) throw new InvalidOperationException("外景已存在，请直接编辑外景分组。");
            var landscape = Group(root, "窗外山谷");
            Landscape(landscape);
            Save();
            Debug.Log("独立美术画室：第三阶段前景树枝、中景小屋和远山已保存。");
        }

        /// <summary>接入现有 GPU 颜料星夜生产预设；覆盖与流动仅是美术初态。</summary>
        [MenuItem("游戏/独立美术画室/04 接入程序化流动星夜")]
        public static void ConnectSky()
        {
            var root = Root();
            if (root.GetComponentInChildren<LivingSkyController>() != null) throw new InvalidOperationException("程序化天空已接入。");
            string path = "Assets/_Game/Content/TechnicalArt/Production/Prefabs/LivingSky.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("缺少生产星空预设：" + path);
            var sky = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            sky.name = "持续流动的程序化星夜";
            var controller = sky.GetComponent<LivingSkyController>();
            controller.InitialCoverage = 1;
            controller.InitialFlow = 1;
            controller.InitialLight = .72f;
            controller.Opacity = 1;
            controller.Paused = false;
            controller.SeparateLightAndFlow = false;
            controller.PanoramaRepeats = 3;
            controller.SkyYawDegrees = -13;
            controller.SkyElevationDegrees = 29;
            controller.BindCamera(root.GetComponentInChildren<Camera>());
            controller.Pigment.Width = 1536;
            controller.Pigment.Height = 864;
            // 材质单独复制，保持原生产件、共享 Shader 与现有用户改动原样。
            var own = new Material(controller.Surface.sharedMaterial) { name = "画室程序化天空" };
            AssetDatabase.CreateAsset(own, Art + "Materials/画室程序化天空.mat");
            controller.Surface.sharedMaterial = own;
            Save();
            Debug.Log("独立美术画室：现有 LivingSkyController / GpuPigment 已绑定；静态星空贴图没有进入新场景。");
        }

        /// <summary>保存当前独立画室；拒绝向其他活动场景写入。</summary>
        public static void Save()
        {
            Root();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        /// <summary>核验场景绑定与美术资产依赖，报告不等于真人审美通过。</summary>
        public static string Inspect()
        {
            var root = Root();
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var missing = root.GetComponentsInChildren<Transform>(true).Sum(x => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(x.gameObject));
            var sky = root.GetComponentInChildren<LivingSkyController>();
            string[] dependencies = AssetDatabase.GetDependencies(ScenePath, true);
            string[] forbidden = dependencies.Where(x => x.Contains("NarrativeNightSky") || x.Contains("QuietStarrySky") || x.StartsWith("Docs/")).ToArray();
            return $"场景={ScenePath}\nRenderer={renderers.Length}\n缺失脚本={missing}\n空材质={renderers.Sum(x => x.sharedMaterials.Count(m => m == null))}\n程序化天空={sky != null && sky.IsConfigured}\n禁止贴图依赖={string.Join(",", forbidden)}\n碰撞组件={root.GetComponentsInChildren<Collider>(true).Length}\n构建入口={string.Join(",", EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path))}";
        }

        /// <summary>完成视觉检查后明确设置唯一的新入口；不启用归档场景。</summary>
        public static void SetBuildEntry()
        {
            Root();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Build Settings 已设置为独立美术画室 AtelierNight，旧场景仍归档。");
        }

        /// <summary>根据实看结果修正导入轴与自然尺寸，只编辑本场景专属实例。</summary>
        public static void CorrectImportedPlacement()
        {
            var root = Root();
            var props = root.Find("桌面与创作痕迹");
            for (int i = 0; i < 3; i++)
            {
                var holder = props.Find("使用过的画笔_" + i);
                var source = Load<GameObject>(Art + "Imports/LumiPalette/SM_Brush_0" + (i + 1) + "a.fbx");
                var child = holder.GetChild(0);
                holder.localScale = Vector3.one;
                holder.localRotation = Quaternion.identity;
                holder.localPosition = Vector3.zero;
                child.localRotation = source.transform.localRotation;
                Bounds bounds = BoundsOf(holder);
                Vector3 longest = bounds.size.x > bounds.size.y && bounds.size.x > bounds.size.z ? Vector3.right : bounds.size.z > bounds.size.y ? Vector3.forward : Vector3.up;
                // 先按最长轴扶正，再施加杯中的轻微倾斜，避免用极薄的横向高度归一化。
                child.localRotation = Quaternion.Euler(-8 + i * 8, i * 51, -8 + i * 8) * Quaternion.FromToRotation(longest, Vector3.up) * source.transform.localRotation;
                bounds = BoundsOf(holder);
                float scale = (.44f + i * .025f) / bounds.size.y;
                holder.localScale = Vector3.one * scale;
                holder.localPosition = new Vector3(.71f + i * .028f, 1.135f, -.74f) - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
            }
            var window = root.Find("建筑与木作/旧窗与亚麻帘");
            for (int i = 0; i < 2; i++)
            {
                var holder = window.Find("扫描旧木窗_" + i);
                holder.localRotation = Quaternion.Euler(0, 180, 0);
                holder.localPosition = new Vector3(.47f + i * 1.76f, .97f, 4.12f);
                // 扫描原点在窗框外；旋转后重新用包围盒把底部与中心对齐。
                var r = holder.GetComponentInChildren<Renderer>();
                holder.position += new Vector3(.47f + i * 1.76f - r.bounds.center.x, .97f - r.bounds.min.y, 4.12f - r.bounds.center.z);
            }
            void Replace(string name, Vector3 pos, Vector3 size)
            {
                var node = root.Find("建筑与木作/" + name);
                node.localPosition = pos;
                node.GetComponent<MeshFilter>().sharedMesh = AtelierMeshLibrary.Save(name + "_宽窗修正", AtelierMeshLibrary.Timber(size, .025f));
            }
            Replace("窗左墙", new Vector3(-2.35f, 2.1f, 4.17f), new Vector3(3.9f, 4.2f, .32f));
            Replace("窗右墙", new Vector3(3.7f, 2.1f, 4.17f), new Vector3(1.2f, 4.2f, .32f));
            Replace("窗下墙", new Vector3(1.35f, .48f, 4.17f), new Vector3(3.5f, .96f, .32f));
            Replace("窗上墙", new Vector3(1.35f, 3.91f, 4.17f), new Vector3(3.5f, .58f, .32f));
            window.Find("亚麻帘褶_0").localPosition = new Vector3(-.59f, 3.65f, 3.70f);
            window.Find("亚麻帘褶_1").localPosition = new Vector3(3.29f, 3.65f, 3.70f);
            var windowMat = Load<Material>(Art + "Materials/窗框扫描旧木.mat");
            windowMat.SetFloat("_Cull", 0);
            var light = root.Find("照明/夜间微弱环境方向光").GetComponent<Light>();
            light.intensity = .72f;
            light.color = new Color(.65f, .69f, .79f);
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(new Color(.28f, .23f, .18f));
            RenderSettings.ambientProbe = sh;
            Save();
        }

        /// <summary>修复首轮运行发现的材质与空间问题；修改范围仅为新场景和新目录。</summary>
        public static void RefineSurfacesAndLight()
        {
            var root = Root();
            var roofNode = root.Find("建筑与木作/有厚度的擦开屋顶");
            roofNode.GetComponent<MeshFilter>().sharedMesh = AtelierMeshLibrary.Save("大面积不规则屋顶", AtelierMeshLibrary.Roof());
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Art + "Materials" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (!material.HasProperty("_EmissionColor") || material.GetColor("_EmissionColor").maxColorComponent < .001f) continue;
                // URP 校验用 GI 标记决定发光关键字，显式移除 EmissiveIsBlack，避免保存后被清掉。
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                material.EnableKeyword("_EMISSION");
                EditorUtility.SetDirty(material);
            }
            Load<Material>(Art + "Materials/受光亚麻灯罩.mat").SetColor("_EmissionColor", new Color(1.25f, .49f, .12f));
            Load<Material>(Art + "Materials/窗框扫描旧木.mat").SetColor("_BaseColor", new Color(.78f, .66f, .48f));
            Load<Material>(Art + "Materials/旧亚麻.mat").SetColor("_BaseColor", new Color(.76f, .68f, .52f));
            Load<Material>(Art + "Materials/旧亚麻.mat").SetFloat("_BumpScale", .24f);
            Load<Material>(Art + "Materials/旧亚麻.mat").SetFloat("_Smoothness", .025f);
            var sky = root.GetComponentInChildren<LivingSkyController>();
            sky.Surface.sharedMaterial.shader = Shader.Find("RoomDemo/ArtRebuild/AtelierLivingSky");
            sky.Surface.sharedMaterial.SetFloat("_NightExposure", .85f);
            sky.SkyElevationDegrees = 46;
            sky.SkyYawDegrees = 28;
            sky.PanoramaRepeats = 4;
            root.Find("窗外山谷").localPosition = new Vector3(0, 1.6f, 0);
            var lighting = root.Find("照明");
            var bounce = lighting.Find("窗台与帘褶的蓝色反弹").GetComponent<Light>();
            bounce.intensity = .83f;
            bounce.range = 3.7f;
            bounce.transform.position = new Vector3(1.35f, 2.4f, 3.38f);
            var warm = lighting.Find("桌灯向下的暖工作区").GetComponent<Light>();
            warm.transform.position = new Vector3(1.34f, 1.43f, -.56f);
            warm.transform.rotation = Quaternion.Euler(90, 0, 0);
            warm.spotAngle = 145;
            warm.innerSpotAngle = 105;
            warm.intensity = 5.5f;
            warm.range = 3.1f;
            if (lighting.Find("桌灯在画室中的漫反弹") == null)
            {
                var fill = new GameObject("桌灯在画室中的漫反弹", typeof(Light)).GetComponent<Light>();
                fill.transform.SetParent(lighting);
                fill.transform.position = new Vector3(.85f, 1.72f, -.44f);
                fill.type = LightType.Point;
                fill.color = new Color(1, .67f, .35f);
                fill.intensity = .70f;
                fill.range = 4.7f;
            }
            Save();
        }

        /// <summary>只准备独立素材目录和材质，不改旧资源的导入设置。</summary>
        private static void Prepare()
        {
            foreach (string name in new[] { "Materials", "Meshes", "Textures", "Prefabs" }) Directory.CreateDirectory(Art + name);
            AssetDatabase.Refresh();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Art + "Imports" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.Contains("Normal");
                bool data = normal || path.Contains("Roughness") || path.Contains("ORM") || path.Contains("_rough_") || path.Contains("_ao_") || path.Contains("_AO") || path.Contains("Metallic");
                bool changed = importer.maxTextureSize != 2048 || importer.sRGBTexture == data || normal && importer.textureType != TextureImporterType.NormalMap;
                if (!changed) continue;
                importer.maxTextureSize = 2048;
                importer.sRGBTexture = !data;
                importer.wrapMode = TextureWrapMode.Repeat;
                if (normal) importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Art + "Imports" }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
                if (importer == null || !importer.importAnimation && !importer.importCameras && !importer.importLights && importer.materialImportMode == ModelImporterMaterialImportMode.None) continue;
                importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
            wood = Lit("深暖旧木", new Color(.61f, .47f, .31f), Find("old_wooden_floor_03_diff"), Find("old_wooden_floor_03_nor"), .22f);
            floor = Lit("磨损木地板", new Color(.83f, .74f, .60f), Find("old_wooden_floor_03_diff"), Find("old_wooden_floor_03_nor"), .23f);
            plaster = Lit("暖灰灰泥", new Color(.72f, .69f, .60f), Find("plastered_wall_04_diff"), Find("plastered_wall_04_nor"), .08f);
            linen = Lit("旧亚麻", new Color(.54f, .48f, .36f), Load<Texture2D>("Assets/ThirdParty/PolyHaven/RoughLinen/Diffuse.jpg"), Load<Texture2D>("Assets/ThirdParty/PolyHaven/RoughLinen/nor_gl.jpg"), .08f);
            linen.SetFloat("_Cull", 0);
            paper = Lit("暖灰画纸", new Color(.71f, .65f, .51f), Load<Texture2D>("Assets/ThirdParty/PolyHaven/RoughLinen/Diffuse.jpg"), null, .05f);
            paper.SetFloat("_Cull", 0);
            canvas = Lit("绷布画面", new Color(.66f, .60f, .46f), Load<Texture2D>("Assets/ThirdParty/PolyHaven/RoughLinen/Diffuse.jpg"), Load<Texture2D>("Assets/ThirdParty/PolyHaven/RoughLinen/nor_gl.jpg"), .08f);
            brass = Lit("旧黄铜", new Color(.36f, .24f, .105f), null, null, .46f); brass.SetFloat("_Metallic", .72f);
            ceramic = Lit("灰绿陶釉", new Color(.23f, .28f, .235f), null, null, .41f);
            slate = Lit("风化蓝灰板瓦", new Color(.16f, .20f, .25f), Find("plastered_wall_04_diff"), Find("plastered_wall_04_nor"), .14f);
            soil = Lit("山坡夜色", new Color(.12f, .17f, .13f), Find("plastered_wall_04_diff"), Find("plastered_wall_04_nor"), .03f);
            leaves = Lit("深绿叶片", new Color(.075f, .13f, .075f), null, null, .15f); leaves.SetFloat("_Cull", 0);
            lampShade = Lit("受光亚麻灯罩", new Color(.70f, .49f, .25f), Load<Texture2D>("Assets/ThirdParty/PolyHaven/RoughLinen/Diffuse.jpg"), Load<Texture2D>("Assets/ThirdParty/PolyHaven/RoughLinen/nor_gl.jpg"), .11f);
            lampShade.SetFloat("_Cull", 0); lampShade.EnableKeyword("_EMISSION"); lampShade.SetColor("_EmissionColor", new Color(.39f, .15f, .035f));
            meshes.Clear();
        }

        /// <summary>室内厚墙、窗洞、倒角地板、梁木和大面积不规则屋顶显露。</summary>
        private static void Architecture(Transform root)
        {
            // 窗洞真实贯通厚墙，窗外几何位于墙后，不在玻璃平面上。
            Beam(root, "左侧厚灰泥墙", new Vector3(-4.40f, 2.1f, 0), new Vector3(.25f, 4.2f, 8.7f), plaster, .025f);
            Beam(root, "右侧厚灰泥墙", new Vector3(4.40f, 2.1f, 0), new Vector3(.25f, 4.2f, 8.7f), plaster, .025f);
            Beam(root, "窗左墙", new Vector3(-2.15f, 2.1f, 4.17f), new Vector3(4.30f, 4.2f, .32f), plaster, .035f);
            Beam(root, "窗右墙", new Vector3(3.50f, 2.1f, 4.17f), new Vector3(1.60f, 4.2f, .32f), plaster, .035f);
            Beam(root, "窗下墙", new Vector3(1.35f, .48f, 4.17f), new Vector3(2.70f, .96f, .32f), plaster, .025f);
            Beam(root, "窗上墙", new Vector3(1.35f, 3.91f, 4.17f), new Vector3(2.70f, .58f, .32f), plaster, .025f);
            var floorGroup = Group(root, "真实厚边错缝地板");
            for (int x = 0; x < 29; x++)
            for (int z = 0; z < 4; z++)
            {
                float center = -3.18f + z * 2.18f + (x % 2) * .40f;
                Beam(floorGroup, "旧木板_" + x + "_" + z, new Vector3(-4.20f + x * .30f, -.045f, center), new Vector3(.294f, .09f, 2.16f), floor, .005f);
            }
            Node(root, "有厚度的擦开屋顶", AtelierMeshLibrary.Save("大面积不规则屋顶", AtelierMeshLibrary.Roof()), plaster, Vector3.zero);
            foreach (float x in new[] { -3.75f, -2.05f, 2.05f, 3.75f })
                Beam(root, "纵向旧木梁_" + x, new Vector3(x, 4.07f, 0), new Vector3(.23f, .30f, 8.6f), wood, .025f);
            Beam(root, "后墙承重横梁", new Vector3(0, 3.93f, 3.95f), new Vector3(8.65f, .29f, .25f), wood, .025f);
            Beam(root, "前景承重横梁", new Vector3(0, 4.02f, -3.9f), new Vector3(8.65f, .25f, .27f), wood, .025f);
            foreach (float x in new[] { -4.23f, 4.23f })
                Beam(root, "墙角木立柱_" + x, new Vector3(x, 2.01f, 3.98f), new Vector3(.21f, 4.02f, .22f), wood, .018f);
            Beam(root, "墙根压脚木", new Vector3(0, .12f, 3.94f), new Vector3(8.4f, .24f, .11f), wood, .012f);

            var window = Group(root, "旧窗与亚麻帘");
            var scan = Pbr("窗框扫描旧木", Art + "Imports/QuixelWindow/", "BaseColor", "Normal", "Roughness", "AO", Color.white);
            // 两扇真实扫描木窗组成宽窗；周围厚窗套承接原始窄框与灰泥墙。
            for (int i = 0; i < 2; i++)
                Model(window, "扫描旧木窗_" + i, Art + "Imports/QuixelWindow/Old_Wooden_Window_vmfwcabdw_Mid.fbx", new Vector3(.68f + i * 1.35f, .97f, 4.12f), 2.64f, scan, Vector3.zero);
            Beam(window, "厚实窗台", new Vector3(1.35f, .96f, 3.89f), new Vector3(3.08f, .13f, .64f), wood, .027f);
            Beam(window, "旧木窗楣", new Vector3(1.35f, 3.70f, 3.96f), new Vector3(3.02f, .20f, .26f), wood, .023f);
            for (int i = 0; i < 2; i++)
            {
                var curtain = Node(window, "亚麻帘褶_" + i, AtelierMeshLibrary.Save("亚麻帘_" + i, AtelierMeshLibrary.Curtain(.67f, 2.70f, i)), linen, new Vector3(-.20f + i * 3.10f, 3.65f, 3.70f));
                curtain.localRotation = Quaternion.Euler(0, 180, 0);
                for (int j = 0; j < 8; j++)
                    Lathe(window, "帘环_" + i + "_" + j, new[] { new Vector2(.020f, 0), new Vector2(.029f, .007f), new Vector2(.020f, .015f) }, brass, new Vector3(-.47f + i * 3.1f + j * .075f, 3.72f, 3.69f));
            }
            Link(window, "旧铜帘杆", new Vector3(-.7f, 3.75f, 3.71f), new Vector3(3.4f, 3.75f, 3.71f), .018f, brass);
        }

        /// <summary>复用扫描家具；英雄画架采用带托槽、斜撑与紧固件的专属木作。</summary>
        private static void Furniture(Transform root)
        {
            var tableMat = Pbr("涂漆工作台", Existing + "PolyHaven/painted_wooden_table/", "_diff_", "_nor_", null, null, new Color(.81f, .76f, .65f));
            Rough(tableMat, Find("painted_wooden_table_rough"), Find("painted_wooden_table_ao"));
            Model(root, "有磨损的扫描工作台", Existing + "PolyHaven/painted_wooden_table/painted_wooden_table_2k.fbx", new Vector3(.45f, 0, -.72f), 1.03f, tableMat, Vector3.zero, 3.1f);
            var chair = Pbr("旧漆木椅", Existing + "PolyHaven/painted_wooden_chair_01/", "_diff_", "_nor_", null, null, new Color(.70f, .60f, .42f));
            Rough(chair, Find("painted_wooden_chair_01_rough"), null);
            Model(root, "后移的工作椅", Existing + "PolyHaven/painted_wooden_chair_01/painted_wooden_chair_01_2k.fbx", new Vector3(1.9f, 0, -.10f), 1.05f, chair, new Vector3(0, -26, 0));
            var stool = Pbr("旧木凳", Existing + "PolyHaven/painted_wooden_stool/", "_diff_", "_nor_", null, null, new Color(.76f, .65f, .47f));
            Model(root, "画架旁旧凳", Existing + "PolyHaven/painted_wooden_stool/painted_wooden_stool_2k.fbx", new Vector3(-2.9f, 0, .55f), .59f, stool, new Vector3(0, 18, 0));
            var bookcase = Pbr("雕脚书柜旧木", Art + "Imports/KragBookcase/", "BaseColor", "Normal", "ORM", "ORM", new Color(.74f, .68f, .54f));
            Model(root, "Krag 雕脚旧书柜", Art + "Imports/KragBookcase/AntiqueBookcase.obj", new Vector3(3.65f, 0, 2.64f), 2.85f, bookcase, new Vector3(0, 180, 0));
            var cabinet = Pbr("磨损柜门", Existing + "PolyHaven/painted_wooden_cabinet_02/", "_diff_", "_nor_", "_rough_", "_ao_", new Color(.68f, .62f, .49f));
            Model(root, "左侧旧立柜", Existing + "PolyHaven/painted_wooden_cabinet_02/painted_wooden_cabinet_02_1k.fbx", new Vector3(-3.62f, 0, 3.55f), 2.5f, cabinet, new Vector3(0, 180, 0));
            Easel(Group(root, "细木作画架"));
        }

        /// <summary>画架保留倾角、托板厚边、滑槽、木销和铜螺杆的实际结构。</summary>
        private static void Easel(Transform root)
        {
            root.localPosition = new Vector3(-1.92f, 0, 1.60f);
            root.localRotation = Quaternion.Euler(0, -7, 0);
            foreach (float x in new[] { -.50f, .50f })
            {
                var leg = Beam(root, "斜立画架木脚", new Vector3(x, 1.16f, .12f), new Vector3(.11f, 2.34f, .14f), wood, .013f);
                leg.localRotation = Quaternion.Euler(-8, 0, x < 0 ? -6 : 6);
            }
            var back = Beam(root, "后撑脚", new Vector3(0, .92f, .56f), new Vector3(.12f, 1.94f, .14f), wood, .013f);
            back.localRotation = Quaternion.Euler(24, 0, 0);
            Beam(root, "中间升降立柱", new Vector3(0, 1.65f, .15f), new Vector3(.145f, 2.65f, .14f), wood, .014f);
            Beam(root, "下横撑", new Vector3(0, .45f, .12f), new Vector3(1.0f, .10f, .12f), wood, .013f);
            Beam(root, "画布承托厚边", new Vector3(0, .94f, -.12f), new Vector3(1.47f, .13f, .30f), wood, .018f);
            Beam(root, "承托前挡唇", new Vector3(0, 1.02f, -.255f), new Vector3(1.47f, .055f, .055f), wood, .009f);
            Beam(root, "上方夹具", new Vector3(0, 2.53f, -.03f), new Vector3(.42f, .11f, .24f), wood, .014f);
            for (int i = 0; i < 2; i++)
            {
                var screw = Lathe(root, "铜紧固螺杆_" + i, new[] { new Vector2(.017f, 0), new Vector2(.017f, .07f), new Vector2(.040f, .078f), new Vector2(.040f, .10f), new Vector2(0, .11f) }, brass, new Vector3(0, i == 0 ? .86f : 2.55f, -.13f));
                screw.localRotation = Quaternion.Euler(-90, 0, 0);
            }
            var painting = Group(root, "绷布与木框");
            painting.localPosition = new Vector3(0, 1.77f, -.05f);
            painting.localRotation = Quaternion.Euler(-8, 0, 0);
            Beam(painting, "有侧边的亚麻画布", Vector3.zero, new Vector3(1.35f, 1.49f, .055f), canvas, .009f);
            foreach (float x in new[] { -.68f, .68f }) Beam(painting, "画框竖边", new Vector3(x, 0, -.005f), new Vector3(.055f, 1.58f, .085f), wood, .012f);
            foreach (float y in new[] { -.77f, .77f }) Beam(painting, "画框横边", new Vector3(0, y, -.005f), new Vector3(1.40f, .055f, .085f), wood, .012f);
        }

        /// <summary>一盏桌灯、一组杯笔、一块调色板和三张纸；每组都对应作画动作。</summary>
        private static void WorkCluster(Transform root)
        {
            float top = 1.045f;
            var palette = Pbr("LUMI 使用过的调色板", Art + "Imports/LumiPalette/", "T_Palette_02_BaseColor", "T_Palette_02_Normal", "T_Palette_02_Roughness", "T_Palette_02_AO", new Color(.81f, .78f, .68f));
            Model(root, "带颜料的真实调色板", Art + "Imports/LumiPalette/SM_Palette_02a.fbx", new Vector3(.19f, top + .012f, -.95f), .04f, palette, new Vector3(0, 23, 0), .51f);
            var brushMat = Pbr("LUMI 旧画笔", Art + "Imports/LumiPalette/", "T_Brush_01_BaseColor", "T_Brush_01_Normal", "T_Brush_01_Roughness", "T_Brush_01_AO", new Color(.86f, .81f, .69f));
            var cup = Lathe(root, "厚口陶瓷水杯", new[] { new Vector2(.085f, 0), new Vector2(.10f, .015f), new Vector2(.112f, .055f), new Vector2(.12f, .18f), new Vector2(.126f, .205f), new Vector2(.113f, .214f), new Vector2(.102f, .196f), new Vector2(.096f, .03f), new Vector2(0, .03f) }, ceramic, new Vector3(.73f, top, -.74f));
            for (int i = 0; i < 3; i++)
                Model(root, "使用过的画笔_" + i, Art + "Imports/LumiPalette/SM_Brush_0" + (i + 1) + "a.fbx", new Vector3(.71f + i * .028f, top + .09f, -.74f), .44f + i * .025f, brushMat, new Vector3(-12 + i * 12, i * 51, -9 + i * 9));
            var vase = Pbr("小陶瓶扫描", Existing + "PolyHaven/antique_ceramic_vase_01/", "_diff_", "_nor_", "_rough_", "_ao_", new Color(.70f, .66f, .51f));
            Model(root, "备用陶器", Existing + "PolyHaven/antique_ceramic_vase_01/antique_ceramic_vase_01_1k.fbx", new Vector3(1.55f, top, -.44f), .25f, vase, Vector3.zero);
            for (int i = 0; i < 3; i++)
            {
                var sheet = Node(root, "微翘草稿纸_" + i, AtelierMeshLibrary.Save("草稿曲面_" + i, AtelierMeshLibrary.Paper(.48f, .36f, i)), paper, new Vector3(-.54f + i * .045f, top + .007f + i * .005f, -.72f + i * .025f));
                sheet.localRotation = Quaternion.Euler(0, -13 + i * 8, 0);
            }
            Node(root, "桌沿旧亚麻抹布", AtelierMeshLibrary.Save("桌沿垂布", AtelierMeshLibrary.Drape()), linen, new Vector3(1.23f, top, -1.22f));
            var lamp = Group(root, "有实体灯罩的黄铜桌灯");
            lamp.localPosition = new Vector3(1.34f, top, -.56f);
            Lathe(lamp, "铜灯座", new[] { new Vector2(0, 0), new Vector2(.16f, 0), new Vector2(.17f, .02f), new Vector2(.15f, .045f), new Vector2(.09f, .065f), new Vector2(.06f, .12f), new Vector2(.11f, .18f), new Vector2(.12f, .25f), new Vector2(.09f, .32f), new Vector2(.028f, .36f), new Vector2(.028f, .48f) }, brass, Vector3.zero);
            Lathe(lamp, "亚麻灯罩实体", new[] { new Vector2(.24f, .40f), new Vector2(.245f, .412f), new Vector2(.15f, .75f), new Vector2(.142f, .756f), new Vector2(.148f, .74f), new Vector2(.232f, .415f) }, lampShade, Vector3.zero);
            Lathe(lamp, "灯罩上铜帽", new[] { new Vector2(.026f, 0), new Vector2(.032f, .015f), new Vector2(.018f, .035f), new Vector2(0, .047f) }, brass, new Vector3(0, .756f, 0));
        }

        /// <summary>背景储物只成组摆放，保留墙面和家具剪影的呼吸空间。</summary>
        private static void StorageProps(Transform root)
        {
            var vase = Pbr("窗台旧陶瓶", Existing + "PolyHaven/antique_ceramic_vase_01/", "_diff_", "_nor_", "_rough_", "_ao_", new Color(.71f, .68f, .57f));
            Model(root, "窗台陶瓶", Existing + "PolyHaven/antique_ceramic_vase_01/antique_ceramic_vase_01_1k.fbx", new Vector3(2.42f, 1.03f, 3.88f), .29f, vase, Vector3.zero);
            var bookMat = Lit("旧书布封", new Color(.23f, .20f, .13f), Load<Texture2D>("Assets/ThirdParty/PolyHaven/RoughLinen/Diffuse.jpg"), null, .15f);
            for (int i = 0; i < 5; i++)
            {
                var book = Group(root, "书柜旧书_" + i);
                book.localPosition = new Vector3(3.37f + i * .11f, .73f, 2.44f);
                book.localRotation = Quaternion.Euler(0, 0, i == 4 ? -13 : 0);
                Beam(book, "纸页切面", new Vector3(0, .19f, 0), new Vector3(.075f, .37f, .22f), paper, .006f);
                foreach (float x in new[] { -.043f, .043f }) Beam(book, "硬布封皮", new Vector3(x, .19f, 0), new Vector3(.009f, .40f, .24f), bookMat, .004f);
                Beam(book, "弧脊书背", new Vector3(0, .19f, -.116f), new Vector3(.095f, .40f, .018f), bookMat, .008f);
            }
            var notepad = Pbr("扫描绘画便笺", Existing + "PolyHaven/office_notepads/", "_diff_", "_nor_", null, null, new Color(.72f, .66f, .51f));
            Model(root, "窗台参考纸本", Existing + "PolyHaven/office_notepads/office_notepads_2k.fbx", new Vector3(.30f, 1.035f, 3.82f), .035f, notepad, new Vector3(0, 8, 0), .44f);
        }

        /// <summary>固定镜头下的两条主光与低强度环境；所有实时灯均有明确空间来处。</summary>
        private static void Lights(Transform root)
        {
            var cool = Spot(root, "从窗洞进入的冷月光", new Vector3(1.35f, 2.9f, 4.9f), new Vector3(.3f, 1.20f, .6f), new Color(.35f, .53f, 1), 4.1f, 8.8f, 103);
            cool.shadows = LightShadows.Soft;
            cool.shadowBias = .035f;
            // 反弹补光靠近厚窗台，较短范围限制蓝光只影响邻墙和帘褶。
            var bounce = new GameObject("窗台与帘褶的蓝色反弹", typeof(Light)).GetComponent<Light>();
            bounce.transform.SetParent(root); bounce.transform.position = new Vector3(1.35f, 2.5f, 3.64f);
            bounce.type = LightType.Point; bounce.color = new Color(.30f, .48f, 1); bounce.intensity = .53f; bounce.range = 3.15f;
            var warm = Spot(root, "桌灯向下的暖工作区", new Vector3(1.34f, 1.65f, -.56f), new Vector3(.35f, 1.03f, -.79f), new Color(1, .64f, .29f), 4.8f, 3.4f, 118);
            warm.shadows = LightShadows.Soft;
            warm.shadowBias = .015f;
            var ambient = new GameObject("夜间微弱环境方向光", typeof(Light)).GetComponent<Light>();
            ambient.transform.SetParent(root); ambient.transform.rotation = Quaternion.Euler(46, -29, 0);
            ambient.type = LightType.Directional; ambient.color = new Color(.38f, .49f, .68f); ambient.intensity = .27f;
            ambient.shadows = LightShadows.Soft; ambient.shadowStrength = .5f;
            RenderSettings.sun = ambient;
        }

        /// <summary>外景具体网格在专属部分实现。</summary>
        private static void Landscape(Transform root)
        {
            AtelierLandscape.Build(root, wood, plaster, slate, soil, leaves, brass);
        }

        /// <summary>只返回独立美术场景根，防止误操作旧场景。</summary>
        private static Transform Root()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("请先打开独立画室 AtelierNight。");
            return scene.GetRootGameObjects().Single(x => x.name == "独立夜间画室").transform;
        }

        /// <summary>创建无碰撞的纯美术层级。</summary>
        public static Transform Group(Transform parent, string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj.transform;
        }

        /// <summary>建立 Renderer 和明确的资产引用，不创建基础体或逻辑组件。</summary>
        public static Transform Node(Transform parent, string name, Mesh mesh, Material material, Vector3 position)
        {
            var node = Group(parent, name);
            node.localPosition = position;
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return node;
        }

        /// <summary>制作倒角木作并缓存同尺寸网格，减少资源重复。</summary>
        public static Transform Beam(Transform parent, string name, Vector3 position, Vector3 size, Material material, float bevel = .012f)
        {
            string key = $"倒角_{size.x:F3}_{size.y:F3}_{size.z:F3}_{bevel:F3}";
            if (!meshes.TryGetValue(key, out var mesh))
            {
                mesh = AtelierMeshLibrary.Save(key, AtelierMeshLibrary.Timber(size, bevel));
                meshes[key] = mesh;
            }
            return Node(parent, name, mesh, material, position);
        }

        /// <summary>放置旋转截面工件，单位保持米制。</summary>
        private static Transform Lathe(Transform parent, string name, Vector2[] profile, Material material, Vector3 position)
        {
            return Node(parent, name, AtelierMeshLibrary.Save(name, AtelierMeshLibrary.Lathe(profile)), material, position);
        }

        /// <summary>在两个三维端点间安装圆木、灯杆或结构连接件。</summary>
        public static Transform Link(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material material)
        {
            float length = Vector3.Distance(from, to);
            var part = Node(parent, name, AtelierMeshLibrary.Save(name, AtelierMeshLibrary.Lathe(new[] { new Vector2(radius, 0), new Vector2(radius, length) }, 12)), material, from);
            part.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
            return part;
        }

        /// <summary>按自然模型包围盒统一缩放，底部接触放置面，保留源轴转换。</summary>
        public static Transform Model(Transform parent, string name, string path, Vector3 foot, float height, Material material, Vector3 euler, float width = 0)
        {
            var asset = Load<GameObject>(path);
            var holder = Group(parent, name);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(euler) * asset.transform.localRotation;
            Bounds bounds = BoundsOf(holder);
            float scale = width > 0 ? width / Mathf.Max(.00001f, bounds.size.x) : height / Mathf.Max(.00001f, bounds.size.y);
            holder.localScale = Vector3.one * scale;
            holder.localPosition = foot - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
            foreach (var renderer in holder.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(x => material).ToArray();
            foreach (var collider in holder.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            return holder;
        }

        /// <summary>把各网格包围盒的八个角转换到模型组局部坐标。</summary>
        private static Bounds BoundsOf(Transform root)
        {
            bool first = true; var result = new Bounds();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds; var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (first) { result = new Bounds(p, Vector3.zero); first = false; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }

        /// <summary>从指定来源目录精确建立 PBR 材质。</summary>
        private static Material Pbr(string name, string folder, string colour, string normal, string rough, string ao, Color tint)
        {
            var material = Lit(name, tint, Find(colour, folder), Find(normal, folder), .22f);
            if (rough != null) Rough(material, Find(rough, folder), Find(ao, folder), rough == "ORM");
            return material;
        }

        /// <summary>写入本工具专属 URP 材质，保留所有旧材质原样。</summary>
        public static Material Lit(string name, Color tint, Texture2D colour, Texture2D normal, float smoothness)
        {
            string path = Art + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", tint); material.SetFloat("_Smoothness", smoothness);
            material.SetTexture("_BaseMap", colour);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", .52f);
            if (normal != null) material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>把 Roughness 转换到 URP 金属光滑度图的 alpha；ORM 的 G 为粗糙度。</summary>
        private static void Rough(Material material, Texture2D rough, Texture2D ao, bool orm = false)
        {
            if (rough == null) return;
            string path = Art + "Textures/" + material.name + "_金属光滑度.png";
            if (!File.Exists(path))
            {
                var temporary = RenderTexture.GetTemporary(rough.width, rough.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Graphics.Blit(rough, temporary);
                var previous = RenderTexture.active; RenderTexture.active = temporary;
                var pixels = new Texture2D(rough.width, rough.height, TextureFormat.RGBA32, false, true);
                pixels.ReadPixels(new Rect(0, 0, rough.width, rough.height), 0, 0);
                var colours = pixels.GetPixels();
                for (int i = 0; i < colours.Length; i++)
                {
                    Color source = colours[i];
                    colours[i] = new Color(orm ? source.b : 0, 0, 0, Mathf.Clamp01(1 - (orm ? source.g : source.r)));
                }
                pixels.SetPixels(colours); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Object.DestroyImmediate(pixels); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.sRGBTexture = false; importer.maxTextureSize = 2048; importer.SaveAndReimport();
            }
            material.SetTexture("_MetallicGlossMap", Load<Texture2D>(path));
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Smoothness", .75f);
            if (ao != null && !orm)
            {
                material.SetTexture("_OcclusionMap", ao); material.EnableKeyword("_OCCLUSIONMAP"); material.SetFloat("_OcclusionStrength", .65f);
            }
        }

        /// <summary>按文件名匹配唯一来源；多个同名贴图以明确目录优先，不选天空图片。</summary>
        private static Texture2D Find(string token, string folder = null)
        {
            if (token == null) return null;
            string[] roots = folder == null ? new[] { Art + "Imports", Existing } : new[] { folder.TrimEnd('/') };
            string path = AssetDatabase.FindAssets("t:Texture2D", roots).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(x => Path.GetFileName(x).Contains(token));
            return path == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>要求资源真实存在，避免缺资源时用代理物偷偷替代。</summary>
        private static T Load<T>(string path) where T : Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value == null) throw new InvalidOperationException("缺少已审核美术资源：" + path);
            return value;
        }

        /// <summary>创建有真实目标方向的聚光灯。</summary>
        private static Light Spot(Transform parent, string name, Vector3 position, Vector3 target, Color colour, float intensity, float range, float angle)
        {
            var light = new GameObject(name, typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(parent); light.transform.position = position;
            light.transform.rotation = Quaternion.LookRotation(target - position);
            light.type = LightType.Spot; light.color = colour; light.intensity = intensity; light.range = range; light.spotAngle = angle;
            light.innerSpotAngle = angle * .63f;
            return light;
        }
    }
}
