// =============================================================================
// 文件：SurfaceArtPass.cs
// 职责：统一房间少量基础材质，替换主物体视觉几何与第三方水生叶片。
// 边界：只操作 Visual 与装饰节点；保持 SceneEntity、Collider、规则和 ID 不变。
// =============================================================================
using System.IO;
using System.Linq;
using Game.Unity;
using UnityEditor;
using UnityEngine;
namespace Game.Editor
{
    /// <summary>服务艺术家房间的具体材质与主视觉整理工具。</summary>
    public static class SurfaceArtPass
    {
        private const string Materials = "Assets/_Game/Content/Art/Materials/";
        /// <summary>创建或复用少量命名明确的共享材质，不导入模型附带的材质副本。</summary>
        public static Material Material(string folder, string name, Color color, float smoothness = .2f)
        {
            Directory.CreateDirectory(Materials + folder);
            string path = Materials + folder + "/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            { result = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(result, path); }
            result.color = color; result.SetFloat("_Smoothness", smoothness);
            return result;
        }
        /// <summary>生成微弱原创纹理：木纹、画布或墙面颗粒，不需要外部大贴图包。</summary>
        private static Texture2D Texture(string name, bool wood)
        {
            const string folder = "Assets/_Game/Content/Art/Textures/";
            Directory.CreateDirectory(folder);
            string path = folder + name + ".png";
            var image = new Texture2D(256, 256, TextureFormat.RGB24, false);
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                // 不规则的细木纹与低对比噪声保持整屋统一，不产生额外游戏信息。
                float noise = Mathf.PerlinNoise(x * (wood ? .016f : .16f), y * (wood ? .15f : .16f));
                float grain = wood ? Mathf.Sin(y * .34f + Mathf.PerlinNoise(x * .03f, y * .008f) * 8) * .06f : ((x + y) % 2) * .012f;
                float value = .76f + noise * .18f + grain;
                image.SetPixel(x, y, new Color(value, value, value));
            }
            image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat; importer.maxTextureSize = 256; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        /// <summary>替换统一材料、画框细节、叶片与水生附加层并保存当前场景。</summary>
        [MenuItem("游戏/美术/统一基础材质与主视觉")]
        public static void Apply()
        {
            var root = PresentationArtPass.Room();
            var wall = Material("Environment", "M_Wall", new Color(.56f, .53f, .47f));
            var floor = Material("Environment", "M_Floor", new Color(.29f, .20f, .13f), .32f);
            var dark = Material("Environment", "M_WoodDark", new Color(.21f, .14f, .095f), .3f);
            var light = Material("Environment", "M_WoodLight", new Color(.40f, .29f, .19f), .25f);
            var paper = Material("Props", "M_Paper", new Color(.82f, .76f, .61f));
            var leaf = Material("Props", "M_Plant", new Color(.18f, .30f, .13f), .32f);
            var metal = Material("Props", "M_Metal", new Color(.40f, .29f, .14f), .65f); metal.SetFloat("_Metallic", .65f);
            var woodTexture = Texture("WoodGrain", true);
            var surfaceTexture = Texture("CanvasGrain", false);
            foreach (var material in new[] { floor, dark, light }) material.SetTexture("_BaseMap", woodTexture);
            foreach (var material in new[] { wall, paper }) material.SetTexture("_BaseMap", surfaceTexture);
            // 通过旧材料的用途映射共享基础库，避免每件家具拥有重复材料。
            foreach (var renderer in root.Find("Environment").GetComponentsInChildren<Renderer>(true)
                .Concat(root.Find("Interactables").GetComponentsInChildren<Renderer>(true)))
            {
                var old = renderer.sharedMaterial; if (old == null) continue;
                string name = old.name;
                if (name == "Wall") renderer.sharedMaterial = wall;
                if (name == "Wood") renderer.sharedMaterial = light;
                if (name == "DarkWood") renderer.sharedMaterial = dark;
                if (name == "Paper") renderer.sharedMaterial = paper;
                if (name == "Leaves") renderer.sharedMaterial = leaf;
            }
            root.Find("Environment/RoomShell/Floor").GetComponent<Renderer>().sharedMaterial = floor;
            var decor = PresentationArtPass.Ensure(root.Find("Environment"), "Decoration");
            // 木板接缝、踢脚线和画框内沿提供尺度，远比额外几十件小物体更有效。
            for (int i = 0; i < 17; i++) Cube("FloorJoint" + i, decor, new Vector3(-4 + i * .5f, .004f, 0), new Vector3(.009f, .012f, 8), dark);
            Cube("BackSkirting", decor, new Vector3(0, .10f, 3.76f), new Vector3(8, .16f, .06f), dark);
            Cube("LeftSkirting", decor, new Vector3(-3.88f, .10f, 0), new Vector3(.06f, .16f, 8), dark);
            var painting = root.GetComponentInChildren<PaintingPresenter>(true).transform.Find("Visual");
            Cube("GiltLeft", painting, new Vector3(-.52f, 0, -.10f), new Vector3(.028f, 1.27f, .02f), metal);
            Cube("GiltRight", painting, new Vector3(.52f, 0, -.10f), new Vector3(.028f, 1.27f, .02f), metal);
            Cube("GiltTop", painting, new Vector3(0, .62f, -.10f), new Vector3(1.06f, .026f, .02f), metal);
            Cube("GiltBottom", painting, new Vector3(0, -.62f, -.10f), new Vector3(1.06f, .026f, .02f), metal);
            // 原始圆球叶片换成一张有尖端和弯曲的具体叶片网格，玩法无需改变。
            var plant = root.GetComponentInChildren<PlantPresenter>(true);
            var mesh = LeafMesh();
            foreach (var filter in plant.Visual.GetComponentsInChildren<MeshFilter>(true).Where(x => x.name.StartsWith("Leaf")))
                filter.sharedMesh = mesh;
            var aquatic = plant.AquaticVisual.transform;
            foreach (Transform child in aquatic) child.gameObject.SetActive(false);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Kenney/FurnitureKit/pottedPlant.fbx");
            var modelMesh = model.GetComponentsInChildren<MeshFilter>(true).Single(x => x.name == "plant").sharedMesh;
            var wet = Material("Effects", "M_AquaticPlant", new Color(.07f, .43f, .40f), .88f);
            for (int i = 0; i < 3; i++)
            {
                var layer = PresentationArtPass.Ensure(aquatic, "AquaticLeaves" + i);
                var filter = layer.GetComponent<MeshFilter>(); if (filter == null) filter = layer.gameObject.AddComponent<MeshFilter>();
                var renderer = layer.GetComponent<MeshRenderer>(); if (renderer == null) renderer = layer.gameObject.AddComponent<MeshRenderer>();
                filter.sharedMesh = modelMesh; renderer.sharedMaterial = wet;
                layer.gameObject.SetActive(true);
                layer.localScale = Vector3.one * .14f;
                layer.localPosition = new Vector3(-.15f + i * .08f, .28f, -.17f);
                layer.localRotation = Quaternion.Euler(0, i * 120, (i - 1) * 12);
            }
            PresentationArtPass.Save(root);
        }
        /// <summary>创建或更新一个装饰立方体，不带碰撞和交互逻辑。</summary>
        public static Transform Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var found = parent.Find(name);
            if (found == null) { found = GameObject.CreatePrimitive(PrimitiveType.Cube).transform; found.name = name; found.SetParent(parent, false); Object.DestroyImmediate(found.GetComponent<Collider>()); }
            found.localPosition = position; found.localScale = scale; found.GetComponent<Renderer>().sharedMaterial = material; return found;
        }
        /// <summary>生成两面可见的弯曲叶片，复用单个网格资产。</summary>
        public static Mesh LeafMesh()
        {
            const string path = "Assets/_Game/Content/Art/Models/Leaf.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = "弯曲叶片" }; AssetDatabase.CreateAsset(mesh, path); }
            var vertices = new[] { new Vector3(0, 0, -.5f), new Vector3(-.45f, .06f, 0), new Vector3(0, .20f, .1f), new Vector3(.45f, .06f, 0), new Vector3(0, .10f, .65f) };
            var uv = new[] { new Vector2(.5f, 0), new Vector2(0, .5f), new Vector2(.5f, .55f), new Vector2(1, .5f), new Vector2(.5f, 1) };
            // 背面使用独立顶点，避免相反三角形法线相消导致叶片发黑。
            mesh.vertices = vertices.Concat(vertices).ToArray(); mesh.uv = uv.Concat(uv).ToArray();
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 1, 4, 2, 2, 4, 3, 7, 6, 5, 8, 7, 5, 7, 9, 6, 8, 9, 7 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh); return mesh;
        }
    }
}
