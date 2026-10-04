// =============================================================================
// 文件：ExplorationSceneSetup.cs
// 职责：增量安装第四阶段检视、观察 UI 与技术资产；不重建已有美术场景。
// 边界：所有场景与 Prefab 操作通过 Editor API，先保护未保存人工修改。
// =============================================================================
using System.IO;
using Game.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Editor
{
    /// <summary>按可编译阶段安装探索样板；公开入口可以从连接式 CLI 执行。</summary>
    public static class ExplorationSceneSetup
    {
        private const string Prefabs = "Assets/_Game/Content/Prefabs/Inspectables/";
        /// <summary>先安装技术立方体，验证同一输入控制，再制作近景主物体。</summary>
        public static void InstallInspectionCore()
        {
            var root = PresentationArtPass.Room();
            Directory.CreateDirectory(Prefabs); Directory.CreateDirectory("Assets/_Game/Content/Art/Materials/Inspection"); AssetDatabase.Refresh();
            var cube = CubePrefab();
            var space = PresentationArtPass.Ensure(root.Find("_Systems"), "InspectionSpace"); space.localPosition = new Vector3(0, -100, 0);
            var pivot = PresentationArtPass.Ensure(space, "Pivot");
            var lamp = PresentationArtPass.Ensure(space, "InspectionLight");
            var light = lamp.GetComponent<Light>(); if (light == null) light = lamp.gameObject.AddComponent<Light>();
            lamp.localPosition = new Vector3(-1.5f, 2, -2); light.type = LightType.Point; light.intensity = 4; light.range = 9; light.color = new Color(1, .92f, .78f); light.cullingMask = 1 << InspectionController.InspectionLayer;
            var ui = PresentationArtPass.Ensure(root.Find("UI"), "InspectionCanvas");
            var canvas = ui.GetComponent<Canvas>(); if (canvas == null) canvas = ui.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 15;
            // 添加 Canvas 会把 Transform 替换为 RectTransform，必须重新取得引用。
            ui = canvas.transform;
            var scaler = ui.GetComponent<CanvasScaler>(); if (scaler == null) scaler = ui.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1440, 900);
            if (ui.GetComponent<GraphicRaycaster>() == null) ui.gameObject.AddComponent<GraphicRaycaster>();
            var panel = Rect("InspectionOverlay", ui, Vector2.zero, Vector2.one);
            var title = Label("Title", panel, "仔细看", new Vector2(.05f, .88f), new Vector2(.75f, .96f), 27);
            var hint = Label("Hint", panel, "旋转物体，寻找细节。", new Vector2(.08f, .10f), new Vector2(.92f, .18f), 20); hint.alignment = TextAnchor.MiddleCenter;
            var feedback = Label("Feedback", panel, "", new Vector2(.16f, .21f), new Vector2(.84f, .29f), 25); feedback.alignment = TextAnchor.MiddleCenter;
            var back = Button("ExitInspection", panel, "返回房间 [Esc]", new Vector2(.78f, .88f), new Vector2(.95f, .95f));
            var controller = space.GetComponent<InspectionController>(); if (controller == null) controller = space.gameObject.AddComponent<InspectionController>();
            controller.Configure(root.GetComponentInChildren<Camera>(true), pivot, panel.gameObject, title, hint, feedback, back, cube,
                AudioArtPass.Source(space, "Pickup", false, .24f, false), AudioArtPass.Source(space, "Paper", false, .26f, false), AudioArtPass.Source(space, "Ink", false, .30f, false), AudioArtPass.Source(space, "ExitInspection", false, .22f, false));
            root.GetComponentInChildren<GameBootstrap>(true).ConfigureInspection(controller);
            root.GetComponentInChildren<InteractionSystem>(true).ConfigureInspection(controller);
            panel.gameObject.SetActive(false); PresentationArtPass.Save(root);
        }
        /// <summary>安装盆栽近景复制体：新芽在背侧，旋转后才有有效观察角度。</summary>
        public static void InstallPlantInspection()
        {
            var root = PresentationArtPass.Room();
            var node = new GameObject("PF_InspectPlant");
            var visual = PresentationArtPass.Ensure(node.transform, "Plant");
            var pot = Material("Pot", new Color(.42f, .24f, .14f)); var leaf = Material("Leaf", new Color(.28f, .43f, .20f));
            Primitive("Pot", visual, PrimitiveType.Cylinder, new Vector3(0, -.48f, 0), new Vector3(.90f, .27f, .9f), pot);
            Primitive("Soil", visual, PrimitiveType.Cylinder, new Vector3(0, -.20f, 0), new Vector3(.80f, .015f, .8f), Material("Soil", new Color(.14f, .10f, .06f)));
            Primitive("Stem", visual, PrimitiveType.Cylinder, new Vector3(0, .20f, 0), new Vector3(.055f, .40f, .055f), leaf);
            for (int i = 0; i < 6; i++)
            {
                var blade = Primitive("Leaf" + i, visual, PrimitiveType.Sphere, new Vector3((i % 2 == 0 ? 1 : -1) * .20f, i * .15f, .02f), new Vector3(.55f, .065f, .25f), leaf);
                blade.transform.localRotation = Quaternion.Euler(0, i * 25, (i % 2 == 0 ? 1 : -1) * 18);
            }
            var sprout = Hotspot("Sprout", visual, "sprout", new Vector3(.25f, -.12f, .34f), new Vector3(.17f, .23f, .08f), Material("Sprout", new Color(.47f, .68f, .26f)), Vector3.forward);
            Primitive("NewLeaf", visual, PrimitiveType.Sphere, new Vector3(.29f, .015f, .34f), new Vector3(.16f, .07f, .08f), leaf);
            var turning = new GameObject("NoTurningPage"); turning.transform.SetParent(node.transform, false);
            node.AddComponent<InspectionView>().Configure(new[] { "rest" }, new[] { visual.gameObject }, turning.transform);
            var asset = PrefabUtility.SaveAsPrefabAsset(node, Prefabs + "PF_InspectPlant.prefab"); Object.DestroyImmediate(node);
            var entity = System.Linq.Enumerable.Single(root.GetComponentsInChildren<SceneEntity>(true), x => x.Id.Value == "plant");
            var adapter = entity.GetComponent<InspectableObject>(); if (adapter == null) adapter = entity.gameObject.AddComponent<InspectableObject>(); adapter.Configure(entity, asset);
            root.GetComponentInChildren<GameBootstrap>(true).ConfigureExploration(true);
            PresentationArtPass.Save(root);
        }
        /// <summary>书本近景采用封面与三层书页，翻页动画只改变视觉，不模拟纸张。</summary>
        public static void InstallBookInspection()
        {
            var root = PresentationArtPass.Room();
            var node = new GameObject("PF_InspectBook");
            var paper = Material("Paper", new Color(.9f, .85f, .70f)); var leather = Material("BookCover", new Color(.28f, .12f, .085f));
            var gold = Material("BookInk", new Color(.48f, .34f, .15f));
            var cover = new GameObject("Cover"); cover.transform.SetParent(node.transform, false);
            var closed = Hotspot("Cover", cover.transform, "cover", Vector3.zero, new Vector3(1.65f, 1.85f, .15f), leather, Vector3.back);
            Text3D("CoverTitle", closed.transform, "关于光的习作", new Vector3(0, .14f, -.56f), .13f, TextAnchor.MiddleCenter);
            Text3D("CoverHint", closed.transform, "轻触封面", new Vector3(0, -.26f, -.56f), .07f, TextAnchor.MiddleCenter);
            var pages = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                pages[i] = new GameObject("Page" + (i + 1)); pages[i].transform.SetParent(node.transform, false);
                Primitive("Paper", pages[i].transform, PrimitiveType.Cube, Vector3.zero, new Vector3(3.05f, 1.72f, .10f), paper);
                Primitive("Spine", pages[i].transform, PrimitiveType.Cube, new Vector3(0, 0, -.065f), new Vector3(.025f, 1.7f, .015f), gold);
                Text3D("PageNumber", pages[i].transform, "—— " + (i + 1) + " ——", new Vector3(.74f, -.68f, -.072f), .05f, TextAnchor.MiddleCenter);
                if (i < 2) PageCorner(pages[i].transform, "next" + (i + 1), 1.30f, "→", paper);
                if (i > 0) PageCorner(pages[i].transform, "previous" + (i + 1), -1.30f, "←", paper);
                var close = Hotspot("Close", pages[i].transform, "close", new Vector3(0, .73f, -.077f), new Vector3(.55f, .12f, .025f), paper, Vector3.back);
                Text3D("CloseHint", close.transform, "合上", new Vector3(0, 0, -.7f), .075f, TextAnchor.MiddleCenter);
            }
            Text3D("FirstText", pages[0].transform, "一盆静默的绿。\n\n土壤并没有停下。", new Vector3(-1.27f, .36f, -.076f), .09f, TextAnchor.UpperLeft);
            Text3D("Margin", pages[0].transform, "读下去，\n看看它朝向哪里。", new Vector3(.22f, .2f, -.076f), .08f, TextAnchor.UpperLeft);
            Text3D("QuoteStart", pages[1].transform, "枝叶仍", new Vector3(-1.28f, .40f, -.076f), .105f, TextAnchor.UpperLeft);
            Text3D("QuoteWord", pages[1].transform, "向光", new Vector3(-1.28f, .12f, -.092f), .15f, TextAnchor.UpperLeft);
            Text3D("QuoteEnd", pages[1].transform, "处伸去。", new Vector3(-1.28f, -.20f, -.076f), .105f, TextAnchor.UpperLeft);
            // 热点放在短语下的细线位置，初始同纸色；悬停才出现轻微墨色反馈。
            var quote = Hotspot("Quote", pages[1].transform, "quote", new Vector3(-.97f, -.025f, -.085f), new Vector3(.63f, .34f, .018f), paper, Vector3.back);
            var underline = Primitive("ObservedUnderline", quote.transform, PrimitiveType.Cube, new Vector3(0, -.48f, -.8f), new Vector3(.95f, .035f, .20f), gold); underline.SetActive(false);
            Text3D("SketchNote", pages[1].transform, "新叶还很小，\n却有自己的方向。", new Vector3(.20f, .35f, -.076f), .08f, TextAnchor.UpperLeft);
            Text3D("HorizonText", pages[2].transform, "页边没有尽头。", new Vector3(-1.27f, .34f, -.076f), .09f, TextAnchor.UpperLeft);
            var horizon = Hotspot("Horizon", pages[2].transform, "horizon", new Vector3(.70f, .02f, -.085f), new Vector3(1.05f, .48f, .02f), Material("Horizon", new Color(.41f, .50f, .47f)), Vector3.back);
            Primitive("HorizonLine", horizon.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(.96f, .025f, 1.1f), gold);
            Text3D("HorizonCaption", pages[2].transform, "这一页，明天也许会不同。", new Vector3(.12f, -.42f, -.076f), .055f, TextAnchor.UpperLeft);
            var turn = new GameObject("PagePivot"); turn.transform.SetParent(node.transform, false); turn.transform.localPosition = new Vector3(-1.52f, 0, -.12f);
            Primitive("TurningPage", turn.transform, PrimitiveType.Cube, new Vector3(.74f, 0, 0), new Vector3(1.48f, 1.72f, .025f), paper);
            node.AddComponent<InspectionView>().Configure(new[] { "closed", "page1", "page2", "page3" }, new[] { cover, pages[0], pages[1], pages[2] }, turn.transform);
            var asset = PrefabUtility.SaveAsPrefabAsset(node, Prefabs + "PF_InspectBook.prefab"); Object.DestroyImmediate(node);
            var entity = System.Linq.Enumerable.Single(root.GetComponentsInChildren<SceneEntity>(true), x => x.Id.Value == "book");
            var adapter = entity.GetComponent<InspectableObject>(); if (adapter == null) adapter = entity.gameObject.AddComponent<InspectableObject>(); adapter.Configure(entity, asset);
            PresentationArtPass.Save(root);
        }
        private static void PageCorner(Transform page, string action, float x, string symbol, Material material)
        {
            var hotspot = Hotspot(action, page, action, new Vector3(x, -.60f, -.077f), new Vector3(.30f, .27f, .025f), material, Vector3.back);
            Text3D("Arrow", hotspot.transform, symbol, new Vector3(0, 0, -.7f), .14f, TextAnchor.MiddleCenter);
        }
        /// <summary>近景书页采用嵌入的中文 TextMesh；文字不是世界热点标签。</summary>
        public static TextMesh Text3D(string name, Transform parent, string value, Vector3 position, float size, TextAnchor anchor)
        {
            var node = new GameObject(name); node.transform.SetParent(parent, false); node.transform.localPosition = position;
            // 父节点可能是扁平热点；抵消缩放，字体尺寸始终以世界单位指定。
            node.transform.localScale = new Vector3(1 / parent.localScale.x, 1 / parent.localScale.y, 1 / parent.localScale.z);
            var text = node.AddComponent<TextMesh>(); text.font = AssetDatabase.LoadAssetAtPath<Font>(DemoSceneBuilder.ChineseFontPath);
            text.GetComponent<Renderer>().sharedMaterial = text.font.material; text.fontSize = 64; text.characterSize = size; text.anchor = anchor; text.alignment = TextAlignment.Left;
            text.color = new Color(.21f, .15f, .10f); text.text = value; return text;
        }
        /// <summary>将纸面分成事物、观察、概念；不把任何正确组合写进按钮逻辑。</summary>
        public static void InstallNotebookExploration()
        {
            var root = PresentationArtPass.Room(); var notebook = root.GetComponentInChildren<NotebookView>(true);
            var body = notebook.transform.Find("NotebookPanel");
            // 现有纸页可能来自 Prefab 覆盖，仍沿用同一个实例与所有事件绑定。
            if (body == null) body = notebook.GetComponentsInChildren<CanvasGroup>(true)[0].transform;
            var objects = Rect("Objects", body, new Vector2(.05f, .58f), new Vector2(.31f, .81f));
            var observations = Rect("Observations", body, new Vector2(.36f, .58f), new Vector2(.64f, .81f));
            var concepts = Rect("Concepts", body, new Vector2(.69f, .58f), new Vector2(.95f, .81f));
            var layout = observations.GetComponent<VerticalLayoutGroup>(); if (layout == null) layout = observations.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true; layout.childControlHeight = false; layout.childForceExpandHeight = false; layout.spacing = 4;
            var ink = new Color(.22f, .17f, .11f);
            Label("ObjectsHeading", body, "看见的事物", new Vector2(.05f, .82f), new Vector2(.31f, .88f), 18).color = ink;
            Label("ObservationsHeading", body, "记下的细节", new Vector2(.36f, .82f), new Vector2(.64f, .88f), 18).color = ink;
            Label("ConceptsHeading", body, "想出的意义", new Vector2(.69f, .82f), new Vector2(.95f, .88f), 20).color = ink;
            var word = Label("InsightWord", body, "", new Vector2(.25f, .44f), new Vector2(.75f, .55f), 42); word.color = ink; word.alignment = TextAnchor.MiddleCenter; word.gameObject.SetActive(false);
            notebook.ConfigureExploration(observations, word);
            PresentationArtPass.Save(root);
        }
        private static GameObject CubePrefab()
        {
            var node = new GameObject("PF_InspectionCube");
            var closed = Primitive("Closed", node.transform, PrimitiveType.Cube, Vector3.zero, Vector3.one * 1.2f, Material("Sample", new Color(.36f, .29f, .18f)));
            Hotspot("Open", closed.transform, "open", new Vector3(0, 0, -.51f), new Vector3(.8f, .8f, .05f), Material("SampleMarker", new Color(.65f, .53f, .3f)), Vector3.back);
            var opened = Primitive("Opened", node.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(1.3f, .5f, 1), Material("Paper", new Color(.9f, .85f, .7f)));
            Hotspot("Close", opened.transform, "close", new Vector3(0, 0, -.51f), new Vector3(.8f, .8f, .05f), Material("SampleMarker", new Color(.65f, .53f, .3f)), Vector3.back);
            var turning = Primitive("Turning", node.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(.9f, .9f, .03f), Material("Paper", new Color(.9f, .85f, .7f)));
            node.AddComponent<InspectionView>().Configure(new[] { "closed", "open" }, new[] { closed, opened }, turning.transform);
            var asset = PrefabUtility.SaveAsPrefabAsset(node, Prefabs + "PF_InspectionCube.prefab"); Object.DestroyImmediate(node); return asset;
        }
        /// <summary>少量统一材质；重跑工具只复用，不追加 DefaultMaterial 变体。</summary>
        public static Material Material(string name, Color color)
        {
            string path = "Assets/_Game/Content/Art/Materials/Inspection/M_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.color = color; material.SetFloat("_Smoothness", .28f); return material;
        }
        /// <summary>创建展示网格，普通视觉不带 Collider，只有显式热点响应射线。</summary>
        public static GameObject Primitive(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var node = GameObject.CreatePrimitive(type); node.name = name; node.transform.SetParent(parent, false); node.transform.localPosition = position; node.transform.localScale = scale;
            Object.DestroyImmediate(node.GetComponent<Collider>()); node.GetComponent<Renderer>().sharedMaterial = material; return node;
        }
        /// <summary>独立热点 Collider；状态切换由模型决定，不能根据网格名取得线索。</summary>
        public static InspectionHotspot Hotspot(string name, Transform parent, string action, Vector3 position, Vector3 scale, Material material, Vector3 facing)
        {
            var node = Primitive(name, parent, PrimitiveType.Cube, position, scale, material); node.AddComponent<BoxCollider>();
            var hotspot = node.AddComponent<InspectionHotspot>(); hotspot.Configure(action, node.GetComponent<Renderer>(), facing); return hotspot;
        }
        /// <summary>统一创建 UI 布局，所有文字使用嵌入中文字体。</summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var found = parent.Find(name); var rect = found != null ? found.GetComponent<RectTransform>() : new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            if (found == null) rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        public static Text Label(string name, Transform parent, string value, Vector2 min, Vector2 max, int size = 18)
        {
            var rect = Rect(name, parent, min, max); var text = rect.GetComponent<Text>(); if (text == null) text = rect.gameObject.AddComponent<Text>();
            text.font = AssetDatabase.LoadAssetAtPath<Font>(DemoSceneBuilder.ChineseFontPath); text.fontSize = size; text.text = value; text.color = new Color(.94f, .88f, .74f); text.raycastTarget = false; return text;
        }
        public static Button Button(string name, Transform parent, string value, Vector2 min, Vector2 max)
        {
            var rect = Rect(name, parent, min, max); var image = rect.GetComponent<Image>(); if (image == null) image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.19f, .16f, .12f, .95f);
            var button = rect.GetComponent<Button>(); if (button == null) button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Label("Text", rect, value, Vector2.zero, Vector2.one).alignment = TextAnchor.MiddleCenter; return button;
        }
    }
}
