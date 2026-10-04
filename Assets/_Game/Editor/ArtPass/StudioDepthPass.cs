// =============================================================================
// 文件：StudioDepthPass.cs
// 职责：用少量许可明确的小模型和统一材质补足艺术家房间的生活细节。
// 边界：仅装饰，导入模型封装到自有 Visual；不给它们 Collider 或线索身份。
// =============================================================================
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Game.Editor
{
    public static class StudioDepthPass
    {
        /// <summary>整理四个单文件模型，不导入外部材质和动画。</summary>
        public static void ImportModels()
        {
            AssetDatabase.Refresh();
            foreach (string name in new[] { "books", "cardboardBoxClosed", "cardboardBoxOpen", "lampRoundFloor" })
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/ThirdParty/Kenney/FurnitureKit/" + name + ".fbx");
                importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.importAnimation = false; importer.SaveAndReimport();
            }
        }
        /// <summary>保留主物体周围的点击空间，小物件集中在桌面、架底与边角。</summary>
        public static void Apply()
        {
            var room = PresentationArtPass.Room(); var decor = PresentationArtPass.Ensure(room.Find("Environment"), "StudioProps");
            var paper = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/Art/Materials/Props/M_Paper.mat");
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/Art/Materials/Environment/M_WoodDark.mat");
            var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/Art/Materials/Props/M_Metal.mat");
            var bottle = SurfaceArtPass.Material("Props", "M_StudioBottle", new Color(.29f, .36f, .30f), .5f);
            var wrappers = new[] { Wrap("books", .25f, paper), Wrap("cardboardBoxClosed", .45f, wood), Wrap("cardboardBoxOpen", .48f, wood), Wrap("lampRoundFloor", 2.0f, metal) };
            Place("DeskBooks", decor, wrappers[0], new Vector3(3.0f, .85f, -.20f), 25);
            Place("ShelfBooks", decor, wrappers[0], new Vector3(-2.9f, .75f, 2.70f), 8);
            Place("ClosedDraftBox", decor, wrappers[1], new Vector3(-3.25f, 0, -.7f), 8);
            Place("OpenDraftBox", decor, wrappers[2], new Vector3(-3.2f, 0, 1.75f), -8);
            Place("OldFloorLamp", decor, wrappers[3], new Vector3(3.52f, 0, 2.78f), 0);
            for (int i = 0; i < 6; i++)
            {
                var sheet = SurfaceArtPass.Cube("DiscardedStudy" + i, decor, new Vector3(-2.2f + i * .34f, .016f, -.50f - (i % 3) * .26f), new Vector3(.28f, .01f, .40f), paper);
                sheet.localRotation = Quaternion.Euler(0, -28 + i * 19, 0);
                SurfaceArtPass.Cube("RepeatedStroke", sheet, new Vector3(0, .8f, .12f), new Vector3(.65f, .10f, .04f), wood);
            }
            for (int i = 0; i < 4; i++)
                SurfaceArtPass.Cube("PaintTube" + i, decor, new Vector3(3.15f + i * .09f, .87f, -.65f), new Vector3(.055f, .04f, .23f), metal);
            for (int i = 0; i < 2; i++)
            {
                var previous = decor.Find("DriedBottle" + i); if (previous != null) Object.DestroyImmediate(previous.gameObject);
                ExplorationSceneSetup.Primitive("DriedBottle" + i, decor, PrimitiveType.Cylinder, new Vector3(3.52f, .94f, -.05f + i * .18f), new Vector3(.08f, .09f, .08f), bottle);
            }
            foreach (var renderer in decor.GetComponentsInChildren<Renderer>())
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
            PresentationArtPass.Save(room);
        }
        /// <summary>按实际模型边界标准化高度并建立 Wrapper；不受 FBX 单位约定影响。</summary>
        public static GameObject Wrap(string name, float height, Material material)
        {
            const string folder = "Assets/_Game/Content/Prefabs/Environment/";
            Directory.CreateDirectory(folder);
            var root = new GameObject("PF_Studio_" + name); var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform, false);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Kenney/FurnitureKit/" + name + ".fbx");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model); instance.transform.SetParent(visual.transform, false);
            var renderers = instance.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            { bounds.Encapsulate(renderer.bounds); renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray(); }
            float scale = height / bounds.size.y; visual.transform.localScale = Vector3.one * scale;
            visual.transform.localPosition = -bounds.center * scale + Vector3.up * height / 2;
            foreach (var collider in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            var asset = PrefabUtility.SaveAsPrefabAsset(root, folder + root.name + ".prefab"); Object.DestroyImmediate(root); return asset;
        }
        private static void Place(string name, Transform parent, GameObject prefab, Vector3 position, float yaw)
        {
            var old = parent.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab); instance.name = name; instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position; instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }
    }
}
