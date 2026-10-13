// =============================================================================
// 文件：AtelierGameplayBuilder.cs
// 职责：从已认可的美术场景另存新玩法场景，增量安装第一阶段对象和纸面。
// 边界：只经 Editor API 保存；拒绝覆盖原场景、未保存内容和已经存在的玩法场景。
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Unity;
using Game.Unity.ArtRebuild;
using Game.Unity.TechnicalArt.RealtimePainterly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Editor.ArtRebuild
{
    /// <summary>新画室的首个可游玩切片，不调用任何旧场景生成器。</summary>
    public static class AtelierGameplayBuilder
    {
        public const string ScenePath = "Assets/_Game/Content/Scenes/AtelierPlayable.unity";
        public const string Content = "Assets/_Game/Content/ArtRebuild/Gameplay/";
        private const string Art = "Assets/_Game/Content/ArtRebuild/";
        private const string NotebookPrefabPath = "Assets/_Game/Content/Prefabs/UI/NotebookPanel.prefab";
        private const string PauseMenuPrefabPath = "Assets/_Game/Content/Prefabs/UI/PauseMenuPanel.prefab";
        private const string PauseMenuBackgroundPath = "Assets/_Game/Content/Art/UI/Menu/Generated/PauseMenuVintageFrame_v1.png";
        private const string CollectionPrefabPath = "Assets/_Game/Content/Prefabs/UI/AtelierCollectionPanel.prefab";
        private const string CollectionBrushArtworkPath = "Assets/_Game/Content/Art/UI/Collection/Generated/CollectibleBrushOil_v1.png";
        private const string CollectionSideTabPath = "Assets/_Game/Content/Art/UI/Collection/Generated/CollectionSideTab_v1.png";
        private const string CollectionArrowsPath = "Assets/_Game/Content/Art/UI/Collection/Generated/CollectionPageArrows_v1.png";
        private static Font font;
        private static readonly Color Ink = new Color(.23f, .21f, .17f);
        private static readonly Color MenuBrown = new Color(.20f, .12f, .07f, .95f);

        /// <summary>第一次建立独立副本；原美术场景和共享正式资产保持原样。</summary>
        [MenuItem("游戏/新画室玩法/建立第一阶段独立场景")]
        public static string BuildStageOne()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式。");
            var source = SceneManager.GetActiveScene();
            if (source.path != "Assets/_Game/Content/Scenes/AtelierNight.unity" || source.isDirty)
                throw new InvalidOperationException("请先打开已保存的 AtelierNight 美术基线，不能覆盖未保存内容。");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("玩法场景已存在，请使用增量修正，禁止重新生成。");
            foreach (string dir in new[] { "Meshes", "Materials", "Textures", "Imports", "Audio" }) Directory.CreateDirectory(Content + dir);
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(source, ScenePath, true)) throw new IOException("独立场景另存失败。");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var art = GameObject.Find("独立夜间画室").transform;
            var artState = art.GetComponent<AtelierArtState>();
            artState.OnlyOwnPipelineAtRuntime = true;
            var pipeline = Object.Instantiate(artState.ScenePipeline);
            Save(pipeline, "画室玩法专用管线.asset");
            pipeline.name = "画室玩法专用管线";
            ((UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)pipeline).additionalLightsShadowmapResolution = 4096;
            artState.ScenePipeline = pipeline;
            var root = Group("新画室玩法", null);
            var game = root.gameObject.AddComponent<AtelierGameplay>();
            game.ViewCamera = Camera.main;
            game.PickingMask = 1 << 2;
            foreach (var old in art.GetComponentsInChildren<AtelierBuildEvidence>(true)) Object.DestroyImmediate(old);
            game.Wash = BuildWash(root);
            game.Painting = BuildPainting(art, root, game.Wash);
            game.FocusPose = Group("画作近看机位", root);
            Vector3 normal = game.Painting.Surface.transform.TransformDirection(Vector3.back).normalized;
            game.FocusPose.position = game.Painting.Surface.bounds.center + normal * 1.77f;
            game.FocusPose.rotation = Quaternion.LookRotation(-normal, game.Painting.Surface.transform.up);
            // 生成图片首次导入时 Unity 默认为普通贴图；这里固定为单张 Sprite，保证 UI 可以直接显示。
            LoadNotebookSprite("Assets/_Game/Content/Art/UI/Notebook/Generated/NotebookOpenSpread_v1.png");
            LoadNotebookSprite("Assets/_Game/Content/Art/UI/Notebook/Generated/NotebookReturnArrow_v1.png");
            LoadNotebookSprite("Assets/_Game/Content/Art/UI/Notebook/Generated/NotebookRegionPanels_v1.png");
            BuildUi(root, game);
            AddOcclusion(art, root);
            // 新对象不进入旧领域绑定；正式入口暂保留已交付美术场景，完整路线后再切换。
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "已建立 " + ScenePath + "；美术基线未覆盖，Build Settings 未切换。";
        }

        /// <summary>仅重建已存在开发副本的笔记本 UI；保留人工美术、玩法物体和正式构建场景。</summary>
        [MenuItem("游戏/新画室玩法/刷新笔记本模块")]
        public static void RefreshNotebookModule()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式。");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("当前场景有未保存改动，已拒绝切换场景或重建笔记本。");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("新画室玩法")?.transform;
            var game = root != null ? root.GetComponent<AtelierGameplay>() : null;
            if (game == null) throw new InvalidOperationException("未找到新画室玩法根节点，不能增量刷新笔记本。");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(NotebookPrefabPath) != null)
            {
                // 笔记纸面已经迁移为人工维护的 Prefab；刷新场景不能覆盖其图片和布局。
                font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSans/NotoSansCJKsc-Regular.otf");
                UpgradeNotebookPrefabLayout();
                var canvas = root.Find("画室纸面与字幕");
                if (canvas != null)
                {
                    EnsureManuscriptPanel(canvas, game);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("笔记本模块保存失败。");
                AssetDatabase.SaveAssets();
                Debug.Log("[笔记本] 已保留可手调 Prefab，并同步词条列、菜单 Prefab 与便签阅读面板。");
                return;
            }
            // 只替换本 Builder 负责的 UI 根节点，避免触碰人工美术与独立玩法的物理对象。
            var oldCanvas = root.Find("画室纸面与字幕");
            if (oldCanvas != null) Object.DestroyImmediate(oldCanvas.gameObject);
            var oldEvents = root.Find("EventSystem");
            if (oldEvents != null) Object.DestroyImmediate(oldEvents.gameObject);
            BuildUi(root, game);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("笔记本模块保存失败。");
            AssetDatabase.SaveAssets();
            Debug.Log("[笔记本] 已刷新词性、灵感与双页纸面模块。");
        }

        /// <summary>恢复原生调查与返回 UI，并把暂停菜单单独迁移为可手调 Prefab。</summary>
        [MenuItem("游戏/新画室玩法/重置调查与返回，并生成暂停菜单 Prefab")]
        public static void ResetContextAndCreatePauseMenuPrefab()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式。");
            var root = GameObject.Find("新画室玩法")?.transform;
            var game = root != null ? root.GetComponent<AtelierGameplay>() : null;
            var canvas = root?.Find("画室纸面与字幕");
            if (game == null || canvas == null) throw new InvalidOperationException("请先打开 AtelierPlayable 场景，再重置菜单。");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSans/NotoSansCJKsc-Regular.otf");
            var oldMenuRoot = canvas.Find("画室菜单");
            if (oldMenuRoot != null) Object.DestroyImmediate(oldMenuRoot.gameObject);
            RestoreOriginalContextAndReturn(canvas, game);
            EnsurePauseMenuPrefab(canvas, game);
            EnsureManuscriptPanel(canvas, game);
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            if (!EditorSceneManager.SaveScene(activeScene)) throw new IOException("暂停菜单场景实例保存失败。");
            AssetDatabase.SaveAssets();
            Debug.Log("[菜单] 已恢复原生调查与返回，并生成暂停菜单 Prefab：" + PauseMenuPrefabPath);
        }

        /// <summary>仅用暂停菜单 Prefab 替换当前场景旧实例，不触碰调查、返回或笔记本的人工布局。</summary>
        [MenuItem("游戏/新画室玩法/同步暂停菜单 Prefab 到当前场景")]
        public static void SyncPauseMenuPrefabToCurrentScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式。");
            var root = GameObject.Find("新画室玩法")?.transform;
            var game = root != null ? root.GetComponent<AtelierGameplay>() : null;
            var canvas = root?.Find("画室纸面与字幕");
            if (game == null || canvas == null) throw new InvalidOperationException("请先打开 AtelierPlayable 场景，再同步暂停菜单。");

            EnsurePauseMenuPrefab(canvas, game);
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            if (!EditorSceneManager.SaveScene(activeScene)) throw new IOException("暂停菜单场景实例保存失败。");
            AssetDatabase.SaveAssets();
            Debug.Log("[菜单] 已清除旧尺寸覆盖，并从 Prefab 同步暂停菜单：" + PauseMenuPrefabPath);
        }

        /// <summary>安装藏品簿 Prefab、笔记侧边入口和当前场景引用；不重建人工调整过的笔记布局。</summary>
        [MenuItem("游戏/新画室玩法/安装藏品簿模块")]
        public static void InstallCollectionModule()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式。");
            var root = GameObject.Find("新画室玩法")?.transform;
            var game = root != null ? root.GetComponent<AtelierGameplay>() : null;
            var canvas = root?.Find("画室纸面与字幕");
            if (game == null || canvas == null) throw new InvalidOperationException("请先打开 AtelierPlayable 场景，再安装藏品簿。");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSans/NotoSansCJKsc-Regular.otf");
            LoadNotebookSprite(CollectionBrushArtworkPath);
            LoadNotebookSprite(CollectionSideTabPath);
            LoadNotebookSprite(CollectionArrowsPath);
            EnsureNotebookCollectionEntry();
            AssetDatabase.SaveAssets();

            // NotebookView 挂在全屏 Canvas，而可见笔记根节点是其下的“笔记纸面”。
            // 入口必须作为纸面的子节点，不能挂到 Canvas 上而在场景画面中常驻。
            var strayEntry = canvas.Find("藏品入口便签");
            if (strayEntry != null) Object.DestroyImmediate(strayEntry.gameObject);
            var notebookPanel = canvas.Find("笔记纸面");
            var entry = notebookPanel != null ? notebookPanel.Find("藏品入口便签")?.GetComponent<Button>() : null;
            if (entry == null)
            {
                if (notebookPanel == null) throw new InvalidOperationException("当前场景找不到笔记纸面，不能安装藏品入口。");
                // 少数带旧 Prefab Override 的场景不会自动接收新增子节点；只补这一枚便签，不重置其余布局。
                entry = CreateNotebookCollectionEntry(notebookPanel);
            }
            EnsureCollectionPanel(canvas, game, entry, (RectTransform)notebookPanel);
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            if (!EditorSceneManager.SaveScene(activeScene)) throw new IOException("藏品簿场景引用保存失败。");
            AssetDatabase.SaveAssets();
            Debug.Log("[藏品] 已安装可手调 Prefab：" + CollectionPrefabPath);
        }

        /// <summary>
        /// 将笔记本生成美术素材按 UI 单图的形式导入，并返回可供 <see cref="Image"/> 使用的精灵。
        /// </summary>
        /// <param name="assetPath">工程内的图片路径。</param>
        /// <returns>导入后的精灵；图片不存在时返回 <c>null</c>。</returns>
        private static Sprite LoadNotebookSprite(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                bool needsReimport = importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Single
                    || !importer.alphaIsTransparency
                    || importer.mipmapEnabled;

                if (needsReimport)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static AtelierWashPresenter BuildWash(Transform root)
        {
            var group = Group("独立洗笔工作簇", root);
            group.position = new Vector3(-.17f, 1.058f, -1.18f);
            var glaze = new Material(AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/灰绿陶釉.mat"));
            glaze.name = "洗笔罐_磨损暖白釉";
            glaze.SetColor("_BaseColor", new Color(.63f, .59f, .47f));
            glaze.SetFloat("_Smoothness", .29f); glaze.SetFloat("_WearStrength", .31f);
            Save(glaze, "Materials/洗笔罐_磨损暖白釉.mat");
            // 连续截面包含足圈、鼓腹、厚口、内壁和底，尺寸为实际米制。
            var profile = new[] {
                new Vector2(0,0),new Vector2(.078f,0),new Vector2(.091f,.004f),new Vector2(.096f,.011f),
                new Vector2(.096f,.019f),new Vector2(.101f,.023f),new Vector2(.113f,.03f),new Vector2(.124f,.047f),
                new Vector2(.137f,.08f),new Vector2(.145f,.13f),new Vector2(.149f,.18f),new Vector2(.151f,.21f),
                new Vector2(.154f,.218f),new Vector2(.153f,.224f),new Vector2(.148f,.228f),new Vector2(.141f,.226f),
                new Vector2(.137f,.22f),new Vector2(.137f,.205f),new Vector2(.134f,.17f),new Vector2(.129f,.12f),
                new Vector2(.119f,.075f),new Vector2(.104f,.047f),new Vector2(.088f,.036f),new Vector2(0,.036f)
            };
            var shell = MeshNode("Visual_有厚口和内壁的洗笔罐", group, SaveMesh("洗笔罐厚壁", AtelierMeshLibrary.Lathe(profile, 128)), glaze);
            var waterMaterial = new Material(Shader.Find("RoomDemo/Atelier/WashWater"));
            Save(waterMaterial, "Materials/洗笔水_有限颜料.mat");
            var water = MeshNode("Visual_杯内自由液面", group, Disc(.134f, 128, 24), waterMaterial);
            water.transform.localPosition = Vector3.up * .183f;
            var presenter = group.gameObject.AddComponent<AtelierWashPresenter>();
            presenter.Water = water;
            presenter.Pigment = group.gameObject.AddComponent<GpuPigment>();
            presenter.Pigment.Mode = 1; presenter.Pigment.Width = presenter.Pigment.Height = 512;
            presenter.Pigment.Target = water; presenter.Pigment.AutomaticTime = false; presenter.Pigment.Flow = 0;
            presenter.Pigment.SimulationShader = Shader.Find("Hidden/RoomDemo/Atelier/WashPigment");
            var pivot = Group("Visual_杯口支承点", group);
            pivot.localPosition = new Vector3(.13f, .225f, -.008f);
            pivot.localRotation = Quaternion.FromToRotation(Vector3.right, new Vector3(-.85f, -.48f, .21f).normalized);
            // 只为本场景复制源模型并开启 CPU 顶点访问，不修改正式 LUMI 导入设置。
            string brushPath = Content + "Imports/洗笔画笔_SM_Brush_01a.fbx";
            AssetDatabase.CopyAsset(Art + "Imports/LumiPalette/SM_Brush_01a.fbx", brushPath);
            var importer = (ModelImporter)AssetImporter.GetAtPath(brushPath);
            importer.isReadable = true; importer.importNormals = ModelImporterNormals.Calculate; importer.SaveAndReimport();
            var brushMesh = AssetDatabase.LoadAllAssetsAtPath(brushPath).OfType<Mesh>().First();
            var brush = MeshNode("Visual_可弯曲笔毛的扫描画笔", pivot, brushMesh,
                AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/LUMI 旧画笔.mat"));
            brush.transform.localScale = Vector3.one * 125;
            Vector3 tip = group.TransformPoint(new Vector3(.0536f, .179f, .01072f));
            brush.transform.localPosition = pivot.InverseTransformPoint(tip) - Vector3.right * brushMesh.bounds.max.x * 125;
            presenter.SupportedBrush = pivot; presenter.BrushSurface = brush.GetComponent<MeshFilter>();
            var pick = Group("Logic_洗笔罐", group); pick.gameObject.layer = 2;
            var box = pick.gameObject.AddComponent<BoxCollider>(); box.center = new Vector3(0,.12f,0); box.size = new Vector3(.31f,.24f,.31f);
            var target = pick.gameObject.AddComponent<AtelierInteractionTarget>();
            target.Kind = AtelierTargetKind.WashWater; target.ObjectId = StudioRules.Cup.Value; target.DisplayName = "杯里的水";
            return presenter;
        }

        private static AtelierPaintingPresenter BuildPainting(Transform art, Transform root, AtelierWashPresenter wash)
        {
            var old = art.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "塞尚溪流临摹参考_CC0");
            var surface = Object.Instantiate(old.gameObject, old.transform.parent).GetComponent<Renderer>();
            surface.name = "Visual_取材当前画室的未交稿画作";
            var presenter = surface.gameObject.AddComponent<AtelierPaintingPresenter>(); presenter.Surface = surface;
            // 临摹参考另挂侧墙；新任务画仍在原画架里，不挪动已认可的主要木作。
            old.transform.SetParent(art, true); old.name = "塞尚溪流参考纸_侧墙";
            old.transform.SetPositionAndRotation(new Vector3(-4.23f, 1.65f, 2.6f), Quaternion.Euler(0,-90,0));
            old.transform.localScale = Vector3.one * .65f;
            var material = new Material(Shader.Find("RoomDemo/Atelier/TaskPainting"));
            Save(material, "Materials/任务画_亚麻与湿色.mat"); surface.sharedMaterial = material;
            var logic = Group("Logic_任务画", surface.transform); logic.gameObject.layer = 2;
            var collider = logic.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = surface.GetComponent<MeshFilter>().sharedMesh;
            var target = logic.gameObject.AddComponent<AtelierInteractionTarget>(); target.Kind = AtelierTargetKind.Painting;
            target.ObjectId = AtelierRules.Painting.Value; target.DisplayName = "未交稿的画";
            BakePainting(art, presenter, wash);
            return presenter;
        }

        /// <summary>从真实场景取材并按亮度边缘重绘短笔触，不读取概念图或外部图片。</summary>
        public static void BakePainting(Transform art, AtelierPaintingPresenter painting, AtelierWashPresenter wash)
        {
            var source = Camera.main;
            var cameraObject = Object.Instantiate(source.gameObject);
            cameraObject.name = "任务画取材临时相机";
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(-.15f, 1.98f, -3.0f);
            camera.transform.LookAt(new Vector3(.65f, 1.65f, 1.6f)); camera.fieldOfView = 58;
            const int width = 1024, height = 800;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var oldActive = RenderTexture.active;
            var easel = art.Find("室内家具/细木作画架");
            bool wasActive = easel.gameObject.activeSelf;
            Texture2D capture = null, result = null;
            try
            {
                easel.gameObject.SetActive(false);
                camera.targetTexture = target; camera.aspect = (float)width / height; camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(width,height,TextureFormat.RGB24,false);
                capture.ReadPixels(new Rect(0,0,width,height),0,0); capture.Apply();
                string evidence = "Logs/AtelierCaptures/任务画取材";
                Directory.CreateDirectory(evidence); File.WriteAllBytes(evidence + "/任务画取材_当前场景原始渲染.png", capture.EncodeToPNG());
                Vector3 water = camera.WorldToViewportPoint(wash.Water.bounds.center);
                Vector3 side = camera.WorldToViewportPoint(wash.Water.bounds.center + Vector3.right * .134f);
                Vector3 depth = camera.WorldToViewportPoint(wash.Water.bounds.center + Vector3.forward * .134f);
                painting.WaterUV = new Vector2(water.x,water.y);
                painting.WaterRadius = new Vector2(Mathf.Abs(side.x-water.x), Mathf.Abs(depth.y-water.y)) * 1.06f;
                var lamp = camera.WorldToViewportPoint(new Vector3(1.30f,1.6f,-.65f));
                var window = camera.WorldToViewportPoint(new Vector3(1.35f,2.5f,2.2f));
                painting.LampUV = new Vector2(lamp.x,lamp.y); painting.WindowUV = new Vector2(window.x,window.y);
                result = Repaint(capture);
                string path = Content + "Textures/任务画_现场短笔触重绘.png";
                File.WriteAllBytes(path, result.EncodeToPNG()); AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = true; importer.SaveAndReimport();
                painting.Surface.sharedMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                painting.Surface.sharedMaterial.SetVector("_WaterRegion", new Vector4(water.x,water.y,painting.WaterRadius.x,painting.WaterRadius.y));
                EditorUtility.SetDirty(painting.Surface.sharedMaterial);
                EditorUtility.SetDirty(painting);
            }
            finally
            {
                easel.gameObject.SetActive(wasActive); RenderTexture.active = oldActive;
                camera.targetTexture = null; RenderTexture.ReleaseTemporary(target);
                if (capture != null) Object.DestroyImmediate(capture);
                if (result != null) Object.DestroyImmediate(result);
                Object.DestroyImmediate(cameraObject);
            }
        }

        /// <summary>把项目现场渲染转成确定的短笔触；调用者负责释放返回的临时纹理。</summary>
        public static Texture2D Repaint(Texture2D source)
        {
            int w = source.width, h = source.height;
            var input = source.GetPixels(); var output = input.Select(c=>Color.Lerp(c,new Color(.28f,.23f,.17f),.14f)).ToArray();
            var random = new System.Random(14011);
            Color Sample(int x,int y) => input[Mathf.Clamp(y,0,h-1)*w+Mathf.Clamp(x,0,w-1)];
            foreach (int radius in new[] { 18, 10, 5, 3 })
            {
                int step = Mathf.Max(2, radius);
                for (int y = 0; y < h; y += step) for (int x = 0; x < w; x += step)
                {
                    int px = Mathf.Clamp(x+random.Next(-step/2,step/2+1),0,w-1);
                    int py = Mathf.Clamp(y+random.Next(-step/2,step/2+1),0,h-1);
                    float gx = Sample(px+3,py).grayscale-Sample(px-3,py).grayscale;
                    float gy = Sample(px,py+3).grayscale-Sample(px,py-3).grayscale;
                    float angle = Mathf.Atan2(gy,gx)+Mathf.PI*.5f;
                    if (gx*gx+gy*gy < .0001f) angle = Mathf.Sin(px*.013f+py*.007f)*.6f;
                    float cs = Mathf.Cos(angle), sn = Mathf.Sin(angle);
                    var colour = Sample(px,py); colour *= .94f + (float)random.NextDouble()*.10f;
                    colour = Color.Lerp(colour,new Color(.43f,.35f,.24f),.045f);
                    int extent = radius*2;
                    for (int yy = -extent; yy <= extent; yy++) for (int xx = -extent; xx <= extent; xx++)
                    {
                        int ax=px+xx, ay=py+yy; if(ax<0||ax>=w||ay<0||ay>=h)continue;
                        float along=(xx*cs+yy*sn)/(radius*1.65f), across=(-xx*sn+yy*cs)/(radius*.58f);
                        float shape=along*along+across*across; if(shape>1)continue;
                        float bristle=.77f+.23f*Mathf.Sin(across*19+px*.73f);
                        float opacity=(1-Mathf.SmoothStep(.58f,1,shape))*.61f*bristle;
                        output[ay*w+ax]=Color.Lerp(output[ay*w+ax],colour,opacity);
                    }
                }
            }
            var result = new Texture2D(w,h,TextureFormat.RGB24,false); result.SetPixels(output); result.Apply(); return result;
        }

        private static void AddOcclusion(Transform art,Transform root)
        {
            var logic=Group("Logic_室内遮挡",root);
            foreach(var renderer in art.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!renderer.transform.IsChildOf(art.Find("建筑与木作")) && !renderer.name.Contains("厚木") && !renderer.name.Contains("桌面")) continue;
                if(renderer.sharedMaterials.Any(m=>m!=null&&m.renderQueue>=3000))continue;
                var filter=renderer.GetComponent<MeshFilter>(); if(filter==null)continue;
                var go=Group("遮挡_"+renderer.name,logic); go.gameObject.layer=2;
                go.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation); go.localScale=renderer.transform.lossyScale;
                go.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
            }
        }

        private static void BuildUi(Transform root,AtelierGameplay game)
        {
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSans/NotoSansCJKsc-Regular.otf");
            var canvas=Group("画室纸面与字幕",root).gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720); scaler.matchWidthOrHeight=.5f; canvas.gameObject.AddComponent<GraphicRaycaster>();
            var events=Group("EventSystem",root).gameObject; events.AddComponent<EventSystem>(); events.AddComponent<InputSystemUIInputModule>();
            // 笔记本 Prefab 的基准纸面尺寸；内部区域由人工在 Prefab 中自行摆放。
            var body=Panel("笔记纸面",canvas.transform,new Vector2(932,500),new Vector2(0,10),Color.white);
            var notebookBackground=LoadNotebookSprite("Assets/_Game/Content/Art/UI/Notebook/Generated/NotebookOpenSpread_v1.png");
            body.GetComponent<Image>().sprite=notebookBackground;
            body.GetComponent<Image>().type=Image.Type.Simple;
            var opacity=body.gameObject.AddComponent<CanvasGroup>();
            Label("纸页标题",body,"画室笔记",new Vector2(-270,251),new Vector2(220,40),25,Ink);
            var back=Button("返回首页",body,"",new Vector2(-430,252),new Vector2(84,42));
            back.GetComponent<Image>().sprite=LoadNotebookSprite("Assets/_Game/Content/Art/UI/Notebook/Generated/NotebookReturnArrow_v1.png");
            back.GetComponent<Image>().color=Color.white; back.GetComponentInChildren<Text>().text="";
            var nouns=WordColumn("名词材料",body,new Vector2(215,338),new Vector2(-335,-2));
            var adjectives=WordColumn("形容词材料",body,new Vector2(150,140),new Vector2(-104,116));
            var others=WordColumn("其他材料",body,new Vector2(150,140),new Vector2(-104,-103));
            // 右页背景横向收窄约十分之一，提示区与落笔区保持约 1:9 的高度比例。
            var tips=Panel("提示词区",body,new Vector2(426,44),new Vector2(278,220),new Color(.97f,.92f,.78f,.37f));
            var explorationHint=Label("探索提示",tips,"目前场景中还有物品可探索观察。",new Vector2(0,10),new Vector2(392,20),14,Ink);
            var logicHint=Label("灵感提示",tips,"还有灵感可观察回忆。",new Vector2(0,-10),new Vector2(392,20),14,Ink);
            var workspace=Panel("落下一笔区",body,new Vector2(426,396),new Vector2(278,-43),new Color(.98f,.94f,.82f,.38f));
            Label("落下一笔标题",workspace,"落下一笔",new Vector2(0,166),new Vector2(220,30),23,Ink);
            var logicShelf=Panel("灵感陈列",workspace,new Vector2(376,74),new Vector2(0,119),Color.clear);
            var logicLayout=logicShelf.gameObject.AddComponent<VerticalLayoutGroup>();logicLayout.childControlHeight=true;logicLayout.childControlWidth=true;logicLayout.childForceExpandHeight=false;logicLayout.spacing=2;
            logicShelf.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var template=Button("已知材料模板",body,"",Vector2.zero,new Vector2(260,43));template.gameObject.AddComponent<LayoutElement>().preferredHeight=43;template.gameObject.SetActive(false);
            var card=Button("活动词条模板",workspace,"",Vector2.zero,new Vector2(142,62));card.gameObject.SetActive(false);
            var submit=Button("解释按钮",workspace,"解释",new Vector2(0,-164),new Vector2(126,48));
            // 解释是落笔区域的明确操作，使用深色纸签底图而不是仅显示文字。
            submit.GetComponent<Image>().color=new Color(.31f,.17f,.08f,.88f);
            submit.GetComponentInChildren<Text>().color=new Color(.96f,.89f,.70f);
            var hidden=Group("兼容绑定",body);hidden.gameObject.SetActive(false);
            var selection=Label("临时选择",hidden,"",Vector2.zero,Vector2.one,22,Ink);var feedback=Label("反馈",hidden,"",Vector2.zero,Vector2.one,22,Ink);
            var history=Label("历史",hidden,"",Vector2.zero,Vector2.one,22,Ink);var marks=Label("记号",hidden,"",Vector2.zero,Vector2.one,22,Ink);
            var invitation=Label("纸面反馈",workspace,"",new Vector2(0,58),new Vector2(440,35),18,Ink);
            var insight=Label("形成的新词",workspace,"",new Vector2(0,58),new Vector2(440,35),22,new Color(.25f,.39f,.63f));
            var description=Label("词语说明",body,"",new Vector2(-238,-246),new Vector2(490,44),16,Ink);
            var reminder=Button("未读笔记提醒",canvas.transform,"笔记 · N",new Vector2(-550,-322),new Vector2(124,38));
            reminder.GetComponentInChildren<Text>().color=new Color(.9f,.86f,.76f);
            var notice=Panel("短暂新发现",canvas.transform,new Vector2(420,45),new Vector2(370,-285),Color.clear);
            var noticeText=Label("发现文字",notice,"",Vector2.zero,new Vector2(420,45),18,new Color(.93f,.89f,.78f));
            var lines=new Image[2];for(int i=0;i<2;i++)lines[i]=Panel("近邻淡线"+i,workspace,new Vector2(140,1),Vector2.zero,new Color(.4f,.34f,.24f,.14f)).GetComponent<Image>();
            var paper=Sound(root,"翻页",.04f);var ink=Sound(root,"落笔",.04f);var scratch=Sound(root,"纸面轻反馈",.02f);
            var notes=canvas.gameObject.AddComponent<NotebookView>();
            notes.Configure(body.gameObject,Group("旧物体占位",hidden),Group("旧概念占位",hidden),template,submit,selection,feedback);
            notes.ConfigureSlots(Array.Empty<Button>(),history);notes.ConfigureControls(reminder,null);notes.ConfigureDiscoveryFeedback(notice.gameObject,noticeText);
            notes.ConfigurePresentation(body,opacity,lines[0],paper,ink,scratch);notes.ConfigureWordCategories(nouns,adjectives,others);notes.ConfigureLogicShelf(logicShelf,explorationHint,logicHint);notes.ConfigureReturnButton(back);notes.ConfigureExploration(adjectives,insight);notes.ConfigureQuietStudio(true);notes.ConfigureDescription(description);
            var semantic=body.gameObject.AddComponent<NotebookCanvasView>();semantic.Configure(workspace,card,submit,lines,invitation);semantic.ConfigureBlindComposition(true);semantic.ConfigureQuietStudio(true);semantic.ConfigureClickPlacement(true);notes.ConfigureCanvas(semantic);
            var caption=Label("世界字幕",canvas.transform,"",new Vector2(0,-325),new Vector2(850,40),19,new Color(.84f,.89f,.94f));
            var captions=caption.gameObject.AddComponent<OneLineSubtitle>();captions.Configure(caption,caption.gameObject.AddComponent<CanvasGroup>());
            var narrationArea=Panel("旁白区域",canvas.transform,new Vector2(960,70),new Vector2(0,-313),Color.clear);
            var narrationText=Label("旁白",narrationArea,"",Vector2.zero,new Vector2(950,70),23,new Color(.95f,.91f,.82f));
            var audio=canvas.gameObject.AddComponent<ContentFeedback>();audio.Configure(Sound(root,"旁白轻声",0),Array.Empty<AudioCueDefinition>(),Array.Empty<RecipePresentation>(),captions,notes,null,null,null,null);
            var narrator=canvas.gameObject.AddComponent<NarrationSystem>();narrator.Configure(narrationText,narrationArea.gameObject.AddComponent<CanvasGroup>(),audio,Array.Empty<NarrationDefinition>());
            var context=Panel("调查与动作",canvas.transform,new Vector2(440,100),new Vector2(-385,277),MenuBrown);
            game.ObservationText=Label("观察描述",context,"",new Vector2(-40,12),new Vector2(330,75),19,new Color(.93f,.89f,.78f));
            game.ContextAction=Button("当前动作",context,"",new Vector2(155,-15),new Vector2(110,42));game.ContextAction.GetComponentInChildren<Text>().color=new Color(.94f,.89f,.78f);
            game.ContextPanel=context.gameObject;
            game.ReturnButton=Button("返回画室",canvas.transform,"返回画室",new Vector2(532,316),new Vector2(150,42));game.ReturnButton.GetComponentInChildren<Text>().color=new Color(.94f,.9f,.8f);
            var pause=Panel("暂停菜单",canvas.transform,new Vector2(390,290),Vector2.zero,MenuBrown);
            game.ResumeButton=Button("继续",pause,"继续",new Vector2(0,78),new Vector2(230,44));game.RestartButton=Button("重新开始",pause,"重新开始",new Vector2(0,18),new Vector2(230,44));game.QuitButton=Button("退出",pause,"退出",new Vector2(0,-42),new Vector2(230,44));
            foreach(var button in new[]{game.ResumeButton,game.RestartButton,game.QuitButton})button.GetComponentInChildren<Text>().color=new Color(.94f,.90f,.80f);
            Label("操作说明",pause,"按住右键或空格转头 · 滚轮看近\nN 打开笔记 · 近看时右键返回",new Vector2(0,-111),new Vector2(375,58),16,new Color(.84f,.82f,.75f));
            game.HoverText=Label("悬停物体名",canvas.transform,"",Vector2.zero,new Vector2(220,35),18,new Color(.91f,.89f,.81f));
            game.Notebook=notes;game.Narrator=narrator;game.Captions=captions;game.PausePanel=pause.gameObject;game.Reminder=reminder;
            CreateNotebookPrefabIfMissing(body.gameObject);
            UpgradeNotebookPrefabLayout();
            EnsurePauseMenuPrefab(canvas.transform, game);
            EnsureManuscriptPanel(canvas.transform, game);
        }

        /// <summary>
        /// 首次生成后将笔记纸面连接为可在 Inspector 中直接编辑的 Prefab；以后刷新场景不会覆盖其人工布局。
        /// </summary>
        /// <param name="body">已完整构造并绑定内部引用的笔记纸面根节点。</param>
        private static void CreateNotebookPrefabIfMissing(GameObject body)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(NotebookPrefabPath) != null) return;
            const string prefabsFolder = "Assets/_Game/Content/Prefabs";
            const string uiFolder = prefabsFolder + "/UI";
            if (!AssetDatabase.IsValidFolder(prefabsFolder)) AssetDatabase.CreateFolder("Assets/_Game/Content", "Prefabs");
            if (!AssetDatabase.IsValidFolder(uiFolder)) AssetDatabase.CreateFolder(prefabsFolder, "UI");

            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(body, NotebookPrefabPath, InteractionMode.AutomatedAction);
            if (prefab == null) throw new IOException("笔记本 Prefab 创建失败。");
            Debug.Log("[笔记本] 已创建可手调 Prefab：" + NotebookPrefabPath);
        }

        /// <summary>为已有手调 Prefab 追加不会覆盖美术布局的词条列自适应组件。</summary>
        private static void UpgradeNotebookPrefabLayout()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(NotebookPrefabPath);
            try
            {
                foreach (string name in new[] { "名词材料", "形容词材料", "其他材料" })
                {
                    var column = prefabRoot.transform.Find(name);
                    if (column == null) throw new InvalidOperationException("笔记本 Prefab 缺少词条列：" + name);
                    if (column.GetComponent<NotebookWordColumnLayout>() == null)
                        column.gameObject.AddComponent<NotebookWordColumnLayout>();
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, NotebookPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>仅向可手调笔记本 Prefab 补充右侧藏品便签，不移动已有人工布局。</summary>
        private static void EnsureNotebookCollectionEntry()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(NotebookPrefabPath);
            try
            {
                if (prefabRoot.transform.Find("藏品入口便签") != null) return;
                CreateNotebookCollectionEntry(prefabRoot.transform);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, NotebookPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>在指定笔记根节点的右页外侧放置一枚可手调的藏品便签。</summary>
        private static Button CreateNotebookCollectionEntry(Transform parent)
        {
            var entry = Button("藏品入口便签", parent, "", new Vector2(486, 28), new Vector2(44, 142));
            var entryImage = entry.GetComponent<Image>();
            entryImage.sprite = LoadNotebookSprite(CollectionSideTabPath);
            entryImage.color = Color.white;
            entryImage.type = Image.Type.Simple;
            entryImage.preserveAspect = true;
            entry.GetComponentInChildren<Text>().text = "";
            return entry;
        }

        /// <summary>确保当前场景引用藏品 Prefab；已有 Prefab 的手调尺寸和布局不会被生成器覆盖。</summary>
        private static void EnsureCollectionPanel(Transform canvas, AtelierGameplay game, Button notebookEntry, RectTransform notebookPanel)
        {
            var panel = canvas.Find("藏品页");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionPrefabPath);
            bool createdPrefab = false;
            if (prefab != null)
            {
                RepairCollectionPrefabAppearance();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionPrefabPath);
            }
            if (panel == null && prefab != null) panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas)).transform;
            if (panel == null)
            {
                panel = CreateCollectionPanel(canvas);
                createdPrefab = true;
                const string prefabsFolder = "Assets/_Game/Content/Prefabs";
                const string uiFolder = prefabsFolder + "/UI";
                if (!AssetDatabase.IsValidFolder(prefabsFolder)) AssetDatabase.CreateFolder("Assets/_Game/Content", "Prefabs");
                if (!AssetDatabase.IsValidFolder(uiFolder)) AssetDatabase.CreateFolder(prefabsFolder, "UI");
                panel.gameObject.SetActive(true);
                if (PrefabUtility.SaveAsPrefabAssetAndConnect(panel.gameObject, CollectionPrefabPath, InteractionMode.AutomatedAction) == null)
                    throw new IOException("藏品簿 Prefab 创建失败。");
            }
            var view = panel.GetComponent<AtelierCollectionView>();
            if (view == null) throw new InvalidOperationException("藏品簿 Prefab 缺少 AtelierCollectionView。");
            if (createdPrefab)
            {
                // 仅首次创建时对齐笔记本；以后尊重藏品 Prefab 中的人工布局和尺寸。
                var collectionRect = (RectTransform)panel;
                collectionRect.anchorMin = notebookPanel.anchorMin;
                collectionRect.anchorMax = notebookPanel.anchorMax;
                collectionRect.pivot = notebookPanel.pivot;
                collectionRect.anchoredPosition = notebookPanel.anchoredPosition;
                collectionRect.sizeDelta = notebookPanel.sizeDelta;
            }
            StretchCollectionDimLayer(panel);
            var existingDetailArtwork = panel.Find("单个藏品展示/放大藏品图")?.GetComponent<Image>();
            if (existingDetailArtwork != null) existingDetailArtwork.color = Color.white;
            view.ConfigureEntryButton(notebookEntry);
            // 跨 Prefab 的入口引用必须显式记录为场景 Override，避免重开场景后丢失。
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            EditorUtility.SetDirty(view);
            game.Collection = view;
            panel.gameObject.SetActive(false);
        }

        /// <summary>修复早期详情图以透明色创建的显示缺陷，不改动玩家手调的位置和尺寸。</summary>
        private static void RepairCollectionPrefabAppearance()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(CollectionPrefabPath);
            try
            {
                var detailArtwork = prefabRoot.transform.Find("单个藏品展示/放大藏品图")?.GetComponent<Image>();
                if (detailArtwork != null) detailArtwork.color = Color.white;
                StretchCollectionDimLayer(prefabRoot.transform);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, CollectionPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>首次创建与笔记本同尺寸的十二格藏品簿；后续位置和大小可直接在 Prefab Mode 编辑。</summary>
        private static RectTransform CreateCollectionPanel(Transform canvas)
        {
            var panel = Panel("藏品页", canvas, new Vector2(932, 500), new Vector2(0, 10), Color.white);
            var background = panel.GetComponent<Image>();
            background.sprite = LoadNotebookSprite("Assets/_Game/Content/Art/UI/Notebook/Generated/NotebookOpenSpread_v1.png");
            background.type = Image.Type.Simple;
            background.color = Color.white;
            Label("标题", panel, "藏品簿", new Vector2(0, 218), new Vector2(240, 42), 28, Ink);
            var returnButton = Button("返回笔记", panel, "", new Vector2(-430, 220), new Vector2(84, 42));
            returnButton.GetComponent<Image>().sprite = LoadNotebookSprite("Assets/_Game/Content/Art/UI/Notebook/Generated/NotebookReturnArrow_v1.png");
            returnButton.GetComponent<Image>().color = Color.white;
            returnButton.GetComponentInChildren<Text>().text = "";

            var leftSlots = CreateCollectionSlotGrid("左页六格", panel, new Vector2(-232, -7));
            var rightSlots = CreateCollectionSlotGrid("右页六格", panel, new Vector2(232, -7));
            var template = Button("藏品卡模板", panel, "", new Vector2(0, -400), new Vector2(148, 116));
            template.GetComponent<Image>().color = new Color(.34f, .21f, .12f, .16f);
            var art = Panel("藏品图", template.transform, new Vector2(82, 72), new Vector2(0, 14), Color.white);
            art.GetComponent<Image>().preserveAspect = true;
            art.GetComponent<Image>().raycastTarget = false;
            var templateText = template.GetComponentInChildren<Text>();
            templateText.name = "藏品名";
            templateText.rectTransform.anchoredPosition = new Vector2(0, -42);
            templateText.rectTransform.sizeDelta = new Vector2(140, 28);
            templateText.fontSize = 16;
            template.gameObject.SetActive(false);

            var previous = Button("上一页", panel, "‹", new Vector2(-55, -218), new Vector2(52, 34));
            var next = Button("下一页", panel, "›", new Vector2(55, -218), new Vector2(52, 34));
            foreach (var navigation in new[] { previous, next })
            {
                navigation.GetComponent<Image>().color = Color.clear;
                navigation.GetComponentInChildren<Text>().text = "";
            }
            CreatePageArrowArtwork(previous.transform, false);
            CreatePageArrowArtwork(next.transform, true);

            var dim = Panel("藏品页压黑", panel, new Vector2(932, 500), Vector2.zero, new Color(0, 0, 0, .62f)).GetComponent<Image>();
            StretchCollectionDimLayer(panel);
            dim.raycastTarget = true;
            dim.transform.SetAsLastSibling();
            var details = new GameObject("单个藏品展示", typeof(RectTransform)).transform;
            details.SetParent(panel, false);
            var detailRect = (RectTransform)details;
            detailRect.anchorMin = detailRect.anchorMax = new Vector2(.5f, .5f);
            detailRect.sizeDelta = new Vector2(690, 430);
            var detailBack = Button("返回藏品页", details, "返回", new Vector2(-292, 185), new Vector2(98, 38));
            detailBack.GetComponent<Image>().color = new Color(.33f, .19f, .10f, .88f);
            var detailName = Label("藏品名", details, "", new Vector2(0, 180), new Vector2(430, 42), 28, new Color(.96f, .89f, .70f));
            var detailArtwork = Panel("放大藏品图", details, new Vector2(245, 235), new Vector2(0, 30), Color.white).GetComponent<Image>();
            detailArtwork.preserveAspect = true;
            detailArtwork.raycastTarget = false;
            var detailDescription = Label("藏品介绍", details, "", new Vector2(0, -156), new Vector2(560, 70), 21, new Color(.96f, .89f, .70f));
            detailDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailDescription.verticalOverflow = VerticalWrapMode.Overflow;
            details.SetAsLastSibling();

            var view = panel.gameObject.AddComponent<AtelierCollectionView>();
            view.Configure(returnButton, previous, next, leftSlots, rightSlots, template, details.gameObject, dim,
                detailBack, detailArtwork, detailName, detailDescription, LoadNotebookSprite(CollectionBrushArtworkPath));
            details.gameObject.SetActive(false);
            dim.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>压黑层始终随藏品根节点拉伸，手调整体尺寸时不会留下未遮住的页边。</summary>
        private static void StretchCollectionDimLayer(Transform collectionPanel)
        {
            var dim = collectionPanel.Find("藏品页压黑") as RectTransform;
            if (dim == null) return;
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.pivot = new Vector2(.5f, .5f);
            dim.offsetMin = Vector2.zero;
            dim.offsetMax = Vector2.zero;
        }

        /// <summary>建立一页中的六个格位容器；每半页为两列三行。</summary>
        private static Transform CreateCollectionSlotGrid(string name, Transform parent, Vector2 position)
        {
            var grid = Panel(name, parent, new Vector2(350, 350), position, Color.clear);
            grid.GetComponent<Image>().raycastTarget = false;
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            layout.cellSize = new Vector2(148, 116);
            layout.spacing = new Vector2(18, 0);
            layout.padding = new RectOffset(18, 18, 1, 1);
            layout.childAlignment = TextAnchor.MiddleCenter;
            return grid;
        }

        /// <summary>从同一张生成图中裁出左右各半，作为可点击而不带文字的翻页箭头。</summary>
        private static void CreatePageArrowArtwork(Transform parent, bool right)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CollectionArrowsPath);
            var go = new GameObject("箭头美术", typeof(RectTransform), typeof(RawImage));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.uvRect = new Rect(right ? .5f : 0f, 0f, .5f, 1f);
            image.raycastTarget = false;
        }

        /// <summary>确保点击开局便签后有独立阅读纸面；该纸面不属于笔记本 Prefab。</summary>
        private static void EnsureManuscriptPanel(Transform canvas, AtelierGameplay game)
        {
            var existing = canvas.Find("便签阅读");
            if (existing != null)
            {
                game.ManuscriptPanel = existing.gameObject;
                // 便签只能由玩家点击桌面后显现，场景刷新不能让它常驻。
                existing.gameObject.SetActive(false);
                return;
            }

            var panel = Panel("便签阅读", canvas, new Vector2(560, 332), new Vector2(0, 4), new Color(.95f, .88f, .68f, .97f));
            Label("便签标题", panel, "桌上的便签", new Vector2(0, 118), new Vector2(420, 38), 28, Ink);
            Label("便签正文", panel, "明早交稿。\n\n想不出题目，就画眼前这些。\n杯子，笔，灯，再带一点窗外的夜色。", new Vector2(0, 5), new Vector2(440, 205), 20, Ink);
            Label("便签提示", panel, "右键或右上角“返回画室”收起", new Vector2(0, -133), new Vector2(440, 28), 14, new Color(.37f, .31f, .22f));
            game.ManuscriptPanel = panel.gameObject;
            panel.gameObject.SetActive(false);
        }

        /// <summary>按原场景规则重建调查浮层与返回按钮，它们不属于任何菜单 Prefab。</summary>
        private static void RestoreOriginalContextAndReturn(Transform canvas, AtelierGameplay game)
        {
            foreach (string name in new[] { "调查与动作", "返回画室" })
            {
                var existing = canvas.Find(name);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);
            }
            var context = Panel("调查与动作", canvas, new Vector2(440, 100), new Vector2(-385, 277), MenuBrown);
            context.GetComponent<Image>().raycastTarget = false;
            game.ObservationText = Label("观察描述", context, "", new Vector2(-40, 12), new Vector2(330, 75), 19, new Color(.93f, .89f, .78f));
            game.ContextAction = Button("当前动作", context, "", new Vector2(155, -15), new Vector2(110, 42));
            game.ContextAction.GetComponentInChildren<Text>().color = new Color(.94f, .89f, .78f);
            game.ContextPanel = context.gameObject;
            game.ReturnButton = Button("返回画室", canvas, "返回画室", new Vector2(532, 316), new Vector2(150, 42));
            game.ReturnButton.GetComponentInChildren<Text>().color = new Color(.94f, .9f, .8f);
        }

        /// <summary>只将暂停菜单作为单独 Prefab；调查和返回保持场景原生绑定。</summary>
        private static void EnsurePauseMenuPrefab(Transform canvas, AtelierGameplay game)
        {
            // 旧版本先在场景内生成了同名面板，导致之后编辑的 Prefab 从未被运行时引用。
            // 兼容资产改名前后的名称，并始终以 PauseMenuPanel.prefab 作为暂停菜单的唯一来源。
            var pause = canvas.Find("暂停菜单") ?? canvas.Find("PauseMenuPanel");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuPrefabPath);
            if (prefab != null)
            {
                EnsurePausePrefabEditable();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuPrefabPath);
                // 场景曾保存过 390 × 290 的 Prefab Override；该 Override 比 Prefab 中的人工尺寸优先，
                // 即使已经连上同一个资产也会吞掉新的宽高。因此每次这个专用同步命令都重建实例。
                if (pause != null)
                {
                    Object.DestroyImmediate(pause.gameObject);
                    pause = null;
                }
                pause = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas)).transform;
            }
            if (pause == null)
            {
                pause = Panel("暂停菜单", canvas, new Vector2(390, 290), Vector2.zero, MenuBrown);
                var resume = Button("继续", pause, "继续", new Vector2(0, 78), new Vector2(230, 44));
                var restart = Button("重新开始", pause, "重新开始", new Vector2(0, 18), new Vector2(230, 44));
                var quit = Button("退出", pause, "退出", new Vector2(0, -42), new Vector2(230, 44));
                foreach (var button in new[] { resume, restart, quit }) button.GetComponentInChildren<Text>().color = new Color(.94f, .90f, .80f);
                Label("操作说明", pause, "按住右键或空格转头 · 滚轮看近\nN 打开笔记 · 近看时右键返回", new Vector2(0, -111), new Vector2(375, 58), 16, new Color(.84f, .82f, .75f));
            }
            ApplyPauseMenuAppearance(pause);
            game.PausePanel = pause.gameObject;
            game.ResumeButton = pause.Find("继续").GetComponent<Button>();
            game.RestartButton = pause.Find("重新开始").GetComponent<Button>();
            game.QuitButton = pause.Find("退出").GetComponent<Button>();
            pause.gameObject.SetActive(false);
            if (prefab != null) return;
            const string prefabsFolder = "Assets/_Game/Content/Prefabs";
            const string uiFolder = prefabsFolder + "/UI";
            if (!AssetDatabase.IsValidFolder(prefabsFolder)) AssetDatabase.CreateFolder("Assets/_Game/Content", "Prefabs");
            if (!AssetDatabase.IsValidFolder(uiFolder)) AssetDatabase.CreateFolder(prefabsFolder, "UI");
            // Prefab 资产保持启用，才能在 Prefab Mode 直接看见并调整；场景实例由运行时 Start 隐藏。
            pause.gameObject.SetActive(true);
            if (PrefabUtility.SaveAsPrefabAssetAndConnect(pause.gameObject, PauseMenuPrefabPath, InteractionMode.AutomatedAction) == null)
                throw new IOException("暂停菜单 Prefab 创建失败。");
            pause.gameObject.SetActive(false);
        }

        /// <summary>修正旧版本被保存为禁用状态的暂停菜单 Prefab，使其可在 Prefab Mode 中直接编辑。</summary>
        private static void EnsurePausePrefabEditable()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(PauseMenuPrefabPath);
            try
            {
                prefabRoot.SetActive(true);
                prefabRoot.name = "暂停菜单";
                ApplyPauseMenuAppearance(prefabRoot.transform);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PauseMenuPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>暂停面板使用复古框背景，所有非按钮图形不拦截场景射线。</summary>
        private static void ApplyPauseMenuAppearance(Transform pause)
        {
            var frame = LoadNotebookSprite(PauseMenuBackgroundPath);
            if (frame == null) throw new InvalidOperationException("暂停菜单复古框素材未导入：" + PauseMenuBackgroundPath);
            var background = pause.GetComponent<Image>();
            background.sprite = frame;
            background.type = Image.Type.Simple;
            background.color = Color.white;
            foreach (var image in pause.GetComponentsInChildren<Image>(true))
                if (image.GetComponent<Button>() == null) image.raycastTarget = false;
        }

        /// <summary>统一暂停与调查菜单为笔记本同系的暖深棕底色，不触及可手调笔记 Prefab。</summary>
        private static void ApplyMenuPalette(Transform canvas)
        {
            foreach (string name in new[] { "暂停菜单", "调查与动作" })
            {
                var panel = canvas.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
                if (panel == null) continue;
                var image = panel.GetComponent<Image>();
                if (image != null) image.color = MenuBrown;
            }
        }

        private static AudioSource Sound(Transform parent,string name,float volume)
        {
            // 首阶段不拿电子提示音冒充洗笔或纸页 Foley，保留显式声源供后续录音绑定。
            var audio=Group(name,parent).gameObject.AddComponent<AudioSource>();audio.playOnAwake=false;audio.volume=volume;return audio;
        }
        /// <summary>创建一个只承载同词性词条的可滚动纸面列；滚动和按钮仅是 UI 临时状态。</summary>
        private static Transform WordColumn(string name,Transform parent,Vector2 size,Vector2 at)
        {
            var root=Panel(name,parent,size,at,new Color(.98f,.94f,.82f,.40f));
            var viewport=Panel("视窗",root,size-new Vector2(14,12),new Vector2(-5,0),Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=Panel("词条内容",viewport,new Vector2(size.x-18,0),Vector2.zero,Color.clear);
            content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=new Vector2(-4,0);
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.spacing=3;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.content=content;scroll.viewport=viewport;scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=24;scroll.movementType=ScrollRect.MovementType.Clamped;
            return content;
        }
        private static RectTransform Panel(string name,Transform parent,Vector2 size,Vector2 at,Color colour)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=size;rect.anchoredPosition=at;
            var image=go.GetComponent<Image>();image.color=colour;image.raycastTarget=colour.a>.3f;return rect;
        }
        private static Text Label(string name,Transform parent,string value,Vector2 at,Vector2 size,int fontSize,Color colour)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=at;rect.sizeDelta=size;
            var text=go.GetComponent<Text>();text.font=font;text.text=value;text.fontSize=fontSize;text.color=colour;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;
        }
        private static Button Button(string name,Transform parent,string value,Vector2 at,Vector2 size)
        {
            var rect=Panel(name,parent,size,at,new Color(.52f,.47f,.35f,.09f));var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.targetGraphic.raycastTarget=true;
            Label("文字",rect,value,Vector2.zero,size,22,Ink);return button;
        }
        private static Transform Group(string name,Transform parent)
        {
            var go=new GameObject(name);if(parent!=null)go.transform.SetParent(parent,false);return go.transform;
        }
        private static MeshRenderer MeshNode(string name,Transform parent,Mesh mesh,Material material)
        {
            var go=Group(name,parent);go.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;return renderer;
        }
        private static Mesh SaveMesh(string name,Mesh mesh)
        {
            mesh.name=name;mesh.RecalculateBounds();mesh.RecalculateTangents();Save(mesh,"Meshes/"+name+".asset");return mesh;
        }
        private static void Save(Object asset,string path) => AssetDatabase.CreateAsset(asset,Content+path);
        private static Mesh Disc(float radius,int across,int rings)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int r=0;r<=rings;r++)for(int i=0;i<=across;i++)
            {
                float angle=i/(float)across*Mathf.PI*2;Vector2 p=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(r/(float)rings);
                vertices.Add(new Vector3(p.x*radius,0,p.y*radius));uv.Add(p*.5f+Vector2.one*.5f);
                if(r==rings||i==across)continue;int k=r*(across+1)+i;
                triangles.AddRange(new[]{k,k+1,k+across+2,k,k+across+2,k+across+1});
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return SaveMesh("杯内细分液面",mesh);
        }
    }
}
