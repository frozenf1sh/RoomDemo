// =============================================================================
// 文件：WorldArtSetup.cs
// 职责：增量装配第五阶段星光溶解、树林、风动、粒子篝火、银河与空气灰尘。
// 边界：仅修改正式场景的表现资产，不重建房间，不修改领域数据和实体身份。
// 用法：先完成室内表面与窗框的美术，再执行 Apply；方法可重复运行，不累积副本。
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Unity;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor.VisualSprint
{
    /// <summary>这一个样片的具体美术安装器，不提供通用关卡或视觉脚本语言。</summary>
    public static class WorldArtSetup
    {
        private const string MaterialFolder = "Assets/_Game/Content/Art/Materials/VisualSprint";
        private const string MeshFolder = "Assets/_Game/Content/Art/WorldExterior/VisualSprint";
        private const string FirePrefabPath = "Assets/_Game/Content/Prefabs/World/PF_Campfire.prefab";
        private const string NatureFolder = "Assets/ThirdParty/Kenney/NatureKit/";

        /// <summary>依次更新表面、自然外景、火与导演绑定，保留室内人工构图。</summary>
        [MenuItem("游戏/第五阶段/应用星空世界美术")]
        public static void Apply()
        {
            var root = PresentationArtPass.Room();
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(MeshFolder);
            AssetDatabase.Refresh();
            var room = root.GetComponentInChildren<RoomModules>(true);
            var exterior = root.GetComponentInChildren<WorldExterior>(true);
            var sequence = root.GetComponentInChildren<StarryWorldSequence>(true);
            if (room == null || exterior == null || sequence == null)
                throw new InvalidOperationException("请先保留第四阶段房间模块、外景和星空导演。");

            var vfxRoot = PresentationArtPass.Ensure(root.Find("Presentation"), "VFX");
            var worldFx = PresentationArtPass.Ensure(vfxRoot, "StarryBoundaryFX");
            ClearChildren(worldFx);
            var roof = InstallBoundary(room.Ceiling, worldFx, "CeilingStars", 46);
            var back = InstallBoundary(room.WallBack, worldFx, "BackWallStars", 42);
            var left = InstallBoundary(room.WallLeft, worldFx, "LeftWallStars", 28);
            var right = InstallBoundary(room.WallRight, worldFx, "RightWallStars", 28);
            var window = InstallBoundary(room.WindowBoundary, worldFx, "WindowStars", 20);
            var nature = DressExterior(exterior);
            var fire = RebuildCampfire();
            exterior.Configure(exterior.Environment, exterior.FloorPatch, exterior.SkyDome, fire, exterior.CampfireAnchor);
            sequence.ConfigureArt(roof, back, left, right, window, nature);
            UpdateSky(exterior);
            InstallDust(vfxRoot);
            SetNightProfile();
            PresentationArtPass.Save(root);
            Debug.Log("星空美术已更新：边界溶解、风动树林、银河月轮、粒子篝火与室内灰尘。");
        }

        private static WorldBoundaryDissolve InstallBoundary(Transform group, Transform vfxRoot, string name, float rate)
        {
            // 开发标签仍使用字体自己的透明材质，不能参与建筑表面的溶解替换。
            foreach (var label in group.GetComponentsInChildren<TextMesh>(true))
                label.GetComponent<Renderer>().sharedMaterial = label.font.material;
            var renderers = group.GetComponentsInChildren<Renderer>(true)
                .Where(x => x.GetComponent<TextMesh>() == null && !(x is ParticleSystemRenderer)).ToArray();
            if (name == "WindowStars")
            {
                // 海与远方的背景保留专用 Shader，窗边界只借用实体窗框和帘布的表面。
                renderers = renderers.Where(x => x.sharedMaterials.All(m => m != null &&
                    !m.shader.name.Contains("Sea") && !m.shader.name.Contains("StarrySky")) &&
                    !x.name.Contains("Background") && !x.name.Contains("Horizon") &&
                    !x.name.Contains("Glass")).ToArray();
            }
            foreach (var renderer in renderers)
            {
                // 复制表现参数到专用材质资产；不运行时复制材质，不丢弃原有贴图色调。
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null) continue;
                    if (source.shader.name == "Game/WorldBoundary") continue;
                    string assetName = Sanitize(source.name) + "_StarBoundary";
                    var sourceColor = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                        : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                    var material = Material(assetName, "Game/WorldBoundary", sourceColor);
                    if (source.HasProperty("_BaseMap"))
                    {
                        material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                        material.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                        material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
                    }
                    material.SetFloat("_NoiseScale", name == "CeilingStars" ? 1.8f : 1.2f);
                    material.SetFloat("_Dissolve", 0);
                    material.SetFloat("_StarExposure", 0);
                    material.SetColor("_EdgeColor", new Color(1.25f, 1.62f, 2.0f));
                    EditorUtility.SetDirty(material);
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
            }
            var bounds = BoundsOf(renderers);
            var particles = Particle(name, vfxRoot, 0, .025f, 1.8f,
                new Color(.50f, .74f, 1.15f, .65f), ParticleMaterial("M_StarMote", 0));
            particles.transform.position = bounds.center;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = Vector3.Max(bounds.size, Vector3.one * .08f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(.10f, .28f);
            velocity.x = new ParticleSystem.MinMaxCurve(-.06f, .06f);
            // Unity 要求三个轴采用相同曲线模式；零幅度仍须明确使用双常量。
            velocity.z = new ParticleSystem.MinMaxCurve(-.04f, .04f);
            var system = group.GetComponent<WorldBoundaryDissolve>();
            if (system == null) system = group.gameObject.AddComponent<WorldBoundaryDissolve>();
            system.Configure(renderers, new[] { particles }, rate);
            return system;
        }

        private static WorldBoundaryDissolve DressExterior(WorldExterior exterior)
        {
            var landscape = exterior.Environment.transform;
            ClearChildren(landscape);
            var groundMaterial = Material("M_PaintedGround", "Game/PainterlyWind", new Color(.25f, .32f, .22f));
            groundMaterial.SetFloat("_WindStrength", 0);
            groundMaterial.SetFloat("_BrushStrength", .7f);
            var ground = new GameObject("PaintedForestGround");
            ground.transform.SetParent(landscape, false);
            ground.AddComponent<MeshFilter>().sharedMesh = TerrainMesh();
            ground.AddComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            ground.isStatic = true;

            var pines = new[] { "tree_pineTallA", "tree_pineRoundB", "tree_pineRoundC", "tree_pineRoundD", "tree_pineRoundA" };
            var broadleaf = new[] { "tree_detailed", "tree_pineTallA", "tree_pineRoundC", "tree_pineRoundB" };
            var leaves = new[]
            {
                NatureMaterial("M_PineNight", new Color(.18f,.30f,.29f), .27f, 1.5f),
                NatureMaterial("M_PineBlue", new Color(.16f,.25f,.31f), .32f, 1.65f),
                NatureMaterial("M_PineJade", new Color(.21f,.34f,.25f), .26f, 1.7f)
            };
            // 树分成近侧包围、远侧深林和更远的轮廓，中心留给月轮、篝火与主物体。
            for (int i = 0; i < 30; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float x = side * (7.4f + (i % 5) * 2.1f + Mathf.Sin(i * 2.91f) * 1.4f);
                float z = 7.8f + i / 2 * 1.8f + Mathf.Cos(i * 1.73f) * 1.1f;
                if (i >= 22) x = -22 + (i - 22) * 6.2f;
                string model = i % 4 == 0 ? broadleaf[(i / 4) % broadleaf.Length] : pines[i % pines.Length];
                PlaceModel("ForestTree" + i.ToString("00"), landscape, model,
                    new Vector3(x, GroundHeight(x, z), z), 5.4f + (i % 5) * .65f,
                    i * 137.51f, leaves[i % leaves.Length]);
            }
            var rock = NatureMaterial("M_PaintedRock", new Color(.36f, .40f, .42f), 0, -1);
            var rocks = new[] { "rock_largeB", "rock_largeC", "rock_smallA", "rock_smallB", "rock_largeA" };
            for (int i = 0; i < 24; i++)
            {
                float angle = i * 2.39996f;
                float radius = 5.4f + (i % 7) * 1.8f;
                float x = Mathf.Cos(angle) * radius;
                float z = 7.7f + Mathf.Sin(angle) * radius * .6f;
                // 留出从房间到篝火的视线，避免近处石块挡住主画面中心。
                if (Mathf.Abs(x) < 2.8f && z < 7.8f) continue;
                PlaceModel("ForestStone" + i.ToString("00"), landscape, rocks[i % rocks.Length],
                    new Vector3(x, GroundHeight(x, z), z), .28f + (i % 4) * .18f, i * 79, rock);
            }
            var grassMaterials = new[]
            {
                NatureMaterial("M_GrassMoon", new Color(.31f,.40f,.27f), .38f, -1),
                NatureMaterial("M_GrassGold", new Color(.41f,.39f,.21f), .42f, -1),
                NatureMaterial("M_GrassJade", new Color(.21f,.36f,.30f), .40f, -1)
            };
            var grasses = new[] { "grass", "grass_large", "grass_leafs", "grass_leafsLarge", "plant_bushSmall" };
            for (int i = 0; i < 156; i++)
            {
                float angle = i * 2.39996f;
                float radius = 2.1f + Mathf.Sqrt(i / 156f) * 17;
                float x = Mathf.Cos(angle) * radius;
                float z = 8.4f + Mathf.Sin(angle) * radius * .68f;
                // 木台和篝火保持稀疏，连续草地从台外开始，避免草穿过保留家具。
                if (Mathf.Abs(x) < 3.8f && z < 3.0f) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(.35f, 5.7f)) < 1.15f) continue;
                PlaceModel("Meadow" + i.ToString("000"), landscape, grasses[i % grasses.Length],
                    new Vector3(x, GroundHeight(x, z), z), .22f + i % 5 * .065f,
                    i * 137.51f, grassMaterials[i % grassMaterials.Length]);
            }
            var flowers = NatureMaterial("M_MoonFlowers", new Color(.49f, .48f, .68f), .29f, -1);
            for (int i = 0; i < 14; i++)
            {
                float x = -6.6f + i * .94f;
                float z = 4.2f + Mathf.Sin(i * 2.7f) * 1.3f;
                PlaceModel("MoonFlower" + i, landscape, i % 2 == 0 ? "flower_redA" : "flower_yellowA", new Vector3(x, GroundHeight(x, z), z),
                    .23f + (i % 3) * .05f, i * 41, flowers);
            }
            var wood = NatureMaterial("M_ForestWood", new Color(.31f, .21f, .12f), 0, -1);
            PlaceModel("FallenLog", landscape, "log", new Vector3(-4.2f, 0, 6.1f), .55f, 64, wood);
            var dissolve = landscape.GetComponent<WorldBoundaryDissolve>();
            if (dissolve == null) dissolve = landscape.gameObject.AddComponent<WorldBoundaryDissolve>();
            dissolve.Configure(landscape.GetComponentsInChildren<Renderer>(true), new ParticleSystem[0], 0);
            landscape.gameObject.SetActive(false);
            return dissolve;
        }

        private static void UpdateSky(WorldExterior exterior)
        {
            var sky = exterior.SkyDome.GetComponentInChildren<Renderer>(true);
            var material = Material("M_PainterlyNightSky", "Game/StarrySky", Color.white);
            material.SetColor("_Zenith", new Color(.015f, .025f, .068f));
            material.SetColor("_Horizon", new Color(.09f, .15f, .21f));
            material.SetColor("_CloudColor", new Color(.22f, .33f, .48f));
            material.SetFloat("_Stars", 1.55f);
            material.SetFloat("_Nebula", 1.35f);
            material.SetFloat("_Moon", 1.1f);
            sky.sharedMaterial = material;
            EditorUtility.SetDirty(material);
            exterior.SkyDome.SetActive(false);
        }

        private static GameObject RebuildCampfire()
        {
            var root = new GameObject("PF_Campfire");
            var logMaterial = NatureMaterial("M_CharredWood", new Color(.21f, .13f, .065f), 0, -1);
            var stoneMaterial = NatureMaterial("M_FireStones", new Color(.31f, .32f, .35f), 0, -1);
            PlaceModel("WoodLogs", root.transform, "campfire_logs", Vector3.zero, .20f, 0, logMaterial);
            PlaceModel("StoneCircle", root.transform, "campfire_stones", new Vector3(0, -.01f, 0), .19f, 0, stoneMaterial);
            var fire = Particle("Flame", root.transform, 44, .41f, .95f,
                new Color(2.0f, .77f, .16f, .78f), ParticleMaterial("M_ParticleFlame", 1));
            fire.transform.localPosition = Vector3.up * .24f;
            var fireMain = fire.main;
            fireMain.startSpeed = new ParticleSystem.MinMaxCurve(.65f, 1.0f);
            fireMain.startSize = new ParticleSystem.MinMaxCurve(.33f, .58f);
            fireMain.startRotation = new ParticleSystem.MinMaxCurve(-.25f, .25f);
            fireMain.startLifetime = new ParticleSystem.MinMaxCurve(.75f, 1.15f);
            var fireShape = fire.shape;
            fireShape.shapeType = ParticleSystemShapeType.Cone;
            fireShape.radius = .29f;
            fireShape.angle = 6;
            fire.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var fireRenderer = fire.GetComponent<ParticleSystemRenderer>();
            fireRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            fireRenderer.lengthScale = 1.8f;
            fireRenderer.velocityScale = .2f;
            var fireColor = fire.colorOverLifetime;
            fireColor.enabled = true;
            fireColor.color = ColorGradient(new Color(2.7f, 1.45f, .37f), new Color(1.5f, .29f, .06f));

            var ember = Particle("Embers", root.transform, 10, .025f, 2.2f,
                new Color(2.2f, 1.05f, .23f, .85f), ParticleMaterial("M_ParticleEmber", 0));
            ember.transform.localPosition = Vector3.up * .3f;
            ember.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var emberMain = ember.main;
            emberMain.startSpeed = new ParticleSystem.MinMaxCurve(.7f, 1.4f);
            emberMain.gravityModifier = -.015f;
            var emberShape = ember.shape;
            emberShape.shapeType = ParticleSystemShapeType.Cone;
            emberShape.radius = .23f;
            emberShape.angle = 13;
            var smoke = Particle("Smoke", root.transform, 7, .34f, 3.0f,
                new Color(.34f, .39f, .43f, .14f), ParticleMaterial("M_ParticleSmoke", 2));
            smoke.transform.localPosition = Vector3.up * .75f;
            smoke.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var smokeMain = smoke.main;
            smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(.32f, .53f);
            var smokeShape = smoke.shape;
            smokeShape.shapeType = ParticleSystemShapeType.Cone;
            smokeShape.radius = .19f;
            smokeShape.angle = 9;
            var smokeSize = smoke.sizeOverLifetime;
            smokeSize.enabled = true;
            smokeSize.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .38f, 1, 2.1f));
            var smokeNoise = smoke.noise;
            smokeNoise.enabled = true;
            smokeNoise.strength = .17f;
            smokeNoise.frequency = .26f;
            smokeNoise.scrollSpeed = .10f;

            var glow = new GameObject("FireGlow").AddComponent<Light>();
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = Vector3.up * .75f;
            glow.type = LightType.Point;
            glow.intensity = 5.2f;
            glow.range = 14;
            glow.color = new Color(1, .52f, .18f);
            glow.shadows = LightShadows.None;
            var sound = AudioArtPass.Source(root.transform, "Fire", true, .34f);
            sound.spatialBlend = 1;
            sound.minDistance = 1;
            sound.maxDistance = 22;
            sound.dopplerLevel = 0;
            root.AddComponent<CampfireVisual>().ConfigureParticles(new[] { fire }, new[] { ember }, new[] { smoke }, glow, sound);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, FirePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void InstallDust(Transform parent)
        {
            var old = parent.Find("RoomDust");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var dust = Particle("RoomDust", parent, 3.5f, .012f, 12,
                new Color(.91f, .78f, .55f, .13f), ParticleMaterial("M_RoomDust", 0));
            dust.transform.localPosition = new Vector3(0, 2.1f, 0);
            var main = dust.main;
            main.playOnAwake = true;
            main.startSpeed = new ParticleSystem.MinMaxCurve(.008f, .025f);
            main.maxParticles = 65;
            main.prewarm = true;
            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6.5f, 2.4f, 5.4f);
            var velocity = dust.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = .007f;
            velocity.y = -.009f;
        }

        private static ParticleSystem Particle(string name, Transform parent, float rate, float size, float lifetime, Color color, Material material)
        {
            var node = new GameObject(name);
            node.transform.SetParent(parent, false);
            var particles = node.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = lifetime;
            main.startSize = size;
            main.startColor = color;
            main.startSpeed = .02f;
            main.maxParticles = 120;
            main.useUnscaledTime = true;
            particles.useAutoRandomSeed = false;
            particles.randomSeed = 571;
            var emission = particles.emission;
            emission.rateOverTime = rate;
            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1,
                new AnimationCurve(new Keyframe(0, .2f), new Keyframe(.15f, 1), new Keyframe(1, .01f)));
            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = ColorGradient(Color.white, Color.white);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static Gradient ColorGradient(Color from, Color to)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(from, 0), new GradientColorKey(to, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(0, 1) });
            return gradient;
        }

        private static Material ParticleMaterial(string name, float mode)
        {
            var material = Material(name, "Game/AtmosphereParticle", Color.white);
            material.SetFloat("_Mode", mode);
            material.SetFloat("_Softness", mode > 1.5f ? 1.6f : 1.2f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material NatureMaterial(string name, Color color, float wind, float trunk)
        {
            var material = Material(name, "Game/PainterlyWind", color);
            material.SetFloat("_WindStrength", wind);
            material.SetFloat("_TrunkHeight", trunk);
            material.SetFloat("_BrushStrength", .34f);
            material.SetFloat("_Dissolve", 0);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Material(string name, string shaderName, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("请先完成编译：专用 Shader 尚未导入：" + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Tint")) material.SetColor("_Tint", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void PlaceModel(string name, Transform parent, string modelName, Vector3 position, float height, float yaw, Material material)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(NatureFolder + modelName + ".fbx");
            if (model == null && modelName.StartsWith("tree_"))
                model = AssetDatabase.LoadAssetAtPath<GameObject>(NatureFolder + "tree_pineRoundA.fbx");
            if (model == null) return;
            var wrapper = new GameObject(name).transform;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(wrapper, false);
            var renderers = instance.GetComponentsInChildren<Renderer>();
            var bounds = BoundsOf(renderers);
            float scale = height / Mathf.Max(bounds.size.y, .001f);
            instance.transform.localScale *= scale;
            instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
                renderer.receiveShadows = true;
            }
            foreach (var collider in instance.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            wrapper.SetParent(parent, false);
            wrapper.localPosition = position;
            wrapper.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        private static Mesh TerrainMesh()
        {
            const string path = MeshFolder + "/PaintedForestGround.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "夜色草地缓坡" };
                AssetDatabase.CreateAsset(mesh, path);
            }
            const int columns = 32;
            const int rows = 40;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var indices = new List<int>(columns * rows * 6);
            for (int row = 0; row <= rows; row++)
            {
                for (int column = 0; column <= columns; column++)
                {
                    float x = Mathf.Lerp(-34, 34, column / (float)columns);
                    float z = Mathf.Lerp(-20, 65, row / (float)rows);
                    int index = row * (columns + 1) + column;
                    vertices[index] = new Vector3(x, GroundHeight(x, z), z);
                    uv[index] = new Vector2(x * .2f, z * .2f);
                    if (column == columns || row == rows) continue;
                    indices.AddRange(new[] { index, index + columns + 1, index + 1,
                        index + 1, index + columns + 1, index + columns + 2 });
                }
            }
            // 地形仅在编辑器制作一次固定艺术网格，运行时没有世界生成或地形规则。
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = indices.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static float GroundHeight(float x, float z)
        {
            float distance = Mathf.Max(Mathf.Abs(x) - 4.3f, Mathf.Abs(z + .5f) - 4.4f);
            float slope = Mathf.SmoothStep(0, 1, Mathf.Clamp01(distance / 12));
            return -.075f + (Mathf.PerlinNoise(x * .075f + 17, z * .08f + 29) - .25f) * 1.8f * slope;
        }

        private static void SetNightProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VisualThemeProfile>("Assets/_Game/Content/Art/WorldExterior/Themes/StarryWorld.asset");
            profile.Ambient = new Color(.17f, .21f, .32f);
            profile.Fog = new Color(.06f, .105f, .17f);
            profile.FogDensity = .017f;
            profile.Sun = new Color(.53f, .65f, .94f);
            profile.SunIntensity = 1.03f;
            profile.SunEuler = new Vector3(42, -24, 0);
            profile.Fill = new Color(1, .57f, .28f);
            profile.FillIntensity = .65f;
            profile.WindowIntensity = .12f;
            profile.SeaIntensity = 0;
            EditorUtility.SetDirty(profile);
        }

        private static Bounds BoundsOf(Renderer[] renderers)
        {
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static string Sanitize(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return name.Replace(" (Instance)", "");
        }

        private static void ClearChildren(Transform parent)
        {
            foreach (var child in parent.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
        }
    }
}
