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
