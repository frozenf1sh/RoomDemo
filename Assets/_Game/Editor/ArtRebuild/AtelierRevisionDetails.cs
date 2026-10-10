// 文件：AtelierRevisionDetails.cs
// 职责：导入真实厚木工作台与完整旧屋，收紧室内纵深，并细化地面和窗帘。
// 架构边界：只写独立美术场景专属资源；保留首版证据，不触碰旧玩法与原始资源。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Editor.ArtRebuild
{
    /// <summary>主视觉资产与空间的第二轮返工。</summary>
    public static class AtelierRevisionDetails
    {
        private const string Art = "Assets/_Game/Content/ArtRebuild/";

        /// <summary>只处理本轮拥有的导入件，不修改原正式模型的导入设置。</summary>
        public static void ImportHeroAssets()
        {
            AssetDatabase.Refresh();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Art + "Imports/QuixelTable", Art + "Imports/JimbogiesHouse", Art + "Imports/Revision" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.Contains("Normal") || path.Contains("_normal") || path.Contains("_nor_");
                bool data = normal || path.Contains("Rough") || path.Contains("_rough") || path.Contains("URPMask") || path.Contains("AO.") || path.Contains("_ao_") || path.Contains("metallicRoughness") || path.Contains("_alpha_");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !data && !path.EndsWith(".hdr");
                importer.maxTextureSize = path.Contains("QuixelTable") && !data ? 4096 : 2048;
                importer.isReadable = path.Contains("_rough") || path.Contains("Roughness") || path.Contains("_metallic_");
                importer.SaveAndReimport();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Art + "Imports/QuixelTable", Art + "Imports/JimbogiesHouse" }))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.isReadable = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>按扫描比例摆放厚木台，随后把物品实际底面落在新桌面上。</summary>
        public static void AdoptTable()
        {
            var root = Root();
            var furniture = root.Find("室内家具");
            if (furniture.Find("Quixel 厚木工作台") != null) throw new InvalidOperationException("厚木台已存在。");
            var mat = Surface("Quixel 厚木扫描", Art + "Imports/QuixelTable/", "BaseColor", "Normal", "Roughness", "AO");
            mat.SetColor("_BaseColor", new Color(.88f, .80f, .66f));
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Imports/QuixelTable/Wooden_Table_ulzrcgoaw_High.fbx");
            var table = InstantiateSized(source, furniture, "Quixel 厚木工作台", new Vector3(.18f, 0, -.86f), .94f, 0);
            foreach (var renderer in table.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = mat;
            furniture.Find("有磨损的扫描工作台").gameObject.SetActive(false);
            var bounds = BoundsOf(table);
            float change = bounds.max.y - 1.045f;
            var props = root.Find("桌面与创作痕迹");
            foreach (Transform t in props)
                if (!t.name.Contains("窗台") && !t.name.Contains("书柜")) t.position += Vector3.up * change;
            foreach (Transform t in root.Find("返工_扫描工作簇"))
                if (t.name.Contains("桌面") || t.name.Contains("工作台") || t.name.Contains("平放") || t.name.Contains("油灯")) t.position += Vector3.up * change;
            foreach (Transform t in root.Find("照明"))
                if (t.name.StartsWith("桌灯")) t.position += Vector3.up * change;
            AtelierSceneBuilder.Save();
        }

        /// <summary>用完整的授权旧屋替换首版小屋，保留窗外真实前后距离。</summary>
        public static void AdoptHouses()
        {
            var landscape = Root().Find("窗外山谷");
            if (landscape.Find("返工_完整旧屋") != null) throw new InvalidOperationException("完整旧屋已存在。");
            var group = AtelierSceneBuilder.Group(landscape, "返工_完整旧屋");
            const string folder = Art + "Imports/JimbogiesHouse/";
            var data = JsonUtility.FromJson<HouseChannels>(File.ReadAllText(folder + "材质通道.json"));
            var mapped = new Dictionary<string, Material>();
            foreach (var channel in data.materials)
            {
                var mat = MaterialAsset("旧屋扫描_" + channel.name);
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + channel.baseColor));
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + channel.normal));
                if (!string.IsNullOrEmpty(channel.normal)) mat.EnableKeyword("_NORMALMAP");
                mat.SetFloat("_BumpScale", .75f);
                mat.SetFloat("_Smoothness", .18f);
                mat.SetColor("_BaseColor", new Color(.8f, .77f, .68f));
                if (!string.IsNullOrEmpty(channel.packed))
                {
                    mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + channel.packed));
                    mat.SetFloat("_Smoothness", 1);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                if (channel.name == "Glass01") mat.SetColor("_BaseColor", new Color(.12f, .14f, .16f));
                mapped[channel.name] = mat;
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "House.obj");
            var positions = new[] { new Vector2(-.3f, 40), new Vector2(9.1f, 57), new Vector2(17.5f, 78) };
            for (int i = 0; i < 3; i++)
            {
                var p = positions[i];
                var house = InstantiateSized(source, group, "Jimbogies 旧屋_" + i, new Vector3(p.x, AtelierRevision.Height(p.x, p.y) - .23f, p.y), 3.6f - i * .25f, i == 0 ? 156 : i == 1 ? 197 : 142);
                foreach (var renderer in house.GetComponentsInChildren<MeshRenderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => mapped.TryGetValue(m.name, out var own) ? own : mapped.Values.First()).ToArray();
                SparseWindows(house, i);
            }
            foreach (Transform t in landscape.Find("中景错落小屋"))
                if (!t.name.StartsWith("山坡树_")) t.gameObject.SetActive(false);
            foreach (var stone in landscape.Find("返工_自然坡地").Cast<Transform>().Where(t => t.name.StartsWith("护坡基石_")))
            {
                var parts = stone.name.Split('_');
                int house = int.Parse(parts[1]), index = int.Parse(parts[2]);
                var p = positions[house];
                float x = p.x - 2.8f + index * .8f;
                float z = p.y - 2.4f + .18f * Mathf.Sin(index * 2);
                stone.localPosition = new Vector3(x, AtelierRevision.Height(x, z) - .12f, z);
            }
            AtelierSceneBuilder.Save();
        }

        /// <summary>收紧工作台与后墙距离，同时保持所有后墙家具和冷光的空间对应。</summary>
        public static void TightenRoom()
        {
            var root = Root();
            if (root.Find("返工_空间收紧标记") != null) throw new InvalidOperationException("室内纵深已收紧。");
            AtelierSceneBuilder.Group(root, "返工_空间收紧标记");
            var architecture = root.Find("建筑与木作");
            foreach (Transform t in architecture)
                if (t.name.StartsWith("窗") || t.name.StartsWith("旧窗") || t.name.StartsWith("后墙") || t.name.StartsWith("墙角") || t.name == "墙根压脚木") t.position += Vector3.back * 1.65f;
            var furniture = root.Find("室内家具");
            furniture.Find("Krag 雕脚旧书柜").position += Vector3.back * 1.65f;
            furniture.Find("左侧旧立柜").position += Vector3.back * 1.65f;
            furniture.Find("细木作画架").position += Vector3.back * .5f;
            var canvas = furniture.Find("细木作画架/绷布与木框");
            canvas.localScale = new Vector3(1.2f, .9f, 1);
            foreach (Transform t in root.Find("桌面与创作痕迹"))
                if (t.name.Contains("窗台") || t.name.StartsWith("书柜")) t.position += Vector3.back * 1.65f;
            foreach (Transform t in root.Find("返工_扫描工作簇"))
                if (t.name.Contains("书架") || t.name.Contains("书柜") || t.name.Contains("左柜") || t.name.Contains("窗台")) t.position += Vector3.back * 1.65f;
            foreach (Transform t in root.Find("照明"))
                if (t.name.Contains("窗洞") || t.name.Contains("窗台")) t.position += Vector3.back * 1.65f;
            root.Find("原创风景习作/背墙构图小稿").position += Vector3.back * 1.65f;
            var window = architecture.Find("旧窗与亚麻帘");
            for (int i = 0; i < 2; i++)
                window.Find("亚麻帘褶_" + i).GetComponent<MeshFilter>().sharedMesh = AtelierMeshLibrary.Save("返工_自然收束亚麻帘_" + i, Curtain(i));
            AtelierSceneBuilder.Save();
        }

        /// <summary>地面、墙脚和屋面使用各自的实际扫描材质，远山改为三维起伏。</summary>
        public static void FinishLandscapeMaterials()
        {
            var landscape = Root().Find("窗外山谷");
            var ground = Surface("扫描泥地与碎叶", Art + "Imports/Revision/forest_ground_04/", "_diff", "_nor_gl", "_rough", "_ao");
            ground.SetColor("_BaseColor", new Color(.44f, .53f, .40f));
            landscape.Find("近景枝叶与坡地/连续起伏的山坡").GetComponent<MeshRenderer>().sharedMaterial = ground;
            var stone = Surface("扫描旧石墙", Art + "Imports/Revision/old_stone_wall_02/", "_diff", "_nor_gl", "_rough", "_ao");
            var roof = Surface("扫描风化石板瓦", Art + "Imports/Revision/grey_roof_tiles_02/", "_diff", "_nor_gl", "_rough", "_ao");
            foreach (var renderer in landscape.Find("返工_完整旧屋").GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.name == "roof") renderer.sharedMaterial = roof;
                if (renderer.name == "Block02") renderer.sharedMaterial = stone;
            }
            for (int i = 0; i < 3; i++)
            {
                var layer = landscape.Find("远山轮廓/远山层_" + i);
                layer.GetComponent<MeshFilter>().sharedMesh = AtelierMeshLibrary.Save("返工_三维远山_" + i, Mountain(i));
                var mat = MaterialAsset("远山空气透视_" + i, "Universal Render Pipeline/Unlit");
                mat.SetColor("_BaseColor", new Color(.15f + i * .035f, .20f + i * .04f, .28f + i * .045f));
                layer.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            var skybox = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/普通暮色 HDRI.mat");
            skybox.SetFloat("_Exposure", .19f);
            skybox.SetColor("_Tint", new Color(.28f, .38f, .59f));
            AtelierSceneBuilder.Save();
        }

        /// <summary>原模型窗片分连通块，只选少量窗片发光，不把整幢房屋涂亮。</summary>
        private static void SparseWindows(Transform house, int seed)
        {
            var renderer = house.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name == "Glass01");
            if (renderer == null) return;
            var filter = renderer.GetComponent<MeshFilter>();
            var original = filter.sharedMesh;
            var triangles = original.triangles;
            var parent = Enumerable.Range(0, original.vertexCount).ToArray();
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            for (int t = 0; t < triangles.Length; t += 3)
            {
                parent[Find(triangles[t + 1])] = Find(triangles[t]);
                parent[Find(triangles[t + 2])] = Find(triangles[t]);
            }
            var components = triangles.Distinct().Select(Find).Distinct().OrderBy(x => x).ToArray();
            var dark = new List<int>(); var warm = new List<int>();
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int component = Array.IndexOf(components, Find(triangles[t]));
                var target = component % 4 == seed % 4 ? warm : dark;
                target.AddRange(new[] { triangles[t], triangles[t + 1], triangles[t + 2] });
            }
            var mesh = Object.Instantiate(original);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(dark, 0); mesh.SetTriangles(warm, 1);
            filter.sharedMesh = AtelierMeshLibrary.Save("旧屋稀疏窗灯_" + seed, mesh);
            var light = MaterialAsset("旧屋局部窗灯");
            light.SetColor("_BaseColor", new Color(.55f, .30f, .10f));
            light.EnableKeyword("_EMISSION");
            light.SetColor("_EmissionColor", new Color(1.9f, .86f, .21f));
            renderer.sharedMaterials = new[] { renderer.sharedMaterial, light };
        }

        private static Mesh Curtain(int seed)
        {
            const int nx = 80, ny = 90;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int y = 0; y <= ny; y++) for (int x = 0; x <= nx; x++)
            {
                float u = (float)x / nx, v = (float)y / ny;
                float phase = u * 7.4f * Mathf.PI + .38f * Mathf.Sin(v * 3.6f + seed) + .18f * Mathf.Sin(u * 18 + v * 5);
                float gathered = .59f + .41f * Mathf.Sin(v * Mathf.PI * .5f);
                vertices.Add(new Vector3((u - .5f) * .94f * gathered + .06f * Mathf.Sin(v * 3.4f + seed),
                    -v * 2.71f + .06f * Mathf.Sin(u * 13 + seed) * v * v,
                    Mathf.Sin(phase) * (.042f + v * .034f) + .032f * Mathf.Sin(v * 5 + u * 5 + seed)));
                uv.Add(new Vector2(u * 1.5f, v * 4.8f));
            }
            for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
            {
                int a = y * (nx + 1) + x;
                triangles.AddRange(new[] { a, a + 1, a + nx + 2, a, a + nx + 2, a + nx + 1 });
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh Mountain(int layer)
        {
            const int nx = 180, nz = 32;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int z = 0; z <= nz; z++) for (int x = 0; x <= nx; x++)
            {
                float xx = -260 + x * 3f;
                float zz = 110 + layer * 65 + z * 3;
                float envelope = Mathf.Sin((float)z / nz * Mathf.PI);
                float ridge = 8 + layer * 3 + 9 * Mathf.Sin(xx * .022f + layer * 2) + 6 * Mathf.Sin(xx * .053f - layer) + 3 * Mathf.PerlinNoise(xx * .017f + 8, z * .12f + layer);
                vertices.Add(new Vector3(xx, -8 + (ridge + 8) * envelope, zz));
                uv.Add(new Vector2((float)x / nx, (float)z / nz));
            }
            for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++)
            {
                int a = z * (nx + 1) + x;
                triangles.AddRange(new[] { a, a + nx + 2, a + 1, a, a + nx + 1, a + nx + 2 });
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            return mesh;
        }

        private static Material Surface(string name, string folder, string colour, string normal, string rough, string ao)
        {
            Texture2D Map(string token) => Directory.GetFiles(folder).Where(p => !p.EndsWith(".meta") && p.Contains(token)).Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault(t => t != null);
            var mat = MaterialAsset(name, "RoomDemo/Atelier/AgedPigmentLit");
            mat.SetTexture("_BaseMap", Map(colour)); mat.SetTexture("_BumpMap", Map(normal)); mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", .55f); mat.SetFloat("_Smoothness", .14f);
            mat.SetTexture("_OcclusionMap", Map(ao)); mat.SetFloat("_OcclusionStrength", .7f); mat.EnableKeyword("_OCCLUSIONMAP");
            mat.SetFloat("_PigmentStrength", .18f); mat.SetFloat("_WearStrength", .12f); mat.SetFloat("_GrainScale", 18);
            var roughness = Map(rough);
            if (roughness != null)
            {
                string path = Art + "Textures/扫描光滑度_" + name + ".png";
                if (!File.Exists(path))
                {
                    var pixels = roughness.GetPixels();
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0, 0, 0, 1 - pixels[i].r);
                    var packed = new Texture2D(roughness.width, roughness.height, TextureFormat.RGBA32, false, true);
                    packed.SetPixels(pixels); packed.Apply(); File.WriteAllBytes(path, packed.EncodeToPNG()); Object.DestroyImmediate(packed);
                    AssetDatabase.ImportAsset(path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.sRGBTexture = false; importer.SaveAndReimport();
                }
                mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                mat.SetFloat("_Smoothness", 1); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            return mat;
        }

        private static Transform InstantiateSized(GameObject source, Transform parent, string name, Vector3 position, float height, float yaw)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            var child = Object.Instantiate(source, holder).transform;
            var bounds = BoundsOf(holder);
            child.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            holder.localScale = Vector3.one * height / bounds.size.y;
            holder.localRotation = Quaternion.Euler(0, yaw, 0);
            holder.localPosition = position;
            return holder;
        }

        private static Bounds BoundsOf(Transform transform)
        {
            var renderers = transform.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Material MaterialAsset(string name, string shader = "Universal Render Pipeline/Lit")
        {
            string path = Art + "Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find(shader)) { name = name }; AssetDatabase.CreateAsset(mat, path); }
            return mat;
        }

        private static Transform Root()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != AtelierSceneBuilder.ScenePath)
                throw new InvalidOperationException("请在已保存的独立美术画室中退出运行后操作。");
            return GameObject.Find("独立夜间画室").transform;
        }

        /// <summary>GLTF 材质通道记录容器。</summary>
        [Serializable] private sealed class HouseChannels { public HouseChannel[] materials; }
        /// <summary>原作者材质的文件引用；不会修改原始 GLTF。</summary>
        [Serializable] private sealed class HouseChannel
        {
            public string name, baseColor, normal, packed;
            public float[] baseColorFactor;
            public float metallicFactor, roughnessFactor;
        }
    }
}
