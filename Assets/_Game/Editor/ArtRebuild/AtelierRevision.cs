// 文件：AtelierRevision.cs
// 职责：针对首版画室的资产、落地关系、照明和天空偏差执行增量返工。
// 架构边界：仅操作 AtelierNight 与 ArtRebuild 专属资源，不改变旧场景、原扫描件或领域逻辑。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Unity.ArtRebuild;
using Game.Unity.TechnicalArt.RealtimePainterly.Production;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Game.Editor.ArtRebuild
{
    /// <summary>首版被否决后的独立返工入口；每一组新增内容保持可单独编辑。</summary>
    public static class AtelierRevision
    {
        private const string Art = "Assets/_Game/Content/ArtRebuild/";
        private const string Imports = Art + "Imports/Revision/";
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        /// <summary>配置本轮导入数据；透明遮罩、法线与粗糙度分别使用正确的颜色空间。</summary>
        public static void PrepareImports()
        {
            AssetDatabase.Refresh();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Imports }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.Contains("_nor_");
                bool data = normal || path.Contains("_rough") || path.Contains("_alpha_") || path.Contains("_mask_") || path.Contains("_metallic_");
                importer.maxTextureSize = path.EndsWith(".hdr") ? 2048 : 1024;
                importer.sRGBTexture = !data && !path.EndsWith(".hdr");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.alphaIsTransparency = path.Contains("_cutout");
                importer.isReadable = path.Contains("_rough") || path.Contains("_metallic_");
                importer.SaveAndReimport();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Imports }))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                // 暂留源材质槽名称，用于枝干、叶片、封皮和纸页的精确对应。
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>建立独立 AO 管线、普通 HDRI 与完整开局屋顶，后期星夜仅由明确预览触发。</summary>
        public static void SetupRendering()
        {
            var root = Root();
            const string rendering = Art + "Rendering/";
            Directory.CreateDirectory(rendering);
            AssetDatabase.Refresh();
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendering + "画室渲染器.asset");
            if (renderer == null)
            {
                renderer = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset"));
                renderer.name = "画室渲染器";
                renderer.rendererFeatures.Clear();
                AssetDatabase.CreateAsset(renderer, rendering + "画室渲染器.asset");
                var ao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ao.name = "桌沿与物品接触 AO";
                AssetDatabase.AddObjectToAsset(ao, renderer);
                var settings = new SerializedObject(ao);
                settings.FindProperty("m_Settings.Intensity").floatValue = 1.15f;
                settings.FindProperty("m_Settings.Radius").floatValue = .18f;
                settings.FindProperty("m_Settings.DirectLightingStrength").floatValue = .12f;
                settings.FindProperty("m_Settings.Downsample").boolValue = false;
                settings.FindProperty("m_Settings.Source").intValue = 0;
                settings.FindProperty("m_Settings.NormalSamples").intValue = 2;
                settings.FindProperty("m_Settings.Samples").intValue = 0;
                settings.ApplyModifiedPropertiesWithoutUndo();
                renderer.rendererFeatures.Add(ao);
                renderer.SetDirty();
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rendering + "画室专用管线.asset");
            if (pipeline == null)
            {
                pipeline = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset"));
                pipeline.name = "画室专用管线";
                AssetDatabase.CreateAsset(pipeline, rendering + "画室专用管线.asset");
                var serialized = new SerializedObject(pipeline);
                var list = serialized.FindProperty("m_RendererDataList");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            pipeline.supportsHDR = true;
            pipeline.supportsCameraDepthTexture = true;
            pipeline.shadowDistance = 115;
            // 局部绘画遮罩与相机深度统一为单采样，避免 RenderGraph 附件采样数不匹配。
            pipeline.msaaSampleCount = 1;
            pipeline.maxAdditionalLightsCount = 8;
            EditorUtility.SetDirty(pipeline);
            var hdri = NewMaterial("普通暮色 HDRI", Shader.Find("Skybox/Panoramic"));
            hdri.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Imports + "kloppenheim_06_puresky/kloppenheim_06_puresky_2k.hdr"));
            hdri.SetFloat("_Exposure", .36f);
            hdri.SetColor("_Tint", new Color(.37f, .46f, .63f));
            hdri.SetFloat("_Rotation", 118);
            var architecture = root.Find("建筑与木作");
            var cover = architecture.Find("开局完整屋顶");
            if (cover == null)
                cover = AtelierSceneBuilder.Beam(architecture, "开局完整屋顶", new Vector3(0, 4.31f, 0), new Vector3(8.7f, .17f, 8.7f), LoadMaterial("暖灰灰泥"), .015f);
            var sky = root.GetComponentInChildren<LivingSkyController>(true);
            sky.InitialCoverage = 0;
            sky.InitialLight = 1;
            sky.InitialFlow = 1;
            sky.SeparateLightAndFlow = false;
            sky.Surface.sharedMaterial.shader = Shader.Find("RoomDemo/ArtRebuild/AtelierLivingSky");
            var state = root.GetComponent<AtelierArtState>();
            if (state == null) state = root.gameObject.AddComponent<AtelierArtState>();
            state.ScenePipeline = pipeline;
            state.NormalHdri = hdri;
            state.LivingSky = sky;
            state.ClosedRoof = cover.gameObject;
            state.RevealRim = architecture.Find("屋顶干刷残留").gameObject;
            state.SetLateSky(false);
            QualitySettings.renderPipeline = pipeline;
            RenderSettings.skybox = hdri;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.30f, .32f, .37f);
            RenderSettings.ambientEquatorColor = new Color(.22f, .19f, .15f);
            RenderSettings.ambientGroundColor = new Color(.11f, .085f, .06f);
            var camera = root.GetComponentInChildren<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.transform.SetPositionAndRotation(new Vector3(-.3f, 1.8f, -4.3f), Quaternion.Euler(0, 2.5f, 0));
            camera.fieldOfView = 54;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camera.GetUniversalAdditionalCameraData().antialiasingQuality = AntialiasingQuality.High;
            var volumeRoot = root.Find("克制的曝光与星光晕");
            if (volumeRoot == null) volumeRoot = new GameObject("克制的曝光与星光晕").transform;
            volumeRoot.SetParent(root);
            var volume = volumeRoot.GetComponent<Volume>() ?? volumeRoot.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(rendering + "画室曝光.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, rendering + "画室曝光.asset");
                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.value = 1.3f;
                bloom.intensity.value = .13f;
                bloom.scatter.value = .48f;
                var grade = profile.Add<ColorAdjustments>(true);
                grade.postExposure.value = .25f;
                grade.contrast.value = 5;
                grade.saturation.value = -5;
                var tone = profile.Add<Tonemapping>(true);
                tone.mode.value = TonemappingMode.Neutral;
                foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            }
            volume.sharedProfile = profile;
            AtelierSceneBuilder.Save();
        }

        /// <summary>用正式扫描件替换装饰并形成可信工作簇，不把增加物件数量当成完成。</summary>
        public static void DressInterior()
        {
            var root = Root();
            if (root.Find("返工_扫描工作簇") != null) throw new InvalidOperationException("本轮工作簇已存在，请增量编辑。");
            materials.Clear();
            var group = AtelierSceneBuilder.Group(root, "返工_扫描工作簇");
            var props = root.Find("桌面与创作痕迹");
            var furniture = root.Find("室内家具");
            furniture.Find("有磨损的扫描工作台").position += new Vector3(-.22f, 0, -.18f);
            furniture.Find("有磨损的扫描工作台").localScale = new Vector3(1.40f, 1.07f, 1.32f);
            props.Find("有实体灯罩的黄铜桌灯").gameObject.SetActive(false);
            PlaceOilLamp(group);
            Place("potted_plant_02", "窗台活植物", group, new Vector3(.05f, 1.04f, 3.77f), .57f, 43);
            Place("potted_plant_02", "书柜顶旧陶盆", group, new Vector3(3.56f, 2.81f, 2.57f), .65f, -24);
            Place("potted_plant_02", "左柜上的小植株", group, new Vector3(-3.69f, 2.48f, 3.42f), .55f, 111);
            for (int shelf = 0; shelf < 3; shelf++)
                for (int i = 0; i < 9; i++)
                {
                    var book = Place("book_encyclopedia_set_01", "书架皮面旧卷_" + shelf + "_" + i, group,
                        new Vector3(3.01f + i * .132f, .89f + shelf * .64f, 2.42f), .32f + i % 3 * .025f, 180, (i + shelf * 3) % 12);
                    book.Rotate(0, 0, i == 8 ? -10 : (i % 3 - 1) * 1.7f);
                }
            foreach (var old in props.Cast<Transform>().Where(t => t.name.StartsWith("书柜旧书_")).ToArray()) old.gameObject.SetActive(false);
            for (int i = 0; i < 4; i++)
            {
                var book = Place("book_encyclopedia_set_01", "桌面摊放皮卷_" + i, group, new Vector3(-.25f + i * .025f, 1.054f + i * .055f, -.39f), .29f, i * 7 - 12, i + 2);
                book.localRotation = Quaternion.Euler(0, i * 7 - 12, 90);
                RestOn(book, 1.054f + i * .055f);
            }
            // 重用已有正式扫描便笺，桌面留下纸本与笔刷的重叠关系。
            var source = props.Find("窗台参考纸本");
            var notepad = Object.Instantiate(source.gameObject, group).transform;
            notepad.name = "工作台扫描便笺";
            notepad.position = new Vector3(-.74f, 1.065f, -1.02f);
            notepad.localRotation = Quaternion.Euler(0, -16, 0);
            for (int i = 0; i < 3; i++)
            {
                var brush = Object.Instantiate(props.Find("使用过的画笔_" + i).gameObject, group).transform;
                brush.name = "平放的蘸色画笔_" + i;
                brush.rotation = Quaternion.Euler(78, i * 13 - 23, 14);
                brush.position = new Vector3(.27f + i * .08f, 1.07f, -1.15f);
                RestOn(brush, 1.062f + i * .005f);
            }
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var mat in renderer.sharedMaterials.Where(m => m != null).Distinct())
                {
                    if (!AssetDatabase.GetAssetPath(mat).StartsWith(Art) || mat.shader.name != "Universal Render Pipeline/Lit") continue;
                    if (mat.name.Contains("灯") || mat.name.Contains("叶")) continue;
                    mat.shader = Shader.Find("RoomDemo/Atelier/AgedPigmentLit");
                    mat.SetFloat("_PigmentStrength", mat.name.Contains("灰泥") ? .55f : .22f);
                    mat.SetFloat("_WearStrength", mat.name.Contains("灰泥") ? .36f : .16f);
                    mat.SetFloat("_GrainScale", 21);
                    EditorUtility.SetDirty(mat);
                }
            }
            LoadMaterial("暖灰灰泥").SetColor("_BaseColor", new Color(.96f, .87f, .71f));
            LoadMaterial("深暖旧木").SetColor("_BaseColor", new Color(.90f, .80f, .65f));
            var lights = root.Find("照明");
            lights.Find("桌灯在画室中的漫反弹").GetComponent<Light>().intensity = 5.5f;
            lights.Find("桌灯在画室中的漫反弹").position = new Vector3(1.02f, 1.66f, -.65f);
            lights.Find("桌灯烘焙柔光").GetComponent<Light>().intensity = 48;
            lights.Find("桌灯向下的暖工作区").GetComponent<Light>().intensity = 1.5f;
            lights.Find("窗台与帘褶的蓝色反弹").GetComponent<Light>().color = new Color(.44f, .57f, .82f);
            lights.Find("窗台与帘褶的蓝色反弹").GetComponent<Light>().intensity = 2.6f;
            AtelierSceneBuilder.Save();
        }

        /// <summary>移除首版程序树，用扫描枝叶与石块建立坡面、地基和近景植被的衔接。</summary>
        public static void RebuildGrounding()
        {
            var root = Root().Find("窗外山谷");
            if (root.Find("返工_自然坡地") != null) throw new InvalidOperationException("本轮外景已存在，请增量编辑。");
            materials.Clear();
            var group = AtelierSceneBuilder.Group(root, "返工_自然坡地");
            root.localPosition = Vector3.zero;
            foreach (var parent in new[] { root.Find("近景枝叶与坡地"), root.Find("中景错落小屋") })
                foreach (var tree in parent.Cast<Transform>().Where(t => t.name.StartsWith("窗左") || t.name.StartsWith("窗右") || t.name.StartsWith("山坡树_")).ToArray()) tree.gameObject.SetActive(false);
            var terrain = root.Find("近景枝叶与坡地/连续起伏的山坡");
            terrain.GetComponent<MeshFilter>().sharedMesh = AtelierMeshLibrary.Save("返工_自然起伏山坡", GroundMesh());
            terrain.GetComponent<MeshRenderer>().sharedMaterial = LoadMaterial("山坡夜色");
            var houses = root.Find("中景错落小屋").Cast<Transform>().Where(t => t.name.Contains("小屋") || t.name.Contains("窄屋") || t.name.Contains("旧屋")).ToArray();
            var positions = new[] { new Vector2(.6f, 32), new Vector2(8.2f, 45), new Vector2(14, 63) };
            for (int i = 0; i < houses.Length; i++)
            {
                var p = positions[i];
                houses[i].localPosition = new Vector3(p.x, Height(p.x, p.y) - .24f, p.y);
                // 基脚埋入同一高度场，入口另有台阶和护坡石，房屋不悬在坡面上。
                for (int j = 0; j < 8; j++)
                {
                    float x = p.x - 2.8f + j * .8f;
                    float z = p.y - 2.25f + .14f * Mathf.Sin(j * 2);
                    var rock = Place("rock_moss_set_01", "护坡基石_" + i + "_" + j, group, new Vector3(x, Height(x, z) - .12f, z), .42f + j % 3 * .12f, j * 39, j % 6);
                    rock.localScale = Vector3.Scale(rock.localScale, new Vector3(1.45f, 1, .8f));
                }
            }
            Place("tree_small_02", "窗左扫描阔叶树", group, new Vector3(-4.3f, Height(-4.3f, 15) - .20f, 15), 7.2f, -17);
            Place("tree_small_02", "窗右扫描阔叶树", group, new Vector3(7.1f, Height(7.1f, 22) - .2f, 22), 7.7f, 128);
            for (int i = 0; i < 14; i++)
            {
                float z = 26 + i * 3.7f;
                float x = i % 2 == 0 ? -4.6f - i * .23f : 12.5f + i * .24f;
                Place("fir_sapling", "坡地针叶树_" + i, group, new Vector3(x, Height(x, z) - .15f, z), 4.1f + i % 4 * .55f, i * 53, i % 3);
            }
            for (int i = 0; i < 46; i++)
            {
                float z = 9 + i * 1.31f;
                float x = Mathf.Sin(i * 2.39f) * (4 + z * .13f) + 2.2f;
                Place("shrub_02", "坡面自然灌丛_" + i, group, new Vector3(x, Height(x, z) - .10f, z), .55f + i % 5 * .12f, i * 31, i % 4);
                if (i % 4 == 0) Place("rock_moss_set_01", "半埋苔石_" + i, group, new Vector3(x - .7f, Height(x - .7f, z) - .2f, z), .35f + i % 3 * .2f, i * 17, i % 6);
            }
            AtelierSceneBuilder.Save();
        }

        /// <summary>地面连续高度，与所有植被根部和房屋基脚共用。</summary>
        public static float Height(float x, float z)
        {
            float height = RawHeight(x,z);
            // 三处柔和台地支撑水平屋基，外围平滑接回谷地，避免坡面穿屋或悬空。
            foreach (var centre in new[] {new Vector2(9,36),new Vector2(27,53),new Vector2(29.8f,72)})
            {
                float distance = Mathf.Max(Mathf.Abs(x-centre.x)/4.8f,Mathf.Abs(z-centre.y)/3.8f);
                float blend = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1.6f,distance));
                height = Mathf.Lerp(height,RawHeight(centre.x,centre.y),blend);
            }
            return height;
        }

        /// <summary>前低后高的连续山坡，将远处旧屋抬入窗景，而非单独升高房屋。</summary>
        private static float RawHeight(float x, float z)
        {
            float hill = 4.5f * Mathf.Exp(-Mathf.Pow((x-28)/14,2)*.5f-Mathf.Pow((z-68)/20,2)*.5f);
            return -.95f-.023f*(z-10)+.68f*Mathf.Sin(x*.13f+z*.031f)+.28f*Mathf.Sin(z*.19f-x*.17f)+hill;
        }

        /// <summary>增量更新已有地形、植被根部和屋基，不重新生成或删除任何已摆放资产。</summary>
        public static void RefreshGroundHeights()
        {
            var landscape = Root().Find("窗外山谷");
            var terrain = landscape.Find("近景枝叶与坡地/连续起伏的山坡");
            terrain.GetComponent<MeshFilter>().sharedMesh = AtelierMeshLibrary.Save("返工_自然起伏山坡",GroundMesh());
            foreach (Transform plant in landscape.Find("返工_自然坡地"))
            {
                var p = plant.position;
                float embed = plant.name.Contains("阔叶树") || plant.name.StartsWith("半埋苔石") ? .2f : plant.name.StartsWith("坡地针叶树") ? .15f : .12f;
                plant.position = new Vector3(p.x,Height(p.x,p.z)-embed,p.z);
            }
            foreach (Transform house in landscape.Find("返工_完整旧屋"))
            {
                var p = house.position;
                house.position = new Vector3(p.x,Height(p.x,p.z)-.23f,p.z);
            }
            AtelierSceneBuilder.Save();
        }

        private static Mesh GroundMesh()
        {
            const int n = 130;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int z = 0; z <= n; z++) for (int x = 0; x <= n; x++)
            {
                float xx = -42 + x * .72f, zz = 6 + z * .70f;
                vertices.Add(new Vector3(xx, Height(xx, zz), zz));
                uv.Add(new Vector2(xx * .16f, zz * .16f));
            }
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
            {
                int a = z * (n + 1) + x;
                triangles.AddRange(new[] { a, a + n + 2, a + 1, a, a + n + 1, a + n + 2 });
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>实例化实际 FBX 或其中一个独立扫描件，按实际包围盒设置自然尺寸。</summary>
        private static Transform Place(string id, string name, Transform parent, Vector3 position, float height, float yaw, int part = -1)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Imports + id + "/" + id + "_1k.fbx");
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            GameObject instance;
            if (part < 0) instance = Object.Instantiate(prefab, holder);
            else instance = Object.Instantiate(prefab.GetComponentsInChildren<MeshRenderer>()[part].gameObject, holder);
            instance.transform.localPosition = Vector3.zero;
            foreach (var renderer in holder.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => ScanMaterial(id, m.name)).ToArray();
            var bounds = BoundsOf(holder);
            float scale = height / Mathf.Max(.001f, bounds.size.y);
            instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            holder.localScale = Vector3.one * scale;
            holder.localRotation = Quaternion.Euler(0, yaw, 0);
            holder.localPosition = position;
            return holder;
        }

        /// <summary>按照原材质槽对应扫描通道，不用单一平涂替代叶片和纸页。</summary>
        private static Material ScanMaterial(string id, string slot)
        {
            string key = id + "_" + slot;
            if (materials.TryGetValue(key, out var result)) return result;
            result = NewMaterial("扫描_" + key, Shader.Find("Universal Render Pipeline/Lit"));
            string suffix = slot.StartsWith(id) ? slot.Substring(id.Length).Trim('_') : "";
            if (suffix == "branches" && id == "tree_small_02") suffix = "branch";
            if (suffix == "trunk") suffix = "";
            string stem = id + (suffix.Length > 0 ? "_" + suffix : "");
            var files = Directory.GetFiles(Imports + id).Where(p => !p.EndsWith(".meta")).ToArray();
            Texture2D Map(string token) => files.Where(p => Path.GetFileName(p).StartsWith(stem + "_" + token)).Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault(t => t != null);
            var diffuse = files.Where(p => Path.GetFileName(p).StartsWith(stem + "_diff") && p.Contains("cutout")).Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault(t => t != null) ?? Map("diff");
            result.SetTexture("_BaseMap", diffuse);
            result.SetColor("_BaseColor", Color.white);
            result.SetTexture("_BumpMap", Map("nor_gl"));
            result.SetFloat("_BumpScale", .55f);
            result.EnableKeyword("_NORMALMAP");
            result.SetFloat("_Smoothness", .20f);
            var rough = Map("rough");
            if (rough != null)
            {
                result.SetTexture("_MetallicGlossMap", PackedMask(key, rough, Map("metallic")));
                result.SetFloat("_Smoothness", 1);
                result.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            bool foliage = slot.Contains("leaves") || slot.Contains("twig") || id == "shrub_02";
            if (foliage)
            {
                result.SetFloat("_AlphaClip", 1);
                result.SetFloat("_Cutoff", .42f);
                result.SetFloat("_Cull", 0);
                result.EnableKeyword("_ALPHATEST_ON");
                result.SetOverrideTag("RenderType", "TransparentCutout");
                result.renderQueue = 2450;
            }
            result.enableInstancing = true;
            materials[key] = result;
            return result;
        }

        /// <summary>把官方粗糙度转换为 URP 光滑度，金属数据只用于扫描件原有的金属部分。</summary>
        private static Texture2D PackedMask(string key, Texture2D rough, Texture2D metallic)
        {
            string path = Art + "Textures/扫描通道_" + key + ".png";
            var previous = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (previous != null) return previous;
            var pixels = rough.GetPixels();
            var metal = metallic != null && metallic.width == rough.width && metallic.height == rough.height ? metallic.GetPixels() : null;
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(metal == null ? 0 : metal[i].r, 0, 0, 1 - pixels[i].r);
            var texture = new Texture2D(rough.width, rough.height, TextureFormat.RGBA32, false, true);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void PlaceOilLamp(Transform parent)
        {
            const string folder = "Assets/_Game/Content/Studio/Art/Imports/PolyHaven/vintage_oil_lamp/";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "vintage_oil_lamp_2k.fbx");
            var holder = new GameObject("扫描维多利亚旧油灯").transform;
            holder.SetParent(parent, false);
            Object.Instantiate(prefab, holder);
            foreach (var r in holder.GetComponentsInChildren<MeshRenderer>())
            {
                bool flame = r.name.Contains("flame"), glass = r.name.Contains("glass");
                string stem = "vintage_oil_lamp" + (flame ? "_flame" : glass ? "_glass" : "");
                var mat = NewMaterial("扫描油灯_" + stem, Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + stem + "_diff_2k.jpg"));
                mat.SetColor("_BaseColor", Color.white);
                if (!flame && !glass)
                {
                    mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + stem + "_nor_gl_2k.jpg"));
                    mat.EnableKeyword("_NORMALMAP");
                }
                if (glass)
                {
                    mat.SetFloat("_Surface", 1); mat.SetFloat("_ZWrite", 0);
                    mat.SetFloat("_SrcBlend", 5); mat.SetFloat("_DstBlend", 10);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.SetColor("_BaseColor", new Color(.85f, .87f, .91f, .17f));
                    mat.renderQueue = 3000;
                }
                if (flame)
                {
                    mat.SetTexture("_EmissionMap", mat.GetTexture("_BaseMap"));
                    mat.SetColor("_EmissionColor", new Color(4.5f, 2.1f, .5f));
                    mat.EnableKeyword("_EMISSION");
                }
                mat.SetFloat("_Smoothness", glass ? .85f : .28f);
                r.sharedMaterial = mat;
            }
            var b = BoundsOf(holder);
            holder.GetChild(0).localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            holder.localScale = Vector3.one * (.79f / b.size.y);
            holder.position = new Vector3(1.30f, 1.05f, -.65f);
        }

        private static void RestOn(Transform item, float surface)
        {
            var bounds = BoundsOf(item);
            item.position += Vector3.up * (surface - bounds.min.y);
        }

        private static Bounds BoundsOf(Transform item)
        {
            var renderers = item.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Material NewMaterial(string name, Shader shader)
        {
            string path = Art + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material LoadMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/" + name + ".mat");
        private static Transform Root()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != AtelierSceneBuilder.ScenePath)
                throw new InvalidOperationException("请在已保存的独立画室中运行返工工具。");
            if (EditorApplication.isPlaying) throw new InvalidOperationException("场景增量编辑需要退出运行模式。");
            return GameObject.Find("独立夜间画室").transform;
        }
    }
}
