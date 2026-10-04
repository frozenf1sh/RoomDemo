// =============================================================================
// 文件：WorldSceneSetup.cs
// 职责：增量拆分房间建筑，并生成具体星空样板所需的外景与技术绑定。
// 边界：不重建主场景、不改玩法 ID；只操作建筑和视觉资产，所有修改走 Editor API。
// =============================================================================
using System.Linq;
using System.IO;
using Game.Unity;
using UnityEditor;
using UnityEngine;
namespace Game.Editor
{
    public static class WorldSceneSetup
    {
        /// <summary>保留世界坐标拆分建筑；窗根仍保持 Logic／Visual 包装结构。</summary>
        public static void ModularizeRoom()
        {
            var room = PresentationArtPass.Room(); var environment = room.Find("Environment");
            var architecture = PresentationArtPass.Ensure(environment, "Architecture");
            var existing = architecture.GetComponent<RoomModules>(); if (existing != null) return;
            var shell = environment.Find("RoomShell");
            Unpack(shell.gameObject);
            var floor = shell.Find("Floor"); var left = shell.Find("LeftWall"); var right = shell.Find("RightWall");
            floor.SetParent(architecture, true); left.SetParent(architecture, true); right.SetParent(architecture, true);
            var back = PresentationArtPass.Ensure(architecture, "Wall_Back");
            foreach (var name in new[] { "BackLeft", "BackRight", "BackBelow", "BackAbove" }) shell.Find(name).SetParent(back, true);
            var ceiling = environment.Find("Decoration/Ceiling"); ceiling.SetParent(architecture, true);
            var details = PresentationArtPass.Ensure(architecture, "FloorDetails");
            foreach (Transform item in environment.Find("Decoration").Cast<Transform>().ToArray())
                if (item.name.StartsWith("FloorJoint") || item.name.Contains("Skirting")) item.SetParent(details, true);
            var window = room.GetComponentInChildren<WindowPresenter>(true).transform; Unpack(window.gameObject);
            var visual = window.Find("Visual");
            var frame = PresentationArtPass.Ensure(visual, "Frame"); var glass = PresentationArtPass.Ensure(visual, "Glass");
            var exterior = PresentationArtPass.Ensure(visual, "ExteriorPortal"); var curtains = PresentationArtPass.Ensure(visual, "Curtains"); PresentationArtPass.Ensure(visual, "FX");
            foreach (var item in visual.Cast<Transform>().ToArray())
            {
                if (item.name.StartsWith("Frame") && item != frame || item.name == "Crossbar") item.SetParent(frame, true);
                if (item.name == "NearBackground") item.SetParent(glass, true);
                if (item.name == "WindowFarBackground" || item.name == "OceanBackground") item.SetParent(exterior, true);
                if (item.name.StartsWith("Curtain") && item != curtains) item.SetParent(curtains, true);
            }
            // 装饰 Prefab 已解包，几何能独立退去；实体身份和 Collider 仍保留原值。
            architecture.gameObject.AddComponent<RoomModules>().Configure(floor, ceiling, back, left, right, visual, details);
            PresentationArtPass.Save(room);
        }
        /// <summary>逐文件整理自然模型；仅导入三个小 FBX 和许可，禁用外部材质副本。</summary>
        public static void ImportNature()
        {
            AssetDatabase.Refresh();
            foreach (string name in new[] { "tree_pineRoundA", "rock_largeA", "campfire_logs" })
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/ThirdParty/Kenney/NatureKit/" + name + ".fbx");
                importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.importAnimation = false; importer.SaveAndReimport();
            }
        }
        /// <summary>创建固定外景，不在运行中生成地形；初始视觉全部隐藏。</summary>
        public static void CreateExterior()
        {
            var room = PresentationArtPass.Room(); var group = PresentationArtPass.Ensure(room.Find("Presentation"), "World");
            var exterior = PresentationArtPass.Ensure(group, "WorldExterior");
            var field = PresentationArtPass.Ensure(exterior, "Landscape");
            var grass = SurfaceArtPass.Material("WorldExterior", "M_Grass", new Color(.20f, .29f, .17f));
            var rock = SurfaceArtPass.Material("WorldExterior", "M_Rock", new Color(.29f, .32f, .34f));
            var leaf = SurfaceArtPass.Material("WorldExterior", "M_Pine", new Color(.12f, .25f, .19f));
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/Art/Materials/Environment/M_WoodDark.mat");
            SurfaceArtPass.Cube("Ground", field, new Vector3(0, -.12f, 10), new Vector3(52, .22f, 52), grass);
            var tree = NaturePrefab("tree_pineRoundA", "PF_WorldPine", 5.4f, leaf);
            var stone = NaturePrefab("rock_largeA", "PF_WorldRock", .75f, rock);
            for (int i = 0; i < 8; i++)
            {
                Place("Pine" + i, field, tree, new Vector3(-14 + i * 4, 0, 12 + (i % 3) * 3), i * 39);
                Place("Rock" + i, field, stone, new Vector3(-7 + i * 2.4f, 0, 5.8f + (i % 2) * 2), i * 48);
            }
            // 边缘草叶给木地板与外景连续的尺度线索；不是碰撞或可收集物。
            for (int i = 0; i < 32; i++)
            {
                var tuft = SurfaceArtPass.Cube("Grass" + i, field, new Vector3(-7 + (i % 16) * .94f, .15f, 3.6f + i / 16 * 2.6f), new Vector3(.05f, .32f, .13f), grass);
                tuft.localRotation = Quaternion.Euler(0, i * 37, -10 + i % 5 * 4);
            }
            var deck = PresentationArtPass.Ensure(exterior, "RetainedFloor");
            var floor = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/Art/Materials/Environment/M_Floor.mat");
            SurfaceArtPass.Cube("OldWood", deck, new Vector3(0, .001f, -.8f), new Vector3(7.4f, .04f, 6.8f), floor);
            var sky = PresentationArtPass.Ensure(group, "StarrySky");
            var skyNode = sky.Find("Dome"); if (skyNode == null) skyNode = ExplorationSceneSetup.Primitive("Dome", sky, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 160, SkyMaterial()).transform;
            skyNode.GetComponent<Renderer>().sharedMaterial = SkyMaterial();
            var anchor = PresentationArtPass.Ensure(exterior, "CampfireAnchor"); anchor.localPosition = new Vector3(.35f, 0, 5.7f);
            var fire = CampfirePrefab(wood);
            var bindings = group.GetComponent<WorldExterior>(); if (bindings == null) bindings = group.gameObject.AddComponent<WorldExterior>();
            bindings.Configure(field.gameObject, deck.gameObject, sky.gameObject, fire, anchor);
            field.gameObject.SetActive(false); deck.gameObject.SetActive(false); sky.gameObject.SetActive(false);
            PresentationArtPass.Save(room);
        }
        private static Material SkyMaterial()
        {
            const string path = "Assets/_Game/Content/Art/Materials/WorldExterior/M_StarrySky.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Game/StarrySky")); AssetDatabase.CreateAsset(material, path); }
            return material;
        }
        private static GameObject CampfirePrefab(Material wood)
        {
            var root = new GameObject("PF_Campfire");
            var logs = NaturePrefab("campfire_logs", "PF_FireLogs", .35f, wood);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(logs); model.transform.SetParent(root.transform, false);
            var flames = new Transform[3];
            var ember = SurfaceArtPass.Material("WorldExterior", "M_Ember", new Color(1, .46f, .08f)); ember.EnableKeyword("_EMISSION"); ember.SetColor("_EmissionColor", new Color(1.3f, .45f, .04f));
            for (int i = 0; i < flames.Length; i++)
                flames[i] = ExplorationSceneSetup.Primitive("Flame" + i, root.transform, PrimitiveType.Capsule, new Vector3((i - 1) * .13f, .54f, 0), new Vector3(.20f, .33f - i * .035f, .17f), ember).transform;
            var glow = new GameObject("Glow").AddComponent<Light>(); glow.transform.SetParent(root.transform, false); glow.transform.localPosition = Vector3.up * .7f;
            glow.type = LightType.Point; glow.range = 10; glow.intensity = 3.3f; glow.color = new Color(1, .52f, .20f); glow.shadows = LightShadows.None;
            var visual = root.AddComponent<CampfireVisual>(); visual.Configure(flames, glow, AudioArtPass.Source(root.transform, "Fire", true, .29f));
            var asset = PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Game/Content/Prefabs/World/PF_Campfire.prefab"); Object.DestroyImmediate(root); return asset;
        }
        private static GameObject NaturePrefab(string modelName, string prefabName, float height, Material material)
        {
            Directory.CreateDirectory("Assets/_Game/Content/Prefabs/World");
            var root = new GameObject(prefabName); var visual = PresentationArtPass.Ensure(root.transform, "Visual");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Kenney/NatureKit/" + modelName + ".fbx");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model); instance.transform.SetParent(visual, false);
            var renderers = instance.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray(); }
            float scale = height / bounds.size.y; visual.localScale = Vector3.one * scale; visual.localPosition = -bounds.center * scale + Vector3.up * height / 2;
            foreach (var collider in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            var asset = PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Game/Content/Prefabs/World/" + prefabName + ".prefab"); Object.DestroyImmediate(root); return asset;
        }
        private static void Place(string name, Transform parent, GameObject prefab, Vector3 position, float yaw)
        {
            var previous = parent.Find(name); if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab); instance.name = name; instance.transform.SetParent(parent, false); instance.transform.localPosition = position; instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }
        /// <summary>建立薄主题数据和直接表现操作；不替换已有海画导演。</summary>
        public static void InstallMutationTools()
        {
            var room = PresentationArtPass.Room(); var world = room.GetComponentInChildren<WorldExterior>(true).transform;
            Directory.CreateDirectory("Assets/_Game/Content/Art/WorldExterior/Themes");
            var theme = world.GetComponent<VisualThemeController>(); if (theme == null) theme = world.gameObject.AddComponent<VisualThemeController>();
            var ending = new SerializedObject(room.GetComponentInChildren<EndingPresenter>(true));
            theme.Configure((Light)ending.FindProperty("roomLight").objectReferenceValue, (Light)ending.FindProperty("warmFill").objectReferenceValue,
                (Light)ending.FindProperty("windowLight").objectReferenceValue, (Light)ending.FindProperty("seaLight").objectReferenceValue, AudioArtPass.Source(world, "Night", true, .23f, false));
            var mutation = world.GetComponent<WorldMutation>(); if (mutation == null) mutation = world.gameObject.AddComponent<WorldMutation>(); mutation.Configure(theme);
            const string path = "Assets/_Game/Content/Art/WorldExterior/Themes/StarryWorld.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VisualThemeProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VisualThemeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            profile.Ambience = AudioArtPass.Clip("Night", true); EditorUtility.SetDirty(profile);
            PresentationArtPass.Save(room);
        }
        /// <summary>增量绑定一个具体世界导演；不重建家具、玩法实体或人工摆放。</summary>
        public static void InstallStarrySequence()
        {
            var root = PresentationArtPass.Room(); var world = root.GetComponentInChildren<WorldExterior>(true);
            var node = PresentationArtPass.Ensure(root.Find("Presentation/Sequences"), "StarryWorldSequence");
            var sequence = node.GetComponent<StarryWorldSequence>(); if (sequence == null) sequence = node.gameObject.AddComponent<StarryWorldSequence>();
            var canvas = root.Find("UI/DebugCanvas");
            var caption = canvas.Find("StarryCaption"); if (caption != null) Object.DestroyImmediate(caption.gameObject);
            var text = ExplorationSceneSetup.Label("StarryCaption", canvas, "", new Vector2(.24f, .10f), new Vector2(.76f, .20f), 25);
            text.alignment = TextAnchor.MiddleCenter; text.color = new Color(.91f, .85f, .66f); text.raycastTarget = false;
            var window = root.GetComponentsInChildren<SceneEntity>(true).Single(x => x.Id == Game.Domain.DemoRules.Window);
            var opening = AudioArtPass.Source(node, "Paper", false, .28f); opening.pitch = .60f; opening.transform.localPosition = Vector3.up * 4;
            var outside = AudioArtPass.Source(node, "Ocean", false, .24f); outside.pitch = .72f;
            sequence.Configure(root.GetComponentInChildren<RoomModules>(true), world, world.GetComponent<WorldMutation>(), world.GetComponent<VisualThemeController>(),
                AssetDatabase.LoadAssetAtPath<VisualThemeProfile>("Assets/_Game/Content/Art/WorldExterior/Themes/StarryWorld.asset"), root.GetComponentInChildren<EndingPresenter>(true),
                root.GetComponentInChildren<WindPresenter>(true), root.GetComponentInChildren<Camera>(true), window.GetComponent<Collider>(), opening, outside, text);
            root.GetComponentInChildren<GameBootstrap>(true).ConfigureStarrySequence(sequence);
            PresentationArtPass.Save(root);
        }
        private static void Unpack(GameObject node)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(node))
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(node), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
    }
}
