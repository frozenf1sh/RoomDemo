// =============================================================================
// 文件：DemoSceneBuilder.cs
// 职责：通过 Unity Editor API 生成中文灰盒场景、Prefab、材料和占位音频。
// 边界：仅显式重建时覆盖生成资产；拒绝覆盖未保存场景，不手工拼接 Unity YAML。
// =============================================================================
using System;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>通过 Unity Editor API 生成中文灰盒场景、Prefab、材料和占位音频。</summary>
    public static class DemoSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Content/Scenes/DemoRoom.unity";
        public const string GreyboxScenePath = "Assets/_Game/Content/Scenes/DemoGreybox.unity";
        private static string assetVariant = "";
        public const string ChineseFontPath = "Assets/ThirdParty/NotoSans/NotoSansCJKsc-Regular.otf";
        private const string Content = "Assets/_Game/Content/";
        private static Material wall, wood, darkWood, leaf, pot, book, cream, sky, hill, accent;
        private static Font font;
        private static readonly Color Ink = new Color(.10f, .15f, .19f);
        private static readonly Color Paper = new Color(.94f, .92f, .85f);

        /// <summary>命令行灰盒重建入口，明确执行生成操作。</summary>
        public static void BuildGreyboxScene()
        {
            // 独立参考场景和资产目录，不能通过重建灰盒覆盖已有美术摆放或 Prefab。
            assetVariant = "Greybox/";
            try { BuildInto(GreyboxScenePath); }
            finally { assetVariant = ""; }
        }
        /// <summary>兼容验证入口，委托独立场景验证器。</summary>
        public static void ValidateScene() => DemoSceneValidator.ValidateScene();

        [MenuItem("游戏/生成灰盒房间")]
        /// <summary>生成基础房间、交互对象、系统、中文 UI 和绑定并保存。</summary>
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("DemoRoom 已存在。请使用 BuildGreyboxScene 生成独立灰盒，或用 CreateMissingTechnicalObjects 补技术对象，避免覆盖美术场景。");
            BuildInto(ScenePath);
        }
        /// <summary>只生成明确的新场景；所有生成资产使用独立变体目录。</summary>
        private static void BuildInto(string outputPath)
        {
            // 生成属于显式资产写操作；先保护运行状态和未保存场景。
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式，再生成场景。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("请先保存打开的场景，再执行生成。");
            PrepareMaterials();
            // 所有对象通过 Editor API 创建，生成器不手工修改场景 YAML。
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = Group("DemoRoom", null);
            var bootstrapRoot = Group("_Bootstrap", root);
            var systems = Group("_Systems", root);
            var environment = Group("Environment", root);
            var interactables = Group("Interactables", root);
            var effects = Group("Effects", root);
            var ui = Group("UI", root);
            Room(environment); Shelf(environment); Table(environment); Chair(environment);
            // 先建立交互实体，再显式绑定全部表现组件。
            var plant = Plant(interactables);
            var fallenBook = Book(interactables);
            var window = Window(interactables);
            var painting = Painting(interactables);
            var paper = PaperObject(interactables);
            var water = Water(effects);
            var papers = Papers(effects);
            var audioRoot = Group("AudioSystem", systems);
            var audio = audioRoot.gameObject.AddComponent<AudioSource>();
            audio.clip = CreateWindClip(); audio.loop = true; audio.playOnAwake = false; audio.volume = .20f; audio.spatialBlend = 0;
            var wind = Group("Wind", effects).gameObject.AddComponent<WindPresenter>();
            wind.Configure(plant.Visual, papers, audio);

            // 观察位置固定，CameraRig 只负责受限旋转。
            var cameraRig = Group("CameraRig", root);
            cameraRig.localPosition = new Vector3(0, 1.65f, -4.5f);
            cameraRig.localRotation = Quaternion.Euler(4f, 0, 0);
            var cameraObject = Group("MainCamera", cameraRig).gameObject;
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 57; camera.nearClipPlane = .05f; camera.farClipPlane = 60;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.43f, .57f, .65f);
            cameraObject.AddComponent<AudioListener>();
            var look = cameraRig.gameObject.AddComponent<FixedLook>();
            var light = Group("Sun", systems).gameObject.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.8f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(45, -28, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.68f, .71f, .74f);
            var fill = Group("RoomFill", systems).gameObject.AddComponent<Light>();
            fill.type = LightType.Point; fill.range = 13; fill.intensity = 3; fill.transform.localPosition = new Vector3(0, 3, -1);

            var notebook = Notebook(ui);
            var debug = Hud(ui, notebook, out var hover);
            var input = Group("InteractionSystem", systems).gameObject.AddComponent<InteractionSystem>();
            input.Configure(camera, hover); look.Configure(notebook);
            var eventSystem = Group("EventSystem", ui).gameObject;
            eventSystem.AddComponent<EventSystem>(); eventSystem.AddComponent<InputSystemUIInputModule>();
            // 保存后重新取得 Prefab 实例上的引用，避免绑定已销毁的临时 UI。
            ui = SavePrefab(ui, "UI", "UI");
            notebook = ui.GetComponentInChildren<NotebookView>(); debug = ui.GetComponentInChildren<DebugView>();
            hover = ui.Find("DebugCanvas/Hover").GetComponent<Text>();
            input.Configure(camera, hover); look.Configure(notebook);
            var endingPanel = ui.Find("DebugCanvas/Ending").gameObject;
            var ending = Group("Ending", effects).gameObject.AddComponent<EndingPresenter>();
            ending.Configure(endingPanel, light, Sound(audioRoot, "Ambience", true, .2f));
            var bootstrap = bootstrapRoot.gameObject.AddComponent<GameBootstrap>();
            // 启动组件只拿显式引用，所有 Prefab 间协作通过统一会话。
            bootstrap.Configure(new WorldPresenter[] { plant, fallenBook, window, wind, painting, paper, water, ending }, notebook, debug, input);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            // 先保存资源，再保存场景，确保序列化绑定指向有效资产。
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, outputPath);
            PresentationArtPass.InstallTechnicalObjects(root);
            Debug.Log("场景生成器：已生成 " + outputPath);
        }

        /// <summary>准备目录、嵌入中文字体和灰盒共享材料。</summary>
        private static void PrepareMaterials()
        {
            Directory.CreateDirectory(Content + "Art/Materials/" + assetVariant);
            Directory.CreateDirectory(Content + "Audio/" + assetVariant);
            // 中文字体随构建嵌入，避免依赖开发者电脑安装的系统字体。
            var importer = AssetImporter.GetAtPath(ChineseFontPath) as TrueTypeFontImporter;
            if (importer == null) throw new FileNotFoundException("缺少项目中文字体：" + ChineseFontPath);
            if (!importer.includeFontData || importer.fontTextureCase != FontTextureCase.Dynamic)
            {
                importer.includeFontData = true;
                importer.fontTextureCase = FontTextureCase.Dynamic;
                importer.SaveAndReimport();
            }
            font = AssetDatabase.LoadAssetAtPath<Font>(ChineseFontPath);
            wall = Mat("Wall", new Color(.79f, .79f, .73f)); wood = Mat("Wood", new Color(.48f, .33f, .22f));
            darkWood = Mat("DarkWood", new Color(.25f, .20f, .16f)); leaf = Mat("Leaves", new Color(.23f, .48f, .26f));
            pot = Mat("Terracotta", new Color(.64f, .30f, .20f)); book = Mat("BookRed", new Color(.72f, .17f, .12f));
            cream = Mat("Paper", new Color(.94f, .89f, .70f)); sky = Mat("FarSky", new Color(.35f, .68f, .82f));
            hill = Mat("FarHill", new Color(.36f, .56f, .35f)); accent = Mat("Marker", new Color(.90f, .64f, .23f));
        }
        /// <summary>复用或创建 URP 材质，设置灰盒色彩与粗糙度。</summary>
        private static Material Mat(string name, Color color)
        {
            string path = Content + "Art/Materials/" + assetVariant + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.color = color; material.SetFloat("_Smoothness", .15f); return material;
        }
        /// <summary>创建子分组并保持局部坐标，避免对象堆在场景根部。</summary>
        private static Transform Group(string name, Transform parent)
        { var result = new GameObject(name).transform; result.SetParent(parent, false); return result; }
        /// <summary>创建基础几何，视觉默认移除 Collider。</summary>
        private static Transform Shape(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            // 纯视觉默认移除 Primitive Collider，Logic 独立承担交互命中。
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }
        /// <summary>创建立方体几何的具体便捷入口。</summary>
        private static Transform Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider = false)
        { return Shape(name, parent, PrimitiveType.Cube, position, scale, material, collider); }
        /// <summary>保存生成资产，再实例化 Prefab 并保留原始局部姿态。</summary>
        private static Transform SavePrefab(Transform instance, string folder, string name)
        {
            Directory.CreateDirectory(Content + "Prefabs/" + assetVariant + folder);
            var asset = PrefabUtility.SaveAsPrefabAsset(instance.gameObject, Content + "Prefabs/" + assetVariant + folder + "/PF_" + name + ".prefab");
            // 临时生成物替换为 Prefab 实例时保留原有局部位置和旋转。
            var parent = instance.parent; var pos = instance.localPosition; var rot = instance.localRotation;
            UnityEngine.Object.DestroyImmediate(instance.gameObject);
            var created = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            created.name = name; created.transform.localPosition = pos; created.transform.localRotation = rot;
            return created.transform;
        }
        /// <summary>创建使用项目中文字体的世界空间标签。</summary>
        private static void Label(Transform parent, string title, Vector3 position)
        {
            var text = Group("Label", parent).gameObject.AddComponent<TextMesh>();
            text.font = font; text.GetComponent<Renderer>().sharedMaterial = font.material;
            text.text = title; text.fontSize = 48; text.characterSize = .036f; text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(.12f, .17f, .15f); text.transform.localPosition = position;
        }
        /// <summary>独立创建 Logic 节点，绑定领域 ID、碰撞体与点击适配器。</summary>
        private static void Entity(Transform parent, ObjectId id, Vector3 center, Vector3 size)
        {
            // 物体身份和 Collider 放在 Logic，更换 Visual 不改变玩法绑定。
            var logic = Group("Logic", parent).gameObject;
            logic.AddComponent<SceneEntity>().Configure(id);
            var collider = logic.AddComponent<BoxCollider>(); collider.center = center; collider.size = size;
            logic.AddComponent<Clickable>();
        }
        /// <summary>创建灰盒房间与地毯；墙面和地板只作为装饰与射线遮挡。</summary>
        private static void Room(Transform parent)
        {
            var r = Group("RoomShell", parent);
            Cube("Floor", r, new Vector3(0, -.1f, 0), new Vector3(8, .2f, 12), wood, true);
            Cube("LeftWall", r, new Vector3(-4, 2, 0), new Vector3(.2f, 4, 9), wall, true);
            Cube("RightWall", r, new Vector3(4, 2, 0), new Vector3(.2f, 4, 9), wall, true);
            Cube("BackLeft", r, new Vector3(-1.9f, 2, 3.9f), new Vector3(4.2f, 4, .2f), wall, true);
            Cube("BackRight", r, new Vector3(3.45f, 2, 3.9f), new Vector3(1.1f, 4, .2f), wall, true);
            Cube("BackBelow", r, new Vector3(1.55f, .50f, 3.9f), new Vector3(2.7f, 1, .2f), wall, true);
            Cube("BackAbove", r, new Vector3(1.55f, 3.6f, 3.9f), new Vector3(2.7f, .8f, .2f), wall, true);
            Cube("Rug", r, new Vector3(0, .011f, -.1f), new Vector3(3.8f, .02f, 3), Mat("Rug", new Color(.30f, .39f, .38f)));
            SavePrefab(r, "Environment", "RoomShell");
        }
        /// <summary>创建书架与不可交互装饰书，并添加中文标签。</summary>
        private static void Shelf(Transform parent)
        {
            var shelf = Group("BookShelf", parent); shelf.localPosition = new Vector3(-2.05f, 0, 2.7f);
            Cube("Left", shelf, new Vector3(-.9f, .95f, 0), new Vector3(.12f, 1.9f, .55f), darkWood, true);
            Cube("Right", shelf, new Vector3(.9f, .95f, 0), new Vector3(.12f, 1.9f, .55f), darkWood, true);
            for (int i = 0; i < 3; i++) Cube("Shelf" + i, shelf, new Vector3(0, .25f + i * .62f, 0), new Vector3(1.85f, .10f, .55f), wood, true);
            for (int i = 0; i < 5; i++) Cube("DecorativeBook" + i, shelf, new Vector3(-.65f + i * .21f, .74f, -.05f), new Vector3(.13f, .4f, .30f), cream);
            Label(shelf, "书架", new Vector3(0, 2.05f, -.34f));
            SavePrefab(shelf, "Environment", "BookShelf");
        }
        /// <summary>创建桌面和桌腿，不添加游戏逻辑。</summary>
        private static void Table(Transform parent)
        {
            var table = Group("Table", parent); table.localPosition = new Vector3(2.35f, 0, .2f);
            Cube("Top", table, new Vector3(0, .78f, 0), new Vector3(1.7f, .10f, 1.0f), wood, true);
            for (int i = 0; i < 4; i++) Cube("Leg" + i, table, new Vector3((i % 2 == 0 ? -.65f : .65f), .37f, (i < 2 ? -.35f : .35f)), new Vector3(.10f, .74f, .10f), darkWood);
            SavePrefab(table, "Environment", "Table");
        }
        /// <summary>创建背景椅子，不添加交互组件。</summary>
        private static void Chair(Transform parent)
        {
            var chair = Group("Chair", parent); chair.localPosition = new Vector3(3, 0, -1.0f);
            Cube("Seat", chair, new Vector3(0, .44f, 0), new Vector3(.65f, .10f, .65f), wood);
            Cube("Back", chair, new Vector3(0, .80f, -.29f), new Vector3(.65f, .68f, .10f), darkWood);
            for (int i = 0; i < 4; i++) Cube("Leg" + i, chair, new Vector3(i % 2 == 0 ? -.24f : .24f, .22f, i < 2 ? -.24f : .24f), new Vector3(.08f, .44f, .08f), darkWood);
            SavePrefab(chair, "Environment", "Chair");
        }
        /// <summary>保留独立装饰画架生成入口，当前核心画自带支架。</summary>
        private static void Easel(Transform parent)
        {
            var easel = Group("Easel", parent); easel.localPosition = new Vector3(-3.3f, 0, .0f);
            Cube("Canvas", easel, new Vector3(0, 1.25f, 0), new Vector3(.65f, .8f, .07f), cream);
            Cube("Leg", easel, new Vector3(0, .6f, .1f), new Vector3(.08f, 1.2f, .08f), wood);
            SavePrefab(easel, "Environment", "Easel");
        }
        /// <summary>创建盆栽、伸向书架的枝叶及可切换的水生视觉。</summary>
        private static PlantPresenter Plant(Transform parent)
        {
            var plant = Group("Plant", parent); plant.localPosition = new Vector3(-.65f, 0, 2.1f);
            Entity(plant, DemoRules.Plant, new Vector3(0, 1.1f, 0), new Vector3(.95f, 1.8f, .9f));
            var visual = Group("Visual", plant);
            Cube("Stand", visual, new Vector3(0, .3f, 0), new Vector3(.60f, .6f, .60f), darkWood);
            Shape("Pot", visual, PrimitiveType.Cylinder, new Vector3(0, .72f, 0), new Vector3(.55f, .18f, .55f), pot);
            var foliage = Group("Foliage", visual); foliage.localPosition = new Vector3(0, .85f, 0);
            Shape("Stem", foliage, PrimitiveType.Cylinder, new Vector3(0, .24f, 0), new Vector3(.07f, .30f, .07f), leaf);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7;
                var l = Shape("Leaf" + i, foliage, PrimitiveType.Sphere, new Vector3(Mathf.Sin(a) * .17f, .22f + i * .065f, Mathf.Cos(a) * .17f), new Vector3(.30f, .12f, .47f), leaf);
                l.localRotation = Quaternion.Euler(20, i * 51, -20);
            }
            Label(visual, "盆栽", new Vector3(0, .43f, -.34f));
            var branch = Shape("ReachingBranch", foliage, PrimitiveType.Sphere, new Vector3(-.34f, .56f, 0), new Vector3(.8f, .09f, .17f), leaf);
            branch.localRotation = Quaternion.Euler(0, 0, -24);
            // 水生形态作为额外视觉子层，不要求真实植物变形或规则重写。
            var aquatic = Group("AquaticVisual", foliage);
            for (int i = 0; i < 6; i++)
            {
                var kelp = Shape("Kelp" + i, aquatic, PrimitiveType.Capsule, new Vector3((i - 2.5f) * .10f, .6f + i * .025f, -.15f), new Vector3(.055f, .48f, .07f), Mat("AquaticKelp", new Color(.06f, .65f, .62f)));
                kelp.localRotation = Quaternion.Euler(0, 0, (i - 2.5f) * -10);
            }
            aquatic.gameObject.SetActive(false);
            plant.gameObject.AddComponent<PlantPresenter>().Configure(foliage, aquatic.gameObject, Sound(plant, "Grow", false, .25f));
            return SavePrefab(plant, "Interactables", "Plant").GetComponent<PlantPresenter>();
        }
        /// <summary>创建可见但初始不可调查的红书，并配置落地表现。</summary>
        private static BookPresenter Book(Transform parent)
        {
            var b = Group("Book", parent); b.localPosition = new Vector3(-1.37f, 1.66f, 2.53f);
            b.localRotation = Quaternion.Euler(0, 0, 75);
            Entity(b, DemoRules.Book, Vector3.zero, new Vector3(.62f, .19f, .42f));
            var visual = Group("Visual", b);
            Cube("Pages", visual, Vector3.zero, new Vector3(.55f, .11f, .36f), cream);
            Cube("CoverTop", visual, new Vector3(0, .065f, 0), new Vector3(.60f, .025f, .40f), book);
            Cube("CoverBottom", visual, new Vector3(0, -.065f, 0), new Vector3(.60f, .025f, .40f), book);
            Cube("Spine", visual, new Vector3(-.285f, 0, 0), new Vector3(.03f, .14f, .40f), book);
            b.gameObject.AddComponent<BookPresenter>().Configure(new Vector3(-1.65f, .12f, .65f), Sound(b, "Fall", false, .4f));
            return SavePrefab(b, "Interactables", "Book").GetComponent<BookPresenter>();
        }
        /// <summary>创建窗框、近远景与海景，绑定三层表现。</summary>
        private static WindowPresenter Window(Transform parent)
        {
            var window = Group("Window", parent); window.localPosition = new Vector3(1.55f, 2.1f, 3.65f);
            Entity(window, DemoRules.Window, Vector3.zero, new Vector3(2.65f, 2.2f, .16f));
            var visual = Group("Visual", window);
            Cube("FrameLeft", visual, new Vector3(-1.34f, 0, 0), new Vector3(.13f, 2.3f, .20f), cream);
            Cube("FrameRight", visual, new Vector3(1.34f, 0, 0), new Vector3(.13f, 2.3f, .20f), cream);
            Cube("FrameTop", visual, new Vector3(0, 1.10f, 0), new Vector3(2.8f, .13f, .20f), cream);
            Cube("FrameBottom", visual, new Vector3(0, -1.10f, 0), new Vector3(2.8f, .13f, .20f), cream);
            Cube("Crossbar", visual, Vector3.zero, new Vector3(.065f, 2.2f, .17f), cream);
            var near = Group("NearBackground", visual);
            Cube("QuietSky", near, new Vector3(0, 0, .25f), new Vector3(2.65f, 2.16f, .04f), Mat("NearSky", new Color(.55f, .59f, .59f)));
            var far = Group("FarBackground", visual);
            Cube("Sky", far, new Vector3(0, 0, .27f), new Vector3(2.65f, 2.16f, .04f), sky);
            Shape("Sun", far, PrimitiveType.Sphere, new Vector3(.7f, .60f, .21f), new Vector3(.30f, .30f, .03f), cream);
            Shape("DistantMountain", far, PrimitiveType.Sphere, new Vector3(-.52f, -.64f, .21f), new Vector3(1.65f, .86f, .05f), Mat("Mountain", new Color(.31f, .46f, .54f)));
            Shape("Meadow", far, PrimitiveType.Sphere, new Vector3(.32f, -.98f, .17f), new Vector3(2.2f, .68f, .05f), hill);
            Label(visual, "窗户", new Vector3(0, -1.32f, -.12f));
            var farAsset = SavePrefab(far, "Effects", "WindowFarBackground");
            farAsset.gameObject.SetActive(false);
            window.gameObject.AddComponent<WindowPresenter>().Configure(near.gameObject, farAsset.gameObject, Ocean(visual).gameObject);
            return SavePrefab(window, "Interactables", "Window").GetComponent<WindowPresenter>();
        }
        /// <summary>复用原创海面 Shader 材质，不引入流体模拟。</summary>
        private static Material SeaMaterial()
        {
            string path = Content + "Art/Materials/" + assetVariant + "SeaSurface.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("RoomDemo/SeaSurface")); AssetDatabase.CreateAsset(material, path); }
            return material;
        }
        /// <summary>灰盒使用自己的连续表面材料，不覆盖美术材质库。</summary>
        private static Material PaintingSurfaceMaterial(string name, float canvasMode)
        {
            var material = Mat(name, Color.white); material.shader = Shader.Find("RoomDemo/PaintingSea");
            material.SetFloat("_CanvasMode", canvasMode); material.SetFloat("_Transition", 0); return material;
        }
        /// <summary>创建天空、海面、地平线与远月组成的海景层。</summary>
        private static Transform Ocean(Transform parent)
        {
            var ocean = Group("OceanBackground", parent);
            Cube("OceanSky", ocean, new Vector3(0, 0, .26f), new Vector3(2.65f, 2.16f, .04f), PaintingSurfaceMaterial("WindowCanvas", 0));
            Cube("Sea", ocean, new Vector3(0, -.63f, .19f), new Vector3(2.65f, .93f, .04f), SeaMaterial());
            Cube("Horizon", ocean, new Vector3(0, -.16f, .16f), new Vector3(2.65f, .025f, .03f), cream);
            Shape("DistantMoon", ocean, PrimitiveType.Sphere, new Vector3(-.66f, .53f, .19f), new Vector3(.36f, .36f, .03f), cream);
            foreach (Transform child in ocean) if (child.name != "OceanSky") child.gameObject.SetActive(false);
            return ocean;
        }
        /// <summary>创建画框、普通静态画及动态海画视觉层。</summary>
        private static PaintingPresenter Painting(Transform parent)
        {
            var painting = Group("Painting", parent); painting.localPosition = new Vector3(2.72f, 1.85f, 1.3f);
            Entity(painting, DemoRules.Painting, Vector3.zero, new Vector3(1.18f, 1.44f, .18f));
            var visual = Group("Visual", painting);
            Cube("FrameLeft", visual, new Vector3(-.58f, 0, 0), new Vector3(.1f, 1.44f, .18f), darkWood);
            Cube("FrameRight", visual, new Vector3(.58f, 0, 0), new Vector3(.1f, 1.44f, .18f), darkWood);
            Cube("FrameTop", visual, new Vector3(0, .67f, 0), new Vector3(1.26f, .1f, .18f), wood);
            Cube("FrameBottom", visual, new Vector3(0, -.67f, 0), new Vector3(1.26f, .1f, .18f), wood);
            Cube("EaselLeg", visual, new Vector3(0, -1.2f, .1f), new Vector3(.12f, 1.2f, .12f), wood);
            var ordinary = Group("OrdinaryPainting", visual);
            Cube("DryCanvas", ordinary, new Vector3(0, 0, .02f), new Vector3(1.10f, 1.27f, .06f), cream);
            Cube("PigmentBand", ordinary, new Vector3(0, -.14f, -.025f), new Vector3(1.1f, .21f, .02f), pot);
            Shape("StillSun", ordinary, PrimitiveType.Sphere, new Vector3(-.22f, .23f, -.025f), new Vector3(.34f, .34f, .015f), accent);
            var sea = Group("SeaPainting", visual);
            Cube("MovingSea", sea, new Vector3(0, 0, .02f), new Vector3(1.10f, 1.27f, .06f), PaintingSurfaceMaterial("PaintingCanvas", 1)); sea.gameObject.SetActive(false);
            Label(visual, "画", new Vector3(0, -.85f, -.14f));
            painting.gameObject.AddComponent<PaintingPresenter>().Configure(ordinary.gameObject, sea.gameObject, Sound(painting, "Sea", true, .23f));
            return SavePrefab(painting, "Interactables", "Painting").GetComponent<PaintingPresenter>();
        }
        /// <summary>创建藏在画框后的核心纸张，配置显露位置。</summary>
        private static PaperPresenter PaperObject(Transform parent)
        {
            var paper = Group("Paper", parent); paper.localPosition = new Vector3(2.72f, 1.78f, 1.43f);
            Entity(paper, DemoRules.Paper, Vector3.zero, new Vector3(.60f, .05f, .75f));
            var visual = Group("Visual", paper);
            Cube("Sheet", visual, Vector3.zero, new Vector3(.60f, .014f, .75f), cream);
            for (int i = 0; i < 4; i++) Cube("InkWave" + i, visual, new Vector3(0, .012f, -.2f + i * .12f), new Vector3(.40f, .006f, .025f), sky);
            var label = Group("PaperLabel", visual); label.localRotation = Quaternion.Euler(90, 0, 0); Label(label, "海？", new Vector3(0, 0, -.12f));
            paper.gameObject.AddComponent<PaperPresenter>().Configure(paper, new Vector3(1.20f, .13f, -.10f), new Vector3(-15, 0, -8));
            return SavePrefab(paper, "Interactables", "Paper").GetComponent<PaperPresenter>();
        }
        /// <summary>创建画框滴水和局部地面水渍表现。</summary>
        private static WaterPresenter Water(Transform parent)
        {
            var water = Group("Water", parent); water.localPosition = new Vector3(2.72f, 0, 1.30f);
            var puddle = Group("Puddle", water);
            Shape("WaterStain", puddle, PrimitiveType.Cylinder, new Vector3(0, .018f, -.1f), new Vector3(1.15f, .008f, .75f), SeaMaterial());
            var drops = new Transform[5];
            for (int i = 0; i < drops.Length; i++) drops[i] = Shape("Drop" + i, water, PrimitiveType.Sphere, new Vector3(-.43f + i * .21f, 1.22f, -.14f), new Vector3(.035f, .09f, .035f), Mat("WaterDrop", new Color(.35f, .76f, .83f)));
            water.gameObject.AddComponent<WaterPresenter>().Configure(puddle.gameObject, drops, Sound(water, "Water", true, .13f));
            return SavePrefab(water, "Effects", "Water").GetComponent<WaterPresenter>();
        }
        /// <summary>创建被风吹动的纯装饰纸张，与可调查纸张分开。</summary>
        private static Transform[] Papers(Transform parent)
        {
            var r = Group("WindPaper", parent); r.localPosition = new Vector3(2.35f, .86f, .2f);
            for (int i = 0; i < 3; i++)
            { var p = Cube("Paper" + i, r, new Vector3(-.45f + i * .4f, i * .008f, .05f * i), new Vector3(.32f, .008f, .45f), cream); p.localRotation = Quaternion.Euler(0, i * 18, 0); }
            var saved = SavePrefab(r, "Effects", "WindPaper");
            return Enumerable.Range(0, 3).Select(i => saved.Find("Paper" + i)).ToArray();
        }
        /// <summary>用固定种子噪声和简单音调生成确定性的占位 WAV。</summary>
        private static AudioSource Sound(Transform parent, string kind, bool loop, float volume)
        {
            string path = Content + "Audio/" + assetVariant + kind + ".wav";
            // 写标准单声道 16 位 PCM WAV；声音只是可替换占位反馈。
            const int rate = 22050;
            int count = rate * (loop ? 6 : 1); var random = new System.Random(31); float smooth = 0;
            using (var stream = new BinaryWriter(File.Create(path)))
            {
                // WAV 格式头必须保留协议中的英文 ASCII 字节。
                stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); stream.Write(36 + count * 2);
                stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); stream.Write(16); stream.Write((short)1); stream.Write((short)1);
                stream.Write(rate); stream.Write(rate * 2); stream.Write((short)2); stream.Write((short)16);
                stream.Write(System.Text.Encoding.ASCII.GetBytes("data")); stream.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / rate, phase = (float)i / count;
                    smooth += ((float)random.NextDouble() * 2 - 1 - smooth) * .16f;
                    // 短音衰减、循环音端点衰减，减轻重复播放时的突变。
                    float envelope = loop ? Mathf.Sin(phase * Mathf.PI) : Mathf.Exp(-t * (kind == "Fall" ? 14 : 4));
                    float signal = kind == "Fall" ? smooth * .6f + Mathf.Sin(t * 240) * .4f : kind == "Grow" ? Mathf.Sin(t * (440 + t * 150)) * .15f + smooth * .3f :
                        kind == "Ambience" ? Mathf.Sin(t * Mathf.PI * 2 * 110) * .15f + Mathf.Sin(t * Mathf.PI * 2 * 164.81f) * .1f :
                        kind == "Water" ? Mathf.Sin(t * 1800 + Mathf.Sin(t * 23) * 30) * .12f + smooth * .25f : smooth * (.4f + Mathf.Sin(t * 1.5f) * .25f);
                    stream.Write((short)(signal * envelope * 23000));
                }
            }
            AssetDatabase.ImportAsset(path);
            var audio = Group(kind + "Audio", parent).gameObject.AddComponent<AudioSource>(); audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            audio.loop = loop; audio.playOnAwake = false; audio.volume = volume; audio.spatialBlend = 0; return audio;
        }
        /// <summary>生成原创低通噪声风声，不依赖外部素材。</summary>
        private static AudioClip CreateWindClip()
        {
            // 固定种子的低通噪声构成原创循环风声，不使用外部音频。
            string path = Content + "Audio/" + assetVariant + "Wind.wav";
            const int rate = 22050, count = rate * 4;
            var random = new System.Random(17); float sample = 0;
            using (var stream = new BinaryWriter(File.Create(path)))
            {
                // WAV 格式头必须保留协议中的英文 ASCII 字节。
                stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); stream.Write(36 + count * 2);
                stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); stream.Write(16); stream.Write((short)1); stream.Write((short)1);
                stream.Write(rate); stream.Write(rate * 2); stream.Write((short)2); stream.Write((short)16);
                stream.Write(System.Text.Encoding.ASCII.GetBytes("data")); stream.Write(count * 2);
                for (int i = 0; i < count; i++)
                { sample += ((float)random.NextDouble() * 2 - 1 - sample) * .07f; float envelope = Mathf.Sin(Mathf.PI * i / (count - 1)); stream.Write((short)(sample * envelope * 21000)); }
            }
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        /// <summary>创建使用锚点和偏移的 UI 矩形。</summary>
        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = offsetMin; r.offsetMax = offsetMax; return r;
        }
        /// <summary>在 UI 矩形上添加背景图。</summary>
        private static RectTransform Box(string name, Transform parent, Vector2 min, Vector2 max, Vector2 a, Vector2 b, Color color)
        { var r = Rect(name, parent, min, max, a, b); var image = r.gameObject.AddComponent<Image>(); image.color = color; return r; }
        /// <summary>创建使用统一中文字体的面板文本。</summary>
        private static Text Text(string name, Transform parent, string value, int size, Color color, Vector2 min, Vector2 max, Vector2 a, Vector2 b)
        {
            var r = Rect(name, parent, min, max, a, b); var t = r.gameObject.AddComponent<Text>();
            // 全部面板统一使用可嵌入中文字体，文本不阻挡 UI 射线。
            t.font = font; t.fontSize = size; t.color = color; t.text = value; t.raycastTarget = false;
            t.alignment = TextAnchor.MiddleLeft; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; return t;
        }
        /// <summary>创建中文按钮及其颜色反馈，文本不阻挡射线。</summary>
        private static Button Button(string name, Transform parent, string label, Vector2 min, Vector2 max, Vector2 a, Vector2 b)
        {
            var r = Box(name, parent, min, max, a, b, new Color(.23f, .37f, .34f));
            var button = r.gameObject.AddComponent<Button>();
            var colors = button.colors; colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f); colors.pressedColor = new Color(.7f, .85f, .7f); button.colors = colors;
            var t = Text("Text", r, label, 19, Color.white, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0));
            t.alignment = TextAnchor.MiddleCenter; return button;
        }
        /// <summary>创建随屏幕缩放的叠加画布与 UI 射线组件。</summary>
        private static Transform Canvas(string name, Transform parent, int order)
        {
            var r = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var canvas = r.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
            var scaler = r.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // 统一参考分辨率，保证不同窗口下的锚点布局一致。
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            r.gameObject.AddComponent<GraphicRaycaster>(); return r;
        }
        /// <summary>创建中文笔记本、分类列表、组合槽与解释历史。</summary>
        private static NotebookView Notebook(Transform parent)
        {
            var canvas = Canvas("NotebookCanvas", parent, 10);
            var notebook = canvas.gameObject.AddComponent<NotebookView>();
            var shade = Box("NotebookPanel", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.06f, .09f, .11f, .8f));
            var body = Box("Notebook", shade, new Vector2(.15f, .16f), new Vector2(.85f, .88f), Vector2.zero, Vector2.zero, Paper);
            Text("Title", body, "笔记本 / 收集到的意义", 26, Ink, new Vector2(0, 1), Vector2.one, new Vector2(30, -66), new Vector2(-80, -16));
            Button("CloseNotebook", body, "关闭", Vector2.one, Vector2.one, new Vector2(-90, -54), new Vector2(-18, -16));
            Text("ObjectsHeading", body, "物体", 17, Ink, new Vector2(0, .85f), new Vector2(.5f, .85f), new Vector2(30, -30), new Vector2(-15, 0));
            Text("ConceptsHeading", body, "概念", 17, Ink, new Vector2(.5f, .85f), new Vector2(1, .85f), new Vector2(15, -30), new Vector2(-30, 0));
            var objects = Rect("Objects", body, new Vector2(0, .44f), new Vector2(.5f, .76f), new Vector2(30, 0), new Vector2(-15, 0));
            var concepts = Rect("Concepts", body, new Vector2(.5f, .44f), new Vector2(1, .76f), new Vector2(15, 0), new Vector2(-30, 0));
            foreach (var list in new[] { objects, concepts })
            { var layout = list.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false; }
            var template = Button("RowTemplate", canvas, "碎片", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(200, 42));
            template.gameObject.AddComponent<LayoutElement>().preferredHeight = 42; template.gameObject.SetActive(false);
            var selection = Text("Selection", body, "组合", 16, Ink, new Vector2(0, .36f), new Vector2(1, .42f), new Vector2(30, 0), new Vector2(-30, 0));
            var interpret = Button("Interpret", body, "建立解释", new Vector2(1, .19f), new Vector2(1, .29f), new Vector2(-210, 0), new Vector2(-30, 0));
            var feedback = Text("Feedback", body, "选择已收集的碎片。", 14, Ink, Vector2.zero, new Vector2(1, .11f), new Vector2(30, 15), new Vector2(-30, 0));
            // 固定三个输入槽足以支持二元与三元配方，不引入自由节点编辑器。
            var slots = new Button[3];
            for (int i = 0; i < 3; i++) slots[i] = Button("Slot" + i, body, "+ 选择碎片", new Vector2(i / 3f, .28f), new Vector2((i + 1) / 3f, .36f), new Vector2(30, 0), new Vector2(-15, 0));
            // 中文字体行高较大，为标题和三个已建立解释预留完整的四行空间。
            var history = Text("CompletedRecipes", body, "", 14, Ink, new Vector2(0, .11f), new Vector2(.65f, .28f), new Vector2(30, 0), new Vector2(0, 0));
            history.alignment = TextAnchor.UpperLeft;
            notebook.ConfigureSlots(slots, history);
            notebook.Configure(shade.gameObject, objects, concepts, template, interpret, selection, feedback);
            shade.gameObject.SetActive(false); return notebook;
        }
        /// <summary>创建中文引导、操作说明、调试工具与临时结束提示。</summary>
        private static DebugView Hud(Transform parent, NotebookView notebook, out Text hover)
        {
            var canvas = Canvas("DebugCanvas", parent, 0);
            var header = Box("Header", canvas, new Vector2(0, 1), Vector2.one, new Vector2(0, -90), Vector2.zero, new Color(.07f, .12f, .14f, .92f));
            Text("Title", header, "房间 / 意义构成的世界", 22, Paper, Vector2.zero, Vector2.one, new Vector2(25, 40), new Vector2(-200, 0));
            var objective = Text("Objective", header, "观察盆栽", 17, new Color(.91f, .72f, .36f), Vector2.zero, Vector2.one, new Vector2(25, 0), new Vector2(-345, -40));
            var open = Button("OpenNotebook", header, "笔记本 [N]", new Vector2(1, 0), Vector2.one, new Vector2(-195, 21), new Vector2(-20, -21));
            var restart = Button("Restart", header, "重新开始", new Vector2(1, 0), Vector2.one, new Vector2(-335, 21), new Vector2(-210, -21));
            notebook.ConfigureControls(open, notebook.transform.Find("NotebookPanel/Notebook/CloseNotebook").GetComponent<Button>());
            // 醒目获取通知独立于长观察文案；不遮挡点击，也不自动打开笔记本。
            var notice = Box("DiscoveryNotice", canvas, new Vector2(.30f, .72f), new Vector2(.70f, .87f), Vector2.zero, Vector2.zero, new Color(.10f, .16f, .18f, .97f));
            notice.GetComponent<Image>().raycastTarget = false;
            var noticeText = Text("DiscoveryNoticeText", notice, "", 20, new Color(1f, .84f, .40f), Vector2.zero, Vector2.one, new Vector2(12, 8), new Vector2(-12, -8));
            noticeText.alignment = TextAnchor.MiddleCenter;
            notebook.ConfigureDiscoveryFeedback(notice.gameObject, noticeText);
            notice.gameObject.SetActive(false);
            var footer = Box("Footer", canvas, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 44), new Color(.07f, .12f, .14f, .90f));
            Text("Controls", footer, "左键：观察  右键拖动：转头  N / Tab：笔记本  Home：复位视角  H：提示  F3：调试", 14, Paper, Vector2.zero, Vector2.one, new Vector2(25, 0), new Vector2(-10, 0));
            hover = Text("Hover", canvas, "", 18, Paper, new Vector2(0, 0), new Vector2(1, 0), new Vector2(30, 58), new Vector2(-30, 92));
            hover.alignment = TextAnchor.MiddleCenter;
            // 左侧观察文字与中央结束面板保持水平间隔，避免中文长描述相互遮挡。
            var observation = Text("Observation", canvas, "", 17, Paper, new Vector2(0, .09f), new Vector2(.28f, .28f), new Vector2(26, 0), new Vector2(-15, 0));
            observation.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .8f); observation.alignment = TextAnchor.LowerLeft;
            var panel = Box("DebugPanel", canvas, new Vector2(1, .07f), new Vector2(1, .90f), new Vector2(-410, 0), new Vector2(-15, 0), new Color(.04f, .09f, .12f, .97f));
            Text("DebugTitle", panel, "领域调试 / 状态区可滚动", 16, Paper, new Vector2(0, .94f), Vector2.one, new Vector2(15, 0), new Vector2(-15, 0));
            // 调试状态较长，使用裁剪与滚动容器避免覆盖操作按钮。
            var viewport = Box("StateViewport", panel, new Vector2(0, .34f), new Vector2(1, .93f), new Vector2(15, 0), new Vector2(-15, 0), new Color(0, 0, 0, 0));
            viewport.gameObject.AddComponent<RectMask2D>();
            var state = Text("State", viewport, "", 13, Paper, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero); state.alignment = TextAnchor.UpperLeft;
            state.rectTransform.pivot = new Vector2(.5f, 1);
            state.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = state.rectTransform; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var events = Text("Events", panel, "", 10, new Color(.68f, .81f, .77f), new Vector2(0, .22f), new Vector2(1, .33f), new Vector2(15, 0), new Vector2(-15, 0)); events.alignment = TextAnchor.UpperLeft;
            var reset = Button("DebugReset", panel, "重置", new Vector2(0, .16f), new Vector2(.30f, .21f), new Vector2(15, 0), new Vector2(-4, 0));
            var grant = Button("GrantAll", panel, "获取全部碎片", new Vector2(.30f, .16f), new Vector2(1, .21f), new Vector2(4, 0), new Vector2(-15, 0));
            var factRect = Box("FactInput", panel, new Vector2(0, .10f), new Vector2(.69f, .15f), new Vector2(15, 0), new Vector2(-4, 0), Paper);
            var factInput = factRect.gameObject.AddComponent<InputField>();
            var inputText = Text("Text", factRect, "", 14, Ink, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0));
            // 输入框保留稳定事实 ID 示例，中文文案与机器键分开。
            factInput.textComponent = inputText; factInput.text = "painting.sea"; factInput.characterLimit = 70;
            var add = Button("AddFact", panel, "添加事实", new Vector2(.69f, .10f), new Vector2(1, .15f), new Vector2(4, 0), new Vector2(-15, 0));
            var presetButtons = new Button[4];
            for (int i = 0; i < presetButtons.Length; i++)
            {
                int row = i / 2, column = i % 2;
                presetButtons[i] = Button("Preset" + i, panel, DebugPresets.All[i].DisplayName, new Vector2(column * .5f, .045f - row * .042f), new Vector2((column + 1) * .5f, .085f - row * .042f), new Vector2(15, 0), new Vector2(-6, 0));
                presetButtons[i].GetComponentInChildren<Text>().fontSize = 14;
            }
            var ending = Box("Ending", canvas, new Vector2(.30f, .09f), new Vector2(.70f, .22f), Vector2.zero, Vector2.zero, new Color(.05f, .14f, .18f, .9f));
            ending.GetComponent<Image>().raycastTarget = false;
            var endTitle = Text("EndingTitle", ending, "房间记得。", 29, Paper, new Vector2(0, .35f), Vector2.one, new Vector2(15, 0), new Vector2(-15, 0)); endTitle.alignment = TextAnchor.MiddleCenter;
            var endDetail = Text("EndingDetail", ending, "先前留下的笔迹，还在继续。", 14, new Color(.68f, .87f, .89f), Vector2.zero, new Vector2(1, .4f), new Vector2(15, 0), new Vector2(-15, 0)); endDetail.alignment = TextAnchor.MiddleCenter;
            ending.gameObject.SetActive(false);
            var debug = canvas.gameObject.AddComponent<DebugView>(); debug.Configure(panel.gameObject, state, events, objective);
            debug.ConfigureTools(observation, restart, reset, grant, add, factInput, presetButtons);
            panel.gameObject.SetActive(false); return debug;
        }
    }
}
