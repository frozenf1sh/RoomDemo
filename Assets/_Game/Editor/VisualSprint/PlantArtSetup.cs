// =============================================================================
// 文件：PlantArtSetup.cs
// 职责：增量制作艺术盆栽的曲枝、弯叶、陶盆、梦境长叶与轻量生长粒子。
// 边界：替换 Visual 并校准碰撞范围；保留实体、交互组件、检视热点、领域 ID 与人工场景位置。
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor.VisualSprint
{
    /// <summary>第五阶段盆栽专用美术工具，生成具体株形，不建立通用植物生成框架。</summary>
    public static class PlantArtSetup
    {
        private const string Models = "Assets/_Game/Content/Art/Models/ArtistPlant/";
        private const string Materials = "Assets/_Game/Content/Art/Materials/ArtistPlant/";
        private static Mesh leaf, dreamLeaf;
        private static Material baseLeaf, matureLeaf, dreamMaterial, stemMaterial, potMaterial, soilMaterial, particleMaterial;

        /// <summary>增量应用房间与近景株形；重复执行只更新本工具拥有的视觉节点。</summary>
        [MenuItem("游戏/第五阶段/重制植物株形")]
        public static void Apply()
        {
            var root = PresentationArtPass.Room();
            PrepareAssets();
            var presenter = root.GetComponentInChildren<PlantPresenter>(true);
            // 交互范围围绕盆与下部枝叶；旧灰盒的大碰撞框会遮住身后的书。
            var collider = presenter.GetComponentInChildren<SceneEntity>(true).GetComponent<BoxCollider>();
            collider.center = new Vector3(0, .68f, 0);
            collider.size = new Vector3(.80f, 1.32f, .80f);
            var foliage = presenter.Visual;
            foreach (Transform child in foliage)
            {
                // 旧模型保留为实例覆盖中的隐藏层，技术灰盒与交互身份不受美术删除影响。
                if (child != presenter.AquaticVisual.transform && child.name != "Visual_Base" && child.name != "Visual_Grown" && child.name != "PlantEffects")
                    child.gameObject.SetActive(false);
            }
            foliage.localScale = Vector3.one;
            var baseLayer = Recreate(foliage, "Visual_Base");
            var grownLayer = Recreate(foliage, "Visual_Grown");
            var aquatic = presenter.AquaticVisual.transform;
            aquatic.localScale = Vector3.one;
            foreach (Transform child in aquatic) child.gameObject.SetActive(false);
            var dreamLayer = Recreate(aquatic, "Visual_Dream");
            var effects = Recreate(foliage, "PlantEffects");
            var normals = new List<Renderer>();
            var branches = new List<Renderer>();
            var branchDelays = new List<float>();
            var dreams = new List<Renderer>();
            var dreamDelays = new List<float>();
            BuildBase(baseLayer, normals, 1);
            BuildGrown(grownLayer, branches, branchDelays);
            BuildDream(dreamLayer, dreams, dreamDelays);
            var motes = Particles(effects, "GrowthMotes", false);
            var drops = Particles(effects, "WaterDrops", true);
            var visual = foliage.GetComponent<ArtistPlantVisual>();
            if (visual == null) visual = foliage.gameObject.AddComponent<ArtistPlantVisual>();
            visual.Configure(baseLayer.gameObject, grownLayer.gameObject, aquatic.gameObject,
                normals.ToArray(), branches.ToArray(), branchDelays.ToArray(), dreams.ToArray(), dreamDelays.ToArray(), motes, drops);
            presenter.ConfigureArtistVisual(visual);
            ReplaceRoomPot(presenter.transform.Find("Visual"));
            grownLayer.gameObject.SetActive(false);
            aquatic.gameObject.SetActive(false);
            UpdateInspectionPlant();
            PresentationArtPass.Save(root);
            Debug.Log("植物美术：已安装分枝生长、舒展叶片、独立梦境株形与近景弯叶。");
        }

        /// <summary>准备少量共享叶片与统一材质；网格参数固定，重复执行不会不断产生副本。</summary>
        private static void PrepareAssets()
        {
            Directory.CreateDirectory(Models);
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh();
            leaf = Store("FoldedLeaf", LeafMesh(false));
            dreamLeaf = Store("DreamRibbon", LeafMesh(true));
            baseLeaf = PlantMaterial("M_BaseLeaf", new Color(.115f, .205f, .070f), new Color(.35f, .43f, .11f), Color.black, true, .28f);
            matureLeaf = PlantMaterial("M_GrownLeaf", new Color(.08f, .235f, .080f), new Color(.40f, .52f, .12f), Color.black, true, .36f);
            dreamMaterial = PlantMaterial("M_DreamLeaf", new Color(.075f, .28f, .215f), new Color(.57f, .75f, .39f), new Color(.085f, .19f, .13f), true, .83f);
            stemMaterial = PlantMaterial("M_Stem", new Color(.115f, .15f, .045f), new Color(.35f, .34f, .08f), Color.black, false, .28f);
            potMaterial = Lit("M_CeramicPot", new Color(.37f, .185f, .105f), .34f);
            soilMaterial = Lit("M_Soil", new Color(.06f, .037f, .024f), .08f);
            particleMaterial = ParticleMaterial();
        }

        /// <summary>三根略下垂的枝条形成初始疲倦轮廓，叶片已有自然弯曲而非球体。</summary>
        private static void BuildBase(Transform parent, List<Renderer> renderers, float multiplier)
        {
            for (int stem = 0; stem < 3; stem++)
            {
                float angle = stem * 2.399f + .4f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 a = direction * .035f;
                Vector3 b = direction * .11f + Vector3.up * .32f;
                Vector3 c = direction * .29f + Vector3.up * (.80f - stem * .07f);
                Vector3 d = direction * .38f + Vector3.up * (.72f - stem * .07f);
                var stemNode = Render(parent, "BaseStem" + stem, Store("BaseStem" + stem, Tube(a, b, c, d, .014f)), stemMaterial);
                renderers.Add(stemNode.GetComponent<Renderer>());
                for (int node = 0; node < 5; node++)
                {
                    float t = .23f + node * .145f;
                    Vector3 at = Bezier(a, b, c, d, t);
                    float leafAngle = angle + (node % 2 == 0 ? .95f : -1.25f);
                    Vector3 outward = new Vector3(Mathf.Cos(leafAngle), -.20f + node * .015f, Mathf.Sin(leafAngle)).normalized;
                    var blade = Render(parent, "BaseLeaf" + stem + "_" + node, leaf, baseLeaf);
                    blade.localPosition = at * multiplier;
                    blade.localRotation = Quaternion.LookRotation(outward, Vector3.up);
                    blade.localScale = new Vector3(.74f, 1, .48f + node * .018f) * multiplier;
                    renderers.Add(blade.GetComponent<Renderer>());
                }
            }
        }

        /// <summary>成熟形态包含七条真正改变轮廓的曲枝；左侧长枝指向旧书架。</summary>
        private static void BuildGrown(Transform parent, List<Renderer> renderers, List<float> delays)
        {
            for (int stem = 0; stem < 7; stem++)
            {
                float angle = stem * 2.399f;
                Vector3 reach = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                float height = 1.35f + (stem % 3) * .25f;
                if (stem == 0) reach = new Vector3(-1, 0, .16f);
                Vector3 a = reach * .06f;
                Vector3 b = reach * .17f + Vector3.up * .48f;
                Vector3 c = reach * (.58f + (stem == 0 ? .28f : 0)) + Vector3.up * height;
                Vector3 d = reach * (.76f + (stem == 0 ? .43f : 0)) + Vector3.up * (height - .15f);
                var branch = Render(parent, "GrownVine" + stem, Store("GrownVine" + stem, Tube(a, b, c, d, .021f)), stemMaterial);
                renderers.Add(branch.GetComponent<Renderer>());
                float delay = stem * .072f;
                delays.Add(delay);
                for (int node = 0; node < 7; node++)
                {
                    float t = .20f + node * .11f;
                    Vector3 at = Bezier(a, b, c, d, t);
                    float leafAngle = angle + (node % 2 == 0 ? .9f : -1.15f);
                    if (stem == 0) leafAngle += Mathf.PI;
                    Vector3 outward = new Vector3(Mathf.Cos(leafAngle), .11f - node * .014f, Mathf.Sin(leafAngle)).normalized;
                    var blade = Render(parent, "GrownLeaf" + stem + "_" + node, leaf, matureLeaf);
                    blade.localPosition = at;
                    blade.localRotation = Quaternion.LookRotation(outward, Vector3.up);
                    blade.localScale = new Vector3(1.02f, 1, .57f + (node % 3) * .07f);
                    renderers.Add(blade.GetComponent<Renderer>());
                    // 叶片晚于自己的茎显现，末梢比根部稍晚，因果不靠整株放大表达。
                    delays.Add(Mathf.Min(.82f, delay + .075f + t * .27f));
                }
            }
        }

        /// <summary>梦境株形为细长水草与垂落卷叶，明显区别于第一次的大叶生长。</summary>
        private static void BuildDream(Transform parent, List<Renderer> renderers, List<float> delays)
        {
            for (int stem = 0; stem < 9; stem++)
            {
                float angle = stem * 2.399f + .3f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                float height = 1.75f + (stem % 4) * .18f;
                Vector3 a = radial * .10f;
                Vector3 b = radial * .18f + Vector3.up * .55f;
                Vector3 c = radial * .46f + Vector3.up * height;
                Vector3 d = radial * .78f + Vector3.up * (height - .25f);
                var vine = Render(parent, "DreamStem" + stem, Store("DreamStem" + stem, Tube(a, b, c, d, .012f)), dreamMaterial);
                renderers.Add(vine.GetComponent<Renderer>());
                delays.Add(stem * .035f);
                for (int node = 0; node < 3; node++)
                {
                    float t = .29f + node * .23f;
                    Vector3 at = Bezier(a, b, c, d, t);
                    Vector3 direction = radial + Vector3.up * (.78f - node * .57f);
                    direction = Quaternion.AngleAxis(node % 2 == 0 ? 24 : -28, Vector3.up) * direction;
                    var blade = Render(parent, "DreamRibbon" + stem + "_" + node, dreamLeaf, dreamMaterial);
                    blade.localPosition = at;
                    blade.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                    blade.localScale = new Vector3(.85f + node * .16f, 1, .82f + node * .22f);
                    renderers.Add(blade.GetComponent<Renderer>());
                    delays.Add(.08f + stem * .035f + node * .07f);
                }
            }
        }

        /// <summary>替换房间陶盆与表土的网格，不移动原盆栽、台座或交互碰撞。</summary>
        private static void ReplaceRoomPot(Transform visual)
        {
            var pot = visual.Find("Pot");
            if (pot == null) return;
            pot.GetComponent<MeshFilter>().sharedMesh = Store("PotProfile", PotMesh());
            pot.GetComponent<Renderer>().sharedMaterial = potMaterial;
            pot.localScale = new Vector3(.58f, .40f, .58f);
            var soil = visual.Find("HeroSoil");
            if (soil == null)
            {
                soil = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
                soil.name = "HeroSoil";
                soil.SetParent(visual, false);
                Object.DestroyImmediate(soil.GetComponent<Collider>());
            }
            soil.localPosition = new Vector3(0, .866f, 0);
            soil.localScale = new Vector3(.495f, .008f, .495f);
            soil.GetComponent<Renderer>().sharedMaterial = soilMaterial;
        }

        /// <summary>近景使用同一植物语言；保留背面的新芽热点和所有检视状态配置。</summary>
        private static void UpdateInspectionPlant()
        {
            const string path = "Assets/_Game/Content/Prefabs/Inspectables/PF_InspectPlant.prefab";
            var node = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var plant = node.transform.Find("Plant");
                var pot = plant.Find("Pot");
                pot.GetComponent<MeshFilter>().sharedMesh = Store("PotProfile", PotMesh());
                pot.GetComponent<Renderer>().sharedMaterial = potMaterial;
                pot.localScale = new Vector3(.94f, .59f, .94f);
                plant.Find("Soil").GetComponent<Renderer>().sharedMaterial = soilMaterial;
                foreach (Transform child in plant)
                    if (child.name == "Stem" || child.name.StartsWith("Leaf")) child.gameObject.SetActive(false);
                var foliage = Recreate(plant, "DetailedFoliage");
                foliage.localPosition = new Vector3(0, -.20f, 0);
                foliage.localScale = Vector3.one * 1.12f;
                BuildBase(foliage, new List<Renderer>(), 1);
                foreach (var name in new[] { "NewLeafLeft", "NewLeafRight" })
                {
                    var sproutLeaf = plant.Find(name);
                    sproutLeaf.GetComponent<MeshFilter>().sharedMesh = leaf;
                    sproutLeaf.GetComponent<Renderer>().sharedMaterial = matureLeaf;
                    sproutLeaf.localRotation = Quaternion.Euler(-20, name.EndsWith("Left") ? -115 : 70, 0);
                    sproutLeaf.localScale = new Vector3(.18f, .12f, .16f);
                }
                PrefabUtility.SaveAsPrefabAsset(node, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(node); }
        }

        /// <summary>创建稀疏绿金微粒或少量水滴；粒子数量与寿命保持单房间预算。</summary>
        private static ParticleSystem Particles(Transform parent, string name, bool water)
        {
            var node = new GameObject(name);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = new Vector3(0, water ? 1.4f : .65f, 0);
            var system = node.AddComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 7;
            main.maxParticles = water ? 24 : 64;
            main.startLifetime = water ? 1.15f : 2.4f;
            main.startSpeed = water ? .03f : .08f;
            main.startSize = water ? .018f : .012f;
            main.gravityModifier = water ? .22f : -.007f;
            main.startColor = water ? new Color(.63f, .87f, .79f, .40f) : new Color(.76f, .78f, .31f, .66f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission;
            emission.rateOverTime = water ? 6 : 14;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = water ? .55f : .34f;
            var noise = system.noise;
            noise.enabled = !water;
            noise.strength = .06f;
            noise.frequency = .8f;
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.7f, .15f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = water ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = water ? .25f : 1;
            renderer.velocityScale = water ? .02f : 0;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        /// <summary>重建工具专属的纯视觉节点；不会对交互节点调用删除。</summary>
        private static Transform Recreate(Transform parent, string name)
        {
            var found = parent.Find(name);
            if (found != null) Object.DestroyImmediate(found.gameObject);
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            return node;
        }

        /// <summary>创建无 Collider 的网格节点，植物叶片不独立进入玩法交互。</summary>
        private static Transform Render(Transform parent, string name, Mesh mesh, Material material)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return node;
        }

        /// <summary>创建或更新专用植物材质，所有动态差异通过属性块控制。</summary>
        private static Material PlantMaterial(string name, Color color, Color tip, Color glow, bool isLeaf, float wetness)
        {
            string path = Materials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Game/ArtistPlant"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find("Game/ArtistPlant");
            material.SetColor("_BaseColor", color);
            material.SetColor("_TipColor", tip);
            material.SetColor("_EmissionColor", glow);
            material.SetFloat("_LeafMode", isLeaf ? 1 : 0);
            material.SetFloat("_Smoothness", wetness);
            material.SetFloat("_Reveal", 1);
            material.SetFloat("_Unfurl", 1);
            material.SetFloat("_Fade", 1);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>陶盆和土壤采用少量统一 Lit 材质，保持暖色手作质感。</summary>
        private static Material Lit(string name, Color color, float smoothness)
        {
            string path = Materials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>生成原创的软圆点纹理，避免三角碎片或大规模掉落爆炸的观感。</summary>
        private static Material ParticleMaterial()
        {
            const string texturePath = "Assets/_Game/Content/Art/Models/ArtistPlant/SoftMote.png";
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1 - distance), 2.3f);
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            texture.Apply();
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            string path = Materials + "M_PlantMote.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>保存共享网格，保留资产引用并同步重建 GPU 顶点缓冲。</summary>
        private static Mesh Store(string name, Mesh source)
        {
            source.name = name;
            source.RecalculateNormals();
            source.RecalculateBounds();
            string path = Models + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (asset == null) AssetDatabase.CreateAsset(source, path);
            else
            {
                // CopySerialized 只复制序列化数据，旧的空网格可能仍持有空 GPU 缓冲。
                // 通过网格 API 更新，让已经绑定叶片资产的所有 Renderer 同时获得新几何。
                asset.Clear();
                asset.vertices = source.vertices;
                asset.uv = source.uv;
                asset.triangles = source.triangles;
                asset.RecalculateNormals();
                asset.RecalculateBounds();
                asset.UploadMeshData(false);
                Object.DestroyImmediate(source);
                EditorUtility.SetDirty(asset);
            }
            return asset != null ? asset : source;
        }

        /// <summary>制作连续弯曲的叶面，宽度和叶尖决定普通大叶与梦境带状叶的不同轮廓。</summary>
        private static Mesh LeafMesh(bool ribbon)
        {
            const int rows = 20, columns = 6;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int row = 0; row <= rows; row++)
            {
                float t = row / (float)rows;
                // 浮点的 sin(π) 可能略小于零；非整数幂会产生 NaN，Unity 会拒绝整张叶片网格。
                float width = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), ribbon ? .9f : .64f) * (ribbon ? .105f : .34f);
                width *= 1 + Mathf.Sin(t * (ribbon ? 23 : 34)) * (ribbon ? .24f : .085f);
                for (int column = 0; column <= columns; column++)
                {
                    float x = column / (float)columns * 2 - 1;
                    float bend = Mathf.Sin(t * Mathf.PI) * (ribbon ? .13f : .07f) - t * t * (ribbon ? .24f : .18f);
                    float cup = x * x * Mathf.Sin(t * Mathf.PI) * (ribbon ? .026f : .048f);
                    vertices.Add(new Vector3(x * width + (ribbon ? Mathf.Sin(t * 8) * .055f : 0), bend + cup, t));
                    uv.Add(new Vector2((x + 1) * .5f, t));
                    if (row < rows && column < columns)
                    {
                        int a = row * (columns + 1) + column;
                        int b = a + columns + 1;
                        triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                    }
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        /// <summary>曲枝沿贝塞尔中心线形成圆滑细管，UV 纵轴用于从根到尖逐步显现。</summary>
        private static Mesh Tube(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float radius)
        {
            const int rows = 28, sides = 9;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int row = 0; row <= rows; row++)
            {
                float t = row / (float)rows;
                Vector3 center = Bezier(a, b, c, d, t);
                Vector3 tangent = (Bezier(a, b, c, d, Mathf.Min(1, t + .015f)) - Bezier(a, b, c, d, Mathf.Max(0, t - .015f))).normalized;
                Vector3 right = Vector3.Cross(tangent, Vector3.forward).normalized;
                Vector3 up = Vector3.Cross(right, tangent).normalized;
                for (int side = 0; side <= sides; side++)
                {
                    float angle = side / (float)sides * Mathf.PI * 2;
                    vertices.Add(center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius * Mathf.Lerp(1, .23f, t));
                    uv.Add(new Vector2(side / (float)sides, t));
                    if (row < rows && side < sides)
                    {
                        int index = row * (sides + 1) + side;
                        int next = index + sides + 1;
                        triangles.AddRange(new[] { index, index + 1, next, index + 1, next + 1, next });
                    }
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        /// <summary>陶盆有内壁、厚唇与略不规则的轮廓，避免只是一根无厚度圆柱。</summary>
        private static Mesh PotMesh()
        {
            Vector2[] profile = { new Vector2(.31f, -.50f), new Vector2(.34f, -.47f), new Vector2(.45f, .31f), new Vector2(.48f, .35f), new Vector2(.49f, .46f), new Vector2(.48f, .50f), new Vector2(.425f, .50f), new Vector2(.414f, .38f), new Vector2(.32f, -.42f) };
            const int sides = 64;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int row = 0; row < profile.Length; row++)
                for (int side = 0; side <= sides; side++)
                {
                    float angle = side / (float)sides * Mathf.PI * 2;
                    float handmade = 1 + Mathf.Sin(angle * 5) * .006f;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * profile[row].x * handmade, profile[row].y, Mathf.Sin(angle) * profile[row].x * handmade));
                    uv.Add(new Vector2(side / (float)sides, row / (float)(profile.Length - 1)));
                    if (row < profile.Length - 1 && side < sides)
                    {
                        int a = row * (sides + 1) + side;
                        int b = a + sides + 1;
                        triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                    }
                }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        /// <summary>显式的三次曲线取样，服务当前株形而非运行时规则。</summary>
        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1 - t;
            return u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
        }
    }
}
