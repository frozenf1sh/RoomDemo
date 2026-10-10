// 文件：AtelierFinishing.cs
// 职责：制作原创程序绘制习作，补齐室内承托细节，并准备独立场景的间接光烘焙。
// 架构边界：习作来自本文件的几何/笔触算法，不读取概念图；不修改既有模型和管线资源。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Editor.ArtRebuild
{
    /// <summary>面向运行预览反馈的细化工具。</summary>
    public static class AtelierFinishing
    {
        private const string Art = "Assets/_Game/Content/ArtRebuild/";

        /// <summary>补上原创画作、物件承托与更柔和的桌面光，保持布局可编辑。</summary>
        public static void Apply()
        {
            var root = GameObject.Find("独立夜间画室").transform;
            var props = root.Find("桌面与创作痕迹");
            var wood = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/深暖旧木.mat");
            wood.SetColor("_BaseColor", new Color(.85f, .76f, .59f));
            wood.SetFloat("_Smoothness", .18f);
            var plaster = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/暖灰灰泥.mat");
            plaster.SetFloat("_BumpScale", .27f);
            var brass = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/旧黄铜.mat");
            brass.SetColor("_BaseColor", new Color(.32f, .26f, .16f));
            brass.SetFloat("_Smoothness", .30f);
            var shade = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/受光亚麻灯罩.mat");
            shade.SetColor("_EmissionColor", new Color(.86f, .39f, .10f));
            shade.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/PolyHaven/RoughLinen/Diffuse.jpg"));
            shade.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            shade.EnableKeyword("_EMISSION");
            var lights = root.Find("照明");
            lights.Find("桌灯向下的暖工作区").GetComponent<Light>().intensity = 2.4f;
            lights.Find("桌灯在画室中的漫反弹").GetComponent<Light>().intensity = .45f;
            var beam = root.Find("建筑与木作/后墙承重横梁");
            beam.localPosition = new Vector3(0, 4.03f, 3.95f);
            beam.GetComponent<MeshFilter>().sharedMesh = AtelierMeshLibrary.Save("后承重梁厚边", AtelierMeshLibrary.Timber(new Vector3(8.65f, .49f, .28f), .025f));
            for (int i = 0; i < 5; i++) props.Find("书柜旧书_" + i).localPosition = new Vector3(3.37f + i * .11f, .885f, 2.44f);

            if (root.Find("原创风景习作") == null)
            {
                var works = AtelierSceneBuilder.Group(root, "原创风景习作");
                var picture = PaintingMaterial("未完成的山谷油彩习作", false);
                var sketch = PaintingMaterial("山谷铅笔构图", true);
                var painting = root.Find("室内家具/细木作画架/绷布与木框");
                AtelierSceneBuilder.Node(painting, "原创程序绘制风景习作", Plane("画架习作面", 1.30f, 1.44f), picture, new Vector3(0, 0, -.030f));
                for (int i = 0; i < 2; i++)
                {
                    var study = AtelierSceneBuilder.Group(works, "左墙参考习作_" + i);
                    study.localPosition = new Vector3(-4.235f, 2.42f + i * .47f, .80f + i * .72f);
                    study.localRotation = Quaternion.Euler(0, -90, i == 0 ? -3 : 4);
                    Frame(study, .64f, .46f, wood, i == 0 ? picture : sketch);
                }
                var small = AtelierSceneBuilder.Group(works, "背墙构图小稿");
                small.localPosition = new Vector3(-1.00f, 3.03f, 3.98f);
                small.localRotation = Quaternion.Euler(0, 0, -4);
                Frame(small, .62f, .43f, wood, sketch);
                foreach (var sheet in props.GetComponentsInChildren<MeshRenderer>().Where(x => x.name.StartsWith("微翘草稿纸")))
                {
                    sheet.sharedMaterial = sketch;
                }
            }
            if (lights.Find("桌灯烘焙柔光") == null)
            {
                var area = new GameObject("桌灯烘焙柔光", typeof(Light)).GetComponent<Light>();
                area.transform.SetParent(lights);
                area.transform.position = new Vector3(1.34f, 1.53f, -.56f);
                area.transform.rotation = Quaternion.Euler(90, 0, 0);
                area.type = LightType.Rectangle;
                area.lightmapBakeType = LightmapBakeType.Baked;
                area.areaSize = new Vector2(.34f, .30f);
                area.color = new Color(1, .66f, .35f);
                area.intensity = 3.5f;
                area.range = 5;
            }
            if (root.Find("室内反射探针") == null)
            {
                var probe = new GameObject("室内反射探针", typeof(ReflectionProbe)).GetComponent<ReflectionProbe>();
                probe.transform.SetParent(root);
                probe.transform.position = new Vector3(0, 2.05f, .05f);
                probe.size = new Vector3(8.6f, 4.1f, 8.5f);
                probe.boxProjection = true;
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                probe.resolution = 256;
                probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
                probe.backgroundColor = new Color(.045f, .065f, .10f);
                probe.nearClipPlane = .08f;
                probe.farClipPlane = 35;
            }
            AtelierSceneBuilder.Save();
        }

        /// <summary>仅为室内制作 UV2 副本并准备烘焙；原扫描网格和导入设置保持原样。</summary>
        public static void PrepareBake()
        {
            var root = GameObject.Find("独立夜间画室").transform;
            var copies = new Dictionary<Mesh, Mesh>();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.transform.IsChildOf(root.Find("窗外山谷"))) continue;
                // 天空始终实时；只烘焙明确室内表面，避免天空球进入光照网格。
                if (renderer.transform.IsChildOf(root.Find("持续流动的程序化星夜"))) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Mesh mesh = filter.sharedMesh;
                if (!copies.TryGetValue(mesh, out var owned))
                {
                    string path = AssetDatabase.GetAssetPath(mesh);
                    if (path.StartsWith(Art + "Meshes/")) owned = mesh;
                    else
                    {
                        string name = "室内光照副本_" + mesh.name + "_" + mesh.vertexCount;
                        owned = AtelierMeshLibrary.Save(name, Object.Instantiate(mesh));
                    }
                    if (owned.uv2 == null || owned.uv2.Length != owned.vertexCount)
                    {
                        Unwrapping.GenerateSecondaryUVSet(owned);
                        EditorUtility.SetDirty(owned);
                    }
                    copies[mesh] = owned;
                }
                filter.sharedMesh = owned;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
                renderer.receiveGI = ReceiveGI.Lightmaps;
                renderer.scaleInLightmap = renderer.name.Contains("灯罩") ? .6f : 1;
            }
            foreach (var light in root.Find("照明").GetComponentsInChildren<Light>())
                if (light.type != LightType.Rectangle) light.lightmapBakeType = LightmapBakeType.Mixed;
            string settingsPath = Art + "画室光照设置.lighting";
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(settingsPath);
            if (settings == null)
            {
                settings = new LightingSettings { name = "画室光照设置" };
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.UnityComputeGPU;
            settings.lightmapResolution = 16;
            settings.lightmapMaxSize = 1024;
            settings.lightmapPadding = 4;
            settings.directSampleCount = 64;
            settings.indirectSampleCount = 128;
            settings.environmentSampleCount = 128;
            settings.maxBounces = 3;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            Lightmapping.lightingSettings = settings;
            EditorUtility.SetDirty(settings);
            AtelierSceneBuilder.Save();
            Debug.Log("独立画室：已准备室内专属 UV2、混合光与柔光烘焙；没有修改外部扫描网格。");
        }

        /// <summary>制作带厚框的墙上小习作。</summary>
        private static void Frame(Transform root, float width, float height, Material wood, Material paint)
        {
            AtelierSceneBuilder.Node(root, "习作纸面", Plane("习作面_" + width + "_" + height, width, height), paint, new Vector3(0, 0, -.008f));
            foreach (float x in new[] { -width * .5f, width * .5f }) AtelierSceneBuilder.Beam(root, "旧木竖框", new Vector3(x, 0, 0), new Vector3(.035f, height + .04f, .05f), wood, .007f);
            foreach (float y in new[] { -height * .5f, height * .5f }) AtelierSceneBuilder.Beam(root, "旧木横框", new Vector3(0, y, 0), new Vector3(width, .035f, .05f), wood, .007f);
        }

        /// <summary>前面朝向负 Z，UV 覆盖完整画面，避免木作 UV 截断绘画。</summary>
        private static Mesh Plane(string name, float width, float height)
        {
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-width / 2, -height / 2, 0), new Vector3(-width / 2, height / 2, 0), new Vector3(width / 2, height / 2, 0), new Vector3(width / 2, -height / 2, 0) },
                uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            mesh.RecalculateNormals();
            return AtelierMeshLibrary.Save(name, mesh);
        }

        /// <summary>从本项目的数学构图绘制习作，无概念图采样、裁切或改色。</summary>
        private static Material PaintingMaterial(string name, bool pencil)
        {
            const int width = 640, height = 768;
            string path = Art + "Textures/" + name + ".png";
            if (!File.Exists(path))
            {
                var pixels = new Color[width * height];
                var rng = new System.Random(pencil ? 31 : 15);
                Color Field(float u, float v)
                {
                    Color colour = Color.Lerp(new Color(.42f, .50f, .55f), new Color(.13f, .23f, .37f), v);
                    for (int layer = 0; layer < 4; layer++)
                    {
                        float ridge = .55f - layer * .095f + .07f * Mathf.Sin(u * (7 + layer * 3) + layer * 2) + .035f * Mathf.Sin(u * 23 + layer);
                        if (v < ridge) colour = Color.Lerp(new Color(.31f, .40f, .43f), new Color(.13f, .22f, .19f), layer / 3f);
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        float x = .32f + i * .15f, y = .29f + (i % 2) * .10f;
                        if (Mathf.Abs(u - x) < .060f && v > y && v < y + .080f) colour = new Color(.55f, .47f, .32f);
                        if (v >= y + .080f && v < y + .130f && Mathf.Abs(u - x) < (y + .130f - v) * 1.45f) colour = new Color(.23f, .25f, .24f);
                        if (Mathf.Abs(u - x - .022f) < .009f && v > y + .024f && v < y + .057f) colour = new Color(.89f, .65f, .28f);
                    }
                    for (int i = 0; i < 9; i++)
                    {
                        float x = .10f + i * .10f, bottom = .09f + (i % 3) * .035f, top = bottom + .20f + (i % 4) * .027f;
                        float edge = (top - v) * .20f * (1 + .14f * Mathf.Sin(v * 180 + i));
                        if (v > bottom && v < top && Mathf.Abs(u - x) < edge) colour = new Color(.095f, .16f, .14f);
                    }
                    float border = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
                    float grain = Mathf.PerlinNoise(u * 81, v * 51) * .12f + Mathf.PerlinNoise(u * 31, v * 173) * .045f;
                    colour *= .86f + grain;
                    if (pencil)
                    {
                        float value = colour.grayscale;
                        float hatch = Mathf.Pow(Mathf.Max(0, Mathf.Sin(u * 540 + v * 91)), 14);
                        colour = new Color(.74f, .68f, .53f) * (.62f + value * .57f - hatch * .12f);
                    }
                    if (border < .035f + Mathf.PerlinNoise(u * 53, v * 39) * .025f) colour = new Color(.65f, .58f, .43f);
                    return colour;
                }
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) pixels[y * width + x] = Field((float)x / width, (float)y / height);
                // 干笔短划沿各层走势叠加；画面来自同一数学场，没有外部图像输入。
                for (int i = 0; i < 26000; i++)
                {
                    int x = rng.Next(8, width - 8), y = rng.Next(8, height - 8), length = rng.Next(2, pencil ? 8 : 15);
                    Color c = Field((float)x / width, (float)y / height) * (.85f + (float)rng.NextDouble() * .30f);
                    for (int j = -length; j <= length; j++)
                    {
                        int px = x + j, py = y + (int)(Mathf.Sin((float)y / height * 11) * j * .24f);
                        if (px < 1 || px >= width - 1 || py < 1 || py >= height - 1) continue;
                        pixels[py * width + px] = Color.Lerp(pixels[py * width + px], c, .28f);
                        if (!pencil && j % 3 != 0) pixels[(py + 1) * width + px] = Color.Lerp(pixels[(py + 1) * width + px], c, .18f);
                    }
                }
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.SetPixels(pixels); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }
            var material = AtelierSceneBuilder.Lit(name, new Color(.90f, .87f, .79f), AssetDatabase.LoadAssetAtPath<Texture2D>(path), null, .08f);
            material.SetFloat("_Cull", 0);
            return material;
        }

    }
}
