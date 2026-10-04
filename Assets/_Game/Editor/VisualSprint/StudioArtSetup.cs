// =============================================================================
// 文件：StudioArtSetup.cs
// 职责：以许可明确的小模型、真实表面纹理与手作细节重构艺术家工作室。
// 边界：只增量替换装饰和 Hero Visual，不重建场景，不改实体、碰撞或领域规则。
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
    /// <summary>第五阶段具体工作室构图和资产安装器；前景、中景、背景分别组织。</summary>
    public static class StudioArtSetup
    {
        private const string Art = "Assets/_Game/Content/Art/Studio/";
        private const string Prefabs = "Assets/_Game/Content/Prefabs/Environment/VisualSprint/";
        private const string Source = "Assets/ThirdParty/Kenney/FurnitureKit/";
        private static Material floor, wall, darkWood, warmWood, paleWood, paper, canvas, fabric, brass, ceramic, glass, pigment;
        private static Material[] paintColors;
        private static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, GameObject> modelCache = new Dictionary<string, GameObject>();

        /// <summary>增量重构已保存的主场景，重复执行只替换本工具拥有的装饰分组。</summary>
        [MenuItem("游戏/第五阶段/重构艺术家工作室")]
        public static void Apply()
        {
            var room = PresentationArtPass.Room();
            Prepare();
            var environment = room.Find("Environment");
            var oldDecor = environment.Find("Decoration");
            if (oldDecor != null) oldDecor.gameObject.SetActive(false);
            var oldProps = environment.Find("StudioProps");
            if (oldProps != null) oldProps.gameObject.SetActive(false);
            var studio = Recreate(environment, "ArtistStudio_Visual");
            var foreground = Group(studio, "Foreground");
            var workshop = Group(studio, "WorkSurface");
            var storage = Group(studio, "BackgroundStorage");
            var studies = Group(studio, "WallAndCanvasStudies");
            ApplySurfaces(room);
            ReplaceFurniture(environment, storage);
            DressForeground(foreground);
            DressWorktable(workshop);
            DressStorage(storage, studies);
            AttachWallStudies(room, studies);
            DressPainting(room);
            DressWindow(room);
            // 房间中的封皮与拿近后的布面书保持一致，替换视觉不改书的碰撞和身份。
            var cover = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Content/Art/Inspection/Book/M_BookCloth.mat");
            if (cover != null)
                foreach (var renderer in room.GetComponentInChildren<BookPresenter>(true).GetComponentsInChildren<Renderer>(true))
                    if (renderer.name != "Pages") renderer.sharedMaterial = cover;
            var plant = room.GetComponentInChildren<PlantPresenter>(true);
            plant.transform.localPosition = new Vector3(-.65f, 0, 1.40f);
            room.GetComponentInChildren<Camera>(true).fieldOfView = 57;
            foreach (var renderer in studio.GetComponentsInChildren<Renderer>(true))
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
            PresentationArtPass.Save(room);
            Debug.Log("工作室美术：已完成三层构图、纹理表面、桌面创作细节、实体画框与窗帘。");
        }

        /// <summary>统一导入参数与材质库，不创建下载检验框架或复制外部默认材质。</summary>
        private static void Prepare()
        {
            Directory.CreateDirectory(Art + "Materials");
            Directory.CreateDirectory(Art + "Models");
            Directory.CreateDirectory(Art + "Textures");
            Directory.CreateDirectory(Prefabs);
            AssetDatabase.Refresh();
            foreach (string path in Directory.GetFiles(Source, "*.fbx"))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                if (importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importAnimation && importer.isReadable) continue;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            var woodDiffuse = ImportTexture("OldWoodFloor", "Diffuse", false);
            var woodNormal = ImportTexture("OldWoodFloor", "nor_gl", true);
            var plasterDiffuse = ImportTexture("PaintedPlasterWall", "Diffuse", false);
            var plasterNormal = ImportTexture("PaintedPlasterWall", "nor_gl", true);
            var linenDiffuse = ImportTexture("RoughLinen", "Diffuse", false);
            var linenNormal = ImportTexture("RoughLinen", "nor_gl", true);
            floor = Material("M_StudioFloor", new Color(.72f, .62f, .47f), .30f, woodDiffuse, woodNormal, new Vector2(4, 6));
            wall = Material("M_StudioPlaster", new Color(.84f, .81f, .74f), .12f, plasterDiffuse, plasterNormal, new Vector2(2.5f, 2));
            darkWood = Material("M_Walnut", new Color(.56f, .46f, .34f), .31f, woodDiffuse, woodNormal, Vector2.one);
            warmWood = Material("M_WornOak", new Color(.56f, .41f, .25f), .26f, woodDiffuse, woodNormal, Vector2.one);
            paleWood = Material("M_PaintedIvory", new Color(.74f, .69f, .58f), .22f, plasterDiffuse, plasterNormal, Vector2.one);
            paper = Material("M_UsedPaper", new Color(.88f, .82f, .67f), .14f, linenDiffuse, linenNormal, Vector2.one);
            canvas = Material("M_Canvas", new Color(.89f, .87f, .78f), .09f, linenDiffuse, linenNormal, new Vector2(3, 3));
            fabric = Material("M_LinenCurtain", new Color(.67f, .58f, .43f), .09f, linenDiffuse, linenNormal, new Vector2(3, 5));
            brass = Material("M_AgedBrass", new Color(.48f, .31f, .13f), .66f);
            brass.SetFloat("_Metallic", .7f);
            ceramic = Material("M_StudioCeramic", new Color(.37f, .45f, .37f), .58f);
            glass = Material("M_DriedBottle", new Color(.15f, .23f, .18f), .81f);
            pigment = Material("M_DriedInk", new Color(.14f, .12f, .09f), .15f);
            paintColors = new[] {
                Material("M_PigmentOchre", new Color(.58f, .34f, .08f), .26f),
                Material("M_PigmentVermilion", new Color(.46f, .115f, .08f), .24f),
                Material("M_PigmentViridian", new Color(.075f, .25f, .19f), .24f),
                Material("M_PigmentIndigo", new Color(.10f, .16f, .24f), .24f)
            };
            meshCache.Clear();
            modelCache.Clear();
        }

        /// <summary>正确标记法线贴图并保留可重复的纹理坐标，避免将 RGB 法线当颜色使用。</summary>
        private static Texture2D ImportTexture(string folder, string name, bool normal)
        {
            string path = "Assets/ThirdParty/PolyHaven/" + folder + "/" + name + ".jpg";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.convertToNormalmap = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>地板、独立建筑和旧踢脚线采用真实统一表面，保持模块可供星空溶解消费。</summary>
        private static void ApplySurfaces(Transform room)
        {
            var modules = room.GetComponentInChildren<RoomModules>(true);
            foreach (var renderer in modules.Floor.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = floor;
            foreach (var part in new[] { modules.Ceiling, modules.WallBack, modules.WallLeft, modules.WallRight })
                foreach (var renderer in part.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = wall;
            foreach (var renderer in modules.FloorDetails.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = darkWood;
                if (renderer.name.StartsWith("FloorJoint")) renderer.gameObject.SetActive(false);
            }
            var shellRug = room.Find("Environment/RoomShell/Rug");
            if (shellRug != null) shellRug.gameObject.SetActive(false);
        }

        /// <summary>替换原桌椅书架的纯视觉，旧根节点与所有玩法绑定继续存在。</summary>
        private static void ReplaceFurniture(Transform environment, Transform storage)
        {
            var shelf = environment.Find("BookShelf");
            HideChildren(shelf);
            PlaceModel("Bookcase", shelf, "bookcaseOpen", 2.24f, darkWood, Vector3.zero, 0, 2.16f);
            var table = environment.Find("Table");
            HideChildren(table);
            table.localPosition = new Vector3(2.86f, 0, -.35f);
            table.localRotation = Quaternion.Euler(0, -8, 0);
            PlaceModel("WritingDesk", table, "desk", .92f, warmWood, Vector3.zero, 0, 1.90f);
            var chair = environment.Find("Chair");
            HideChildren(chair);
            chair.localPosition = new Vector3(3.25f, 0, -1.30f);
            PlaceModel("StudioChair", chair, "chairRounded", 1.12f, warmWood, Vector3.zero, 23);
            PlaceModel("SideCabinet", storage, "cabinetBedDrawer", .83f, darkWood, new Vector3(-3.25f, 0, 1.18f), 90);
            PlaceModel("NarrowBookcase", storage, "bookcaseOpenLow", 1.18f, warmWood, new Vector3(3.39f, 0, 2.38f), -90);
        }

        /// <summary>左侧桌沿、椅背和靠近镜头的画布形成不遮住 Hero 的前景层。</summary>
        private static void DressForeground(Transform parent)
        {
            PlaceModel("ForegroundDraftDesk", parent, "deskCorner", .87f, darkWood, new Vector3(-3.04f, 0, -1.60f), 26, 2.0f);
            PlaceModel("ForegroundChairBack", parent, "chair", 1.12f, warmWood, new Vector3(-1.98f, 0, -2.07f), -16);
            PlaceModel("WovenRug", parent, "rugRectangle", .032f, RugMaterial(), new Vector3(-.15f, .015f, -.10f), 13, 2.55f);
            CanvasStudy("NearCanvasEdge", parent, new Vector3(-3.28f, .96f, -.82f), new Vector3(-6, 31, -5), .74f, 1.46f, 0);
            CanvasStudy("NearEmptyFrame", parent, new Vector3(-3.62f, .66f, -.31f), new Vector3(-9, 34, 3), .72f, 1.02f, -1);
            CanvasStudy("CanvasStackFront", parent, new Vector3(-3.48f, .83f, .22f), new Vector3(-11, 17, -4), .77f, 1.32f, 1);
            PaperSheet("ForegroundStudy", parent, new Vector3(-2.67f, .89f, -1.30f), new Vector3(0, 32, 0), .57f, .44f, 1);
            var cloth = MeshNode("DraftCloth", parent, ClothMesh(.78f, .50f, .11f), fabric);
            cloth.localPosition = new Vector3(-2.90f, .91f, -1.70f);
            cloth.localRotation = Quaternion.Euler(0, 26, 0);
        }

        /// <summary>创作工具成簇落在右侧工作台，而不是围绕玩家平均铺开。</summary>
        private static void DressWorktable(Transform parent)
        {
            Vector3 origin = new Vector3(2.84f, .96f, -.35f);
            PlaceModel("UsedReferenceBooks", parent, "books", .19f, paper, origin + new Vector3(.55f, 0, .10f), 18, .50f);
            for (int i = 0; i < 3; i++)
                PaperSheet("DeskDraft" + i, parent, origin + new Vector3(-.37f + i * .06f, .01f + i * .004f, -.10f + i * .07f), new Vector3(0, -13 + i * 8, 0), .49f, .37f, i);
            Cup("BrushJar", parent, origin + new Vector3(.40f, 0, -.34f), .11f, .23f, ceramic);
            for (int i = 0; i < 5; i++)
            {
                var brush = Group(parent, "BrushInJar" + i);
                brush.localPosition = origin + new Vector3(.40f + (i - 2) * .018f, .11f, -.34f);
                brush.localRotation = Quaternion.Euler(-12 + i * 5, i * 61, -14 + i * 7);
                Beam("Handle", brush, new Vector3(0, .17f, 0), new Vector3(.011f, .37f, .011f), warmWood, .003f);
                Beam("Ferrule", brush, new Vector3(0, .373f, 0), new Vector3(.018f, .043f, .016f), brass, .003f);
                Beam("Bristle", brush, new Vector3(0, .408f, 0), new Vector3(.013f, .038f, .012f), pigment, .003f);
            }
            Cup("OldTeaCup", parent, origin + new Vector3(-.72f, 0, .20f), .105f, .15f, paleWood);
            Bottle("LinseedBottle", parent, origin + new Vector3(.76f, 0, .22f), .09f, .29f);
            Bottle("PigmentBottle", parent, origin + new Vector3(.59f, 0, .31f), .065f, .22f);
            var palette = MeshNode("UsedPalette", parent, OvalMesh(.28f, .17f, .025f), warmWood);
            palette.localPosition = origin + new Vector3(-.05f, .008f, -.33f);
            palette.localRotation = Quaternion.Euler(0, 22, 0);
            for (int i = 0; i < 4; i++)
            {
                var patch = MeshNode("PalettePigment" + i, palette, OvalMesh(.039f, .026f, .004f), paintColors[i]);
                patch.localPosition = new Vector3(-.16f + i * .085f, .014f, -.02f + Mathf.Sin(i * 2) * .05f);
            }
            for (int i = 0; i < 5; i++) PaintTube("UsedPaintTube" + i, parent, origin + new Vector3(-.23f + i * .115f, .02f, .27f), 12 + i * 29, paintColors[i % 4]);
            PlaceModel("DeskLamp", parent, "lampRoundTable", .53f, brass, origin + new Vector3(.69f, 0, -.05f), -17);
            PointLight(parent, "DeskPoolLight", origin + new Vector3(.60f, .45f, -.04f), new Color(1, .69f, .34f), 1.45f, 3.5f);
            var paperStack = Group(parent, "CutPaperStack");
            paperStack.localPosition = origin + new Vector3(-.69f, 0, -.23f);
            for (int i = 0; i < 5; i++)
                Beam("Sheet" + i, paperStack, new Vector3(i % 2 * .008f, .003f + i * .006f, i % 3 * .005f), new Vector3(.31f, .004f, .39f), paper, .002f);
        }

        /// <summary>书架、墙上旧画、纸箱与草稿承载长期创作的背景痕迹。</summary>
        private static void DressStorage(Transform parent, Transform studies)
        {
            for (int i = 0; i < 5; i++)
                PlaceModel("ShelfBooks" + i, parent, "books", .30f + (i % 2) * .07f, paper,
                    new Vector3(-2.68f + (i % 3) * .43f, i < 3 ? .53f : 1.15f, 2.67f), i * 7, .37f);
            PlaceModel("DraftStorageBox", parent, "cardboardBoxClosed", .47f, warmWood, new Vector3(-3.17f, 0, -.04f), 14);
            PlaceModel("OpenPaperBox", parent, "cardboardBoxOpen", .40f, paper, new Vector3(-2.79f, 0, .21f), -22);
            PlaceModel("StoredSketchesBox", parent, "cardboardBoxClosed", .35f, darkWood, new Vector3(3.19f, 0, 1.32f), 9);
            PlaceModel("FloorReadingLamp", parent, "lampRoundFloor", 1.95f, brass, new Vector3(-3.30f, 0, 2.73f), 4);
            PointLight(parent, "ReadingPoolLight", new Vector3(-3.25f, 1.85f, 2.45f), new Color(1, .72f, .43f), .82f, 4.1f);
            PlaceModel("CoatAndApronStand", parent, "coatRackStanding", 1.88f, darkWood, new Vector3(3.57f, 0, .32f), 19);
            var apron = MeshNode("ForgottenApron", parent, ClothMesh(.48f, .90f, .05f), fabric);
            apron.localPosition = new Vector3(3.45f, 1.06f, .26f);
            apron.localRotation = Quaternion.Euler(85, -15, 0);
            CanvasStudy("UnfinishedWallStudy", studies, new Vector3(-3.17f, 2.86f, 3.72f), Vector3.zero, .87f, .78f, 2);
            CanvasStudy("OldWallStudy", studies, new Vector3(-1.92f, 2.95f, 3.72f), new Vector3(0, 0, -4), .71f, .88f, 1);
            CanvasStudy("AbandonedLargeCanvas", studies, new Vector3(3.66f, .86f, 1.49f), new Vector3(-8, -68, -4), .74f, 1.33f, 0);
            CanvasStudy("LeaningEmptyFrame", studies, new Vector3(-3.71f, .60f, 1.61f), new Vector3(-6, 61, 4), .65f, 1.08f, -1);
            for (int i = 0; i < 4; i++)
                PaperSheet("DiscardedFloorStudy" + i, studies, new Vector3(-1.77f - i * .17f, .027f + i * .004f, .37f - i * .20f), new Vector3(0, -28 + i * 27, 0), .25f, .35f, i);
            for (int i = 0; i < 3; i++)
            {
                var scrap = MeshNode("CrumpledPaper" + i, studies, CrumpledMesh(i), paper);
                scrap.localPosition = new Vector3(2.60f + i * .24f, .065f, -.95f + i * .23f);
                scrap.localRotation = Quaternion.Euler(i * 27, i * 73, 11);
            }
            for (int i = 0; i < 3; i++)
            {
                var sketch = PaperSheet("PinnedSketch" + i, studies, new Vector3(-3.87f, 1.64f + i * .32f, .13f + i * .25f), new Vector3(90, 90, 0), .33f, .40f, i);
                Beam("Pin", sketch, new Vector3(0, .008f, .14f), new Vector3(.013f, .017f, .013f), brass, .004f);
            }
        }

        /// <summary>墙上装饰随对应建筑边界消散，不把旧画留成无支撑的悬空道具。</summary>
        private static void AttachWallStudies(Transform room, Transform studies)
        {
            var modules = room.GetComponentInChildren<RoomModules>(true);
            var back = Recreate(modules.WallBack, "ArtistWallStudies");
            foreach (string name in new[] { "UnfinishedWallStudy", "OldWallStudy" })
                studies.Find(name).SetParent(back, true);
            var left = Recreate(modules.WallLeft, "ArtistPinnedStudies");
            for (int i = 0; i < 3; i++) studies.Find("PinnedSketch" + i).SetParent(left, true);
        }

        /// <summary>英雄画增加多层框线、真实画布厚度与完整支架，不替换海面绑定。</summary>
        private static void DressPainting(Transform room)
        {
            var presenter = room.GetComponentInChildren<PaintingPresenter>(true);
            var visual = presenter.transform.Find("Visual");
            foreach (Transform child in visual)
                if (child.name.StartsWith("Frame") || child.name.StartsWith("Gilt") || child.name.StartsWith("Support") || child.name.StartsWith("Easel")) child.gameObject.SetActive(false);
            var details = Recreate(visual, "ArtistPaintingFrame");
            Frame(details, "FrameOuter", 1.28f, 1.47f, .11f, .14f, -.035f, darkWood);
            Frame(details, "FrameMoulding", 1.19f, 1.38f, .042f, .09f, -.105f, warmWood);
            Frame(details, "GiltInner", 1.115f, 1.295f, .012f, .028f, -.098f, brass);
            Beam("CanvasBacking", details, new Vector3(0, 0, .09f), new Vector3(1.16f, 1.35f, .085f), canvas, .015f);
            Beam("EaselMast", details, new Vector3(0, -.54f, .20f), new Vector3(.10f, 2.35f, .11f), warmWood, .013f);
            var left = Beam("EaselFootLeft", details, new Vector3(-.39f, -1.07f, .12f), new Vector3(.075f, 1.56f, .095f), warmWood, .012f);
            left.localRotation = Quaternion.Euler(0, 0, -12);
            var right = Beam("EaselFootRight", details, new Vector3(.39f, -1.07f, .12f), new Vector3(.075f, 1.56f, .095f), warmWood, .012f);
            right.localRotation = Quaternion.Euler(0, 0, 12);
            var rear = Beam("EaselRearLeg", details, new Vector3(0, -1.08f, .39f), new Vector3(.065f, 1.58f, .085f), darkWood, .011f);
            rear.localRotation = Quaternion.Euler(-20, 0, 0);
            Beam("EaselRest", details, new Vector3(0, -.765f, .0f), new Vector3(1.48f, .09f, .28f), darkWood, .018f);
            Beam("EaselUpperClamp", details, new Vector3(0, .77f, -.025f), new Vector3(.29f, .09f, .15f), darkWood, .012f);
        }

        /// <summary>窗户具有窗深、厚框、窗台和褶皱布；新增边界全部位于既有 Visual 内。</summary>
        private static void DressWindow(Transform room)
        {
            var window = room.GetComponentInChildren<WindowPresenter>(true).transform.Find("Visual");
            var oldFrame = window.Find("Frame");
            if (oldFrame != null) oldFrame.gameObject.SetActive(false);
            var oldCurtain = window.Find("Curtains");
            if (oldCurtain != null) oldCurtain.gameObject.SetActive(false);
            var details = Recreate(window, "ArtistWindowFrame");
            Frame(details, "FrameOuter", 2.92f, 2.51f, .14f, .32f, -.025f, paleWood);
            Frame(details, "FrameInner", 2.74f, 2.32f, .045f, .22f, -.15f, warmWood);
            Beam("DeepWindowSill", details, new Vector3(0, -1.26f, -.18f), new Vector3(3.12f, .10f, .56f), paleWood, .025f);
            Beam("UpperLintel", details, new Vector3(0, 1.31f, -.12f), new Vector3(3.03f, .075f, .31f), warmWood, .020f);
            Beam("WindowMullion", details, new Vector3(0, 0, -.09f), new Vector3(.045f, 2.23f, .13f), paleWood, .009f);
            Beam("CurtainRail", details, new Vector3(0, 1.45f, -.32f), new Vector3(3.49f, .025f, .032f), brass, .008f);
            var curtains = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                curtains[i] = MeshNode("CurtainSilk_" + (i == 0 ? "Left" : "Right"), details, CurtainMesh(.42f, 2.49f), fabric);
                curtains[i].localPosition = new Vector3(i == 0 ? -1.48f : 1.48f, .045f, -.29f);
            }
            var wind = room.GetComponentInChildren<WindPresenter>(true);
            var serialized = new SerializedObject(wind);
            var property = serialized.FindProperty("papers");
            var papers = Enumerable.Range(0, property.arraySize).Select(i => property.GetArrayElementAtIndex(i).objectReferenceValue as Transform)
                .Where(x => x != null && !x.name.StartsWith("Curtain")).Concat(curtains).ToArray();
            wind.Configure((Transform)serialized.FindProperty("plantVisual").objectReferenceValue, papers, wind.WindAudio);
        }

        /// <summary>导入模型经自有 Wrapper 统一高度与坐标，所有外部碰撞都会删除。</summary>
        private static Transform PlaceModel(string name, Transform parent, string source, float height, Material material, Vector3 position, float yaw, float width = 0)
        {
            string key = source + "_" + Mathf.RoundToInt(height * 1000) + "_" + material.name;
            if (!modelCache.TryGetValue(key, out var prefab))
            {
                var wrapper = new GameObject("PF_Artist_" + key);
                var visual = Group(wrapper.transform, "Visual");
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Source + source + ".fbx");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                instance.transform.SetParent(visual, false);
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                int meshIndex = 0;
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
                    filter.sharedMesh = SurfaceUvMesh(key + "_" + meshIndex++, filter.sharedMesh);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    var shared = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                    for (int i = 0; i < shared.Length; i++)
                        shared[i] = source.StartsWith("lamp") && i > 0 ? paper : source == "books" && i > 0 ? paintColors[(i - 1) % paintColors.Length] : material;
                    renderer.sharedMaterials = shared;
                }
                float scale = height / Mathf.Max(.001f, bounds.size.y);
                visual.localScale = Vector3.one * scale;
                visual.localPosition = -bounds.center * scale + Vector3.up * height / 2;
                foreach (var collider in wrapper.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, Prefabs + wrapper.name + ".prefab");
                Object.DestroyImmediate(wrapper);
                modelCache.Add(key, prefab);
            }
            var previous = parent.Find(name);
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = name;
            model.transform.SetParent(parent, false);
            model.transform.localPosition = position;
            if (width > 0)
            {
                // 静态家具只校准宽度，模型高度与落地位置仍由 Wrapper 明确保证。
                var renderers = model.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                model.transform.localScale = new Vector3(width / Mathf.Max(.01f, bounds.size.x), 1, 1);
            }
            model.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return model.transform;
        }

        /// <summary>将色板式低模的 UV 改成真实表面坐标，让统一木纹与织物在家具上可见。</summary>
        private static Mesh SurfaceUvMesh(string name, Mesh original)
        {
            string path = Art + "Models/Surface_" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool create = mesh == null;
            if (create) mesh = new Mesh { name = "Surface_" + name };
            mesh.Clear();
            var positions = original.vertices;
            var normals = original.normals;
            var bounds = original.bounds;
            var uv = new Vector2[positions.Length];
            for (int i = 0; i < uv.Length; i++)
            {
                // 各个面选自己的主要平面，不沿用只采样色板单点的原 UV。
                Vector3 p = positions[i] - bounds.min, n = normals[i];
                p = new Vector3(p.x / Mathf.Max(.001f, bounds.size.x), p.y / Mathf.Max(.001f, bounds.size.y), p.z / Mathf.Max(.001f, bounds.size.z));
                Vector3 axis = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                uv[i] = axis.y >= axis.x && axis.y >= axis.z ? new Vector2(p.x, p.z)
                    : axis.x >= axis.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
            }
            mesh.vertices = positions; mesh.normals = normals; mesh.uv = uv;
            mesh.subMeshCount = original.subMeshCount;
            for (int i = 0; i < mesh.subMeshCount; i++) mesh.SetTriangles(original.GetTriangles(i), i);
            mesh.RecalculateTangents(); mesh.RecalculateBounds(); mesh.UploadMeshData(false);
            if (create) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>原创旧织毯：细密经纬、褪色边框与低对比几何纹，不让前景成为纯色大块。</summary>
        private static Material RugMaterial()
        {
            string path = Art + "Textures/WornRug.png";
            if (!File.Exists(path))
            {
                var image = new Texture2D(512, 512, TextureFormat.RGB24, false);
                for (int y = 0; y < 512; y++)
                    for (int x = 0; x < 512; x++)
                    {
                        float u = x / 511f, v = y / 511f;
                        float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
                        float thread = ((x + y) % 2 == 0 ? .94f : 1.04f) * (.88f + .18f * Mathf.PerlinNoise(x * .14f, y * .12f));
                        Color shade = new Color(.46f, .40f, .30f);
                        if (edge < .08f) shade = new Color(.29f, .32f, .31f);
                        if (edge > .025f && edge < .035f || edge > .07f && edge < .083f) shade = new Color(.68f, .57f, .40f);
                        float diamond = Mathf.Abs(Mathf.Repeat(u * 8, 1) - .5f) + Mathf.Abs(Mathf.Repeat(v * 5, 1) - .5f);
                        if (edge > .1f && diamond < .19f) shade = Color.Lerp(shade, new Color(.35f, .39f, .37f), .6f);
                        image.SetPixel(x, y, shade * thread);
                    }
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                AssetDatabase.ImportAsset(path);
            }
            // 单个模型的 UV 已投影归一，纹样铺满整张织毯，而不是被缩成一片平均色。
            return Material("M_WornRug", Color.white, .05f, AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }

        /// <summary>画框包围纸面而不是贴在纸上；未完成画作带独立原创笔触纹理。</summary>
        private static void CanvasStudy(string name, Transform parent, Vector3 position, Vector3 angles, float width, float height, int painting)
        {
            var root = Group(parent, name);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(angles);
            Frame(root, "WoodFrame", width + .08f, height + .08f, .045f, .075f, 0, warmWood);
            if (painting < 0) return;
            var surface = Beam("Canvas", root, new Vector3(0, 0, .025f), new Vector3(width, height, .035f), canvas, .012f);
            var art = MeshNode("PaintedStudy", surface, VerticalQuad(width * .95f, height * .95f), StudyMaterial(painting));
            art.localPosition = new Vector3(0, 0, -.021f);
        }

        /// <summary>纸张略有卷曲，画面是低对比手作研究稿，不承担可交互线索。</summary>
        private static Transform PaperSheet(string name, Transform parent, Vector3 position, Vector3 rotation, float width, float height, int study)
        {
            var root = MeshNode(name, parent, ClothMesh(width, height, .009f), StudyMaterial(study));
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(rotation);
            return root;
        }

        /// <summary>创建厚陶杯与小杯耳，形成真实工作台尺度。</summary>
        private static void Cup(string name, Transform parent, Vector3 position, float radius, float height, Material material)
        {
            var root = Group(parent, name);
            root.localPosition = position;
            MeshNode("Cup", root, Lathe(new[] { new Vector2(radius * .77f, 0), new Vector2(radius, height), new Vector2(radius * .82f, height), new Vector2(radius * .70f, .018f) }), material);
            var handle = MeshNode("Handle", root, RingMesh(radius * .49f, .012f), material);
            handle.localPosition = new Vector3(radius * .98f, height * .52f, 0);
            handle.localRotation = Quaternion.Euler(0, 90, 0);
        }

        /// <summary>油瓶具有肩部与窄颈，避免以圆柱代替完整道具。</summary>
        private static void Bottle(string name, Transform parent, Vector3 position, float radius, float height)
        {
            var root = Group(parent, name);
            root.localPosition = position;
            MeshNode("Bottle", root, Lathe(new[] { new Vector2(radius * .82f, 0), new Vector2(radius, .025f), new Vector2(radius, height * .66f), new Vector2(radius * .41f, height * .79f), new Vector2(radius * .37f, height) }), glass);
            var cap = MeshNode("Cork", root, Lathe(new[] { new Vector2(radius * .41f, height * .93f), new Vector2(radius * .41f, height * 1.04f), new Vector2(0, height * 1.04f) }), warmWood);
            cap.localRotation = Quaternion.Euler(0, 17, 0);
        }

        /// <summary>使用压扁软管、金属肩与有色标签，不再用单个灰色 Cube 表示颜料。</summary>
        private static void PaintTube(string name, Transform parent, Vector3 position, float yaw, Material color)
        {
            var root = Group(parent, name);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            Beam("TinBody", root, Vector3.zero, new Vector3(.057f, .039f, .15f), paleWood, .014f);
            Beam("PigmentLabel", root, new Vector3(0, .020f, -.012f), new Vector3(.050f, .005f, .067f), color, .006f);
            Beam("FoldedEnd", root, new Vector3(0, 0, -.085f), new Vector3(.065f, .024f, .012f), brass, .006f);
            Beam("Cap", root, new Vector3(0, 0, .085f), new Vector3(.027f, .027f, .035f), darkWood, .007f);
        }

        /// <summary>四根倒角边形成多层框线；相机近处可以看见真实截面。</summary>
        private static void Frame(Transform parent, string name, float width, float height, float border, float depth, float z, Material material)
        {
            Beam(name + "Left", parent, new Vector3(-width / 2, 0, z), new Vector3(border, height + border, depth), material, border * .2f);
            Beam(name + "Right", parent, new Vector3(width / 2, 0, z), new Vector3(border, height + border, depth), material, border * .2f);
            Beam(name + "Top", parent, new Vector3(0, height / 2, z), new Vector3(width + border, border, depth), material, border * .2f);
            Beam(name + "Bottom", parent, new Vector3(0, -height / 2, z), new Vector3(width + border, border, depth), material, border * .2f);
        }

        /// <summary>灯是实际局部光源，阴影只交给主要灯，避免每个小道具增加高成本实时阴影。</summary>
        private static void PointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var node = Group(parent, name);
            node.localPosition = position;
            var light = node.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        /// <summary>共享材质的纹理和法线明确定义，外部 Material_001 不进入自有内容库。</summary>
        private static Material Material(string name, Color color, float gloss, Texture2D diffuse = null, Texture2D normal = null, Vector2? tiling = null)
        {
            string path = Art + "Materials/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            {
                result = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(result, path);
            }
            result.shader = Shader.Find("Universal Render Pipeline/Lit");
            result.SetColor("_BaseColor", color);
            result.SetFloat("_Smoothness", gloss);
            if (diffuse != null) result.SetTexture("_BaseMap", diffuse);
            if (normal != null)
            {
                result.SetTexture("_BumpMap", normal);
                result.SetFloat("_BumpScale", .46f);
                result.EnableKeyword("_NORMALMAP");
            }
            result.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            EditorUtility.SetDirty(result);
            return result;
        }

        /// <summary>生成少量原创纸面研究稿；来源无需冒充第三方下载或正式剧情文本。</summary>
        private static Material StudyMaterial(int index)
        {
            index %= 3;
            string path = Art + "Textures/StudioStudy" + index + ".png";
            if (!File.Exists(path))
            {
                var image = new Texture2D(256, 256, TextureFormat.RGB24, false);
                Color ink = index == 0 ? new Color(.31f, .35f, .27f) : index == 1 ? new Color(.30f, .40f, .45f) : new Color(.49f, .31f, .17f);
                for (int y = 0; y < 256; y++)
                    for (int x = 0; x < 256; x++)
                    {
                        float u = x / 255f, v = y / 255f;
                        float grain = Mathf.PerlinNoise(x * .24f, y * .19f);
                        Color color = new Color(.87f, .82f, .69f) * (.91f + grain * .1f);
                        float hill = .37f + Mathf.Sin(u * 6.2f + index) * .065f + Mathf.Sin(u * 17) * .018f;
                        float wash = Mathf.Clamp01((hill - v) * 15) * .65f;
                        float brush = Mathf.PerlinNoise(x * .025f + index * 5, y * .13f);
                        color = Color.Lerp(color, ink, wash * (.42f + brush * .33f));
                        float stem = Mathf.Abs(u - (.28f + Mathf.Sin(v * 8) * .02f));
                        if (index == 0 && v > .28f && v < .72f && stem < .008f) color = Color.Lerp(color, ink, .7f);
                        if (index == 2 && Mathf.Abs(v - .62f + Mathf.Sin(u * 13) * .04f) < .012f && u > .13f && u < .8f) color = Color.Lerp(color, ink, .45f);
                        image.SetPixel(x, y, color);
                    }
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                AssetDatabase.ImportAsset(path);
            }
            return Material("M_Study" + index, Color.white, .1f, AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }

        /// <summary>删除本工具拥有的分组；旧人工节点只隐藏，不静默覆盖未保存摆放。</summary>
        private static Transform Recreate(Transform parent, string name)
        {
            var old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            return Group(parent, name);
        }
        /// <summary>纯视觉分组没有 SceneEntity、Clickable 或 Collider。</summary>
        private static Transform Group(Transform parent, string name)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            return node;
        }
        /// <summary>保留旧家具根，使复测和导演引用不失效。</summary>
        private static void HideChildren(Transform parent)
        {
            foreach (Transform child in parent) child.gameObject.SetActive(false);
        }
        /// <summary>创建共享网格节点，不携带碰撞或玩法组件。</summary>
        private static Transform MeshNode(string name, Transform parent, Mesh mesh, Material material)
        {
            var node = Group(parent, name);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return node;
        }
        /// <summary>倒角木件与金属件具有真实截面，静态尺寸由网格明确表达。</summary>
        private static Transform Beam(string name, Transform parent, Vector3 position, Vector3 size, Material material, float bevel)
        {
            var node = MeshNode(name, parent, BevelBox(size, bevel), material);
            node.localPosition = position;
            return node;
        }

        /// <summary>把临时网格存为共享资产，场景保存后不会出现缺失的内存 Mesh 引用。</summary>
        private static Mesh Store(string key, List<Vector3> vertices, List<int> indices, List<Vector2> uv)
        {
            key = key.Replace(".", "p").Replace("-", "m");
            if (meshCache.TryGetValue(key, out var cached)) return cached;
            var source = new Mesh { name = key };
            source.SetVertices(vertices);
            source.SetUVs(0, uv);
            source.SetTriangles(indices, 0);
            source.RecalculateNormals();
            source.RecalculateBounds();
            string path = Art + "Models/" + key + ".asset";
            var result = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (result == null) { AssetDatabase.CreateAsset(source, path); result = source; }
            else
            {
                // 保留 GUID，同时用网格 API 同步 CPU 与 GPU 数据，避免重复安装留下旧缓冲。
                result.Clear(); result.vertices = source.vertices; result.uv = source.uv;
                result.triangles = source.triangles; result.normals = source.normals;
                result.bounds = source.bounds; result.UploadMeshData(false);
                Object.DestroyImmediate(source); EditorUtility.SetDirty(result);
            }
            meshCache.Add(key, result);
            return result;
        }

        /// <summary>八边截面的薄倒角盒，避免画框与工具都呈现无厚度矩形。</summary>
        private static Mesh BevelBox(Vector3 size, float bevel)
        {
            float x = size.x / 2, y = size.y / 2, z = size.z / 2;
            float b = Mathf.Min(bevel, Mathf.Min(x, y) * .7f);
            Vector2[] ring = { new Vector2(-x + b, -y), new Vector2(x - b, -y), new Vector2(x, -y + b), new Vector2(x, y - b), new Vector2(x - b, y), new Vector2(-x + b, y), new Vector2(-x, y - b), new Vector2(-x, -y + b) };
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                vertices.Add(new Vector3(0, 0, side == 0 ? -z : z)); uv.Add(Vector2.one * .5f);
                foreach (var point in ring) { vertices.Add(new Vector3(point.x, point.y, side == 0 ? -z : z)); uv.Add(new Vector2(point.x / size.x + .5f, point.y / size.y + .5f)); }
                for (int n = 0; n < 8; n++)
                {
                    int center = side * 9, a = center + n + 1, next = center + (n + 1) % 8 + 1;
                    if (side == 0) triangles.AddRange(new[] { center, next, a }); else triangles.AddRange(new[] { center, a, next });
                }
            }
            for (int n = 0; n < 8; n++)
            {
                int a = 1 + n, b1 = 1 + (n + 1) % 8;
                triangles.AddRange(new[] { a, b1, a + 9, b1, b1 + 9, a + 9 });
            }
            return Store("Bevel_" + size.x.ToString("F3") + "_" + size.y.ToString("F3") + "_" + size.z.ToString("F3"), vertices, triangles, uv);
        }

        /// <summary>竖直画面朝向房间观察机位。</summary>
        private static Mesh VerticalQuad(float width, float height)
        {
            return Store("ArtQuad_" + width.ToString("F3") + "_" + height.ToString("F3"),
                new List<Vector3> { new Vector3(-width / 2, -height / 2, 0), new Vector3(width / 2, -height / 2, 0), new Vector3(-width / 2, height / 2, 0), new Vector3(width / 2, height / 2, 0) },
                new List<int> { 0, 2, 1, 1, 2, 3 }, new List<Vector2> { Vector2.zero, Vector2.right, Vector2.up, Vector2.one });
        }

        /// <summary>褶皱帘以连续曲面表达布料，背面独立三角面避免法线相消。</summary>
        private static Mesh CurtainMesh(float width, float height)
        {
            const int rows = 16, columns = 24;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= columns; x++)
                {
                    float u = x / (float)columns, v = y / (float)rows;
                    vertices.Add(new Vector3((u - .5f) * width, (v - .5f) * height, Mathf.Sin(u * Mathf.PI * 8) * .065f + Mathf.Sin(v * 3) * .025f));
                    uv.Add(new Vector2(u, v));
                    if (x < columns && y < rows)
                    {
                        int a = y * (columns + 1) + x, b = a + columns + 1;
                        triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                    }
                }
            int count = vertices.Count;
            vertices.AddRange(vertices.ToArray()); uv.AddRange(uv.ToArray());
            int[] front = triangles.ToArray();
            for (int n = 0; n < front.Length; n += 3) triangles.AddRange(new[] { front[n] + count, front[n + 2] + count, front[n + 1] + count });
            return Store("Curtain_" + width + "_" + height, vertices, triangles, uv);
        }

        /// <summary>桌布和纸张的微曲面；高度小于道具，不遮挡核心点击区域。</summary>
        private static Mesh ClothMesh(float width, float length, float amplitude)
        {
            const int rows = 12, columns = 12;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int z = 0; z <= rows; z++)
                for (int x = 0; x <= columns; x++)
                {
                    float u = x / (float)columns, v = z / (float)rows;
                    vertices.Add(new Vector3((u - .5f) * width, (Mathf.Sin(u * 12 + v * 3) * .25f + Mathf.Pow(v, 4) * -.8f) * amplitude, (v - .5f) * length));
                    uv.Add(new Vector2(u, v));
                    if (x < columns && z < rows)
                    {
                        int a = z * (columns + 1) + x, b = a + columns + 1;
                        triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                    }
                }
            return Store("Cloth_" + width + "_" + length + "_" + amplitude, vertices, triangles, uv);
        }

        /// <summary>旋转轮廓形成杯瓶薄壁，UV 连续围绕手作表面。</summary>
        private static Mesh Lathe(Vector2[] profile)
        {
            const int sides = 40;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int y = 0; y < profile.Length; y++)
                for (int x = 0; x <= sides; x++)
                {
                    float angle = x / (float)sides * Mathf.PI * 2;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * profile[y].x, profile[y].y, Mathf.Sin(angle) * profile[y].x));
                    uv.Add(new Vector2(x / (float)sides, y / (float)(profile.Length - 1)));
                    if (x < sides && y < profile.Length - 1)
                    {
                        int a = y * (sides + 1) + x, b = a + sides + 1;
                        triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                    }
                }
            string key = "Lathe_" + string.Join("_", profile.Select(p => p.x.ToString("F3") + "x" + p.y.ToString("F3")));
            return Store(key, vertices, triangles, uv);
        }

        /// <summary>椭圆调色盘保留厚度与顶面。</summary>
        private static Mesh OvalMesh(float xRadius, float zRadius, float thickness)
        {
            const int sides = 40;
            var vertices = new List<Vector3> { Vector3.up * thickness / 2 }; var uv = new List<Vector2> { Vector2.one * .5f }; var triangles = new List<int>();
            for (int n = 0; n <= sides; n++)
            {
                float a = n / (float)sides * Mathf.PI * 2;
                vertices.Add(new Vector3(Mathf.Cos(a) * xRadius, thickness / 2, Mathf.Sin(a) * zRadius));
                uv.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * .5f + Vector2.one * .5f);
                if (n < sides) triangles.AddRange(new[] { 0, n + 2, n + 1 });
            }
            return Store("Oval_" + xRadius + "_" + zRadius + "_" + thickness, vertices, triangles, uv);
        }

        /// <summary>小杯耳为圆滑环，避免把手只是一根横杆。</summary>
        private static Mesh RingMesh(float radius, float tube)
        {
            const int rings = 24, sides = 8;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int ring = 0; ring <= rings; ring++)
                for (int side = 0; side <= sides; side++)
                {
                    float a = ring / (float)rings * Mathf.PI * 2, b = side / (float)sides * Mathf.PI * 2;
                    vertices.Add(new Vector3(Mathf.Cos(a) * (radius + Mathf.Cos(b) * tube), Mathf.Sin(a) * (radius + Mathf.Cos(b) * tube), Mathf.Sin(b) * tube));
                    uv.Add(new Vector2(ring / (float)rings, side / (float)sides));
                    if (ring < rings && side < sides)
                    {
                        int v = ring * (sides + 1) + side, next = v + sides + 1;
                        triangles.AddRange(new[] { v, next, v + 1, v + 1, next, next + 1 });
                    }
                }
            return Store("CupHandle_" + radius + "_" + tube, vertices, triangles, uv);
        }

        /// <summary>揉皱纸使用低数量不规则面；它只是画面上的生活痕迹。</summary>
        private static Mesh CrumpledMesh(int seed)
        {
            const int rows = 8, sides = 12;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= sides; x++)
                {
                    float v = y / (float)rows, u = x / (float)sides;
                    float latitude = v * Mathf.PI, angle = u * Mathf.PI * 2;
                    float radius = .066f + Mathf.PerlinNoise(x * .71f + seed, y * .69f) * .018f;
                    vertices.Add(new Vector3(Mathf.Sin(latitude) * Mathf.Cos(angle), Mathf.Cos(latitude) * .8f, Mathf.Sin(latitude) * Mathf.Sin(angle)) * radius);
                    uv.Add(new Vector2(u, v));
                    if (x < sides && y < rows)
                    {
                        int a = y * (sides + 1) + x, b = a + sides + 1;
                        triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                    }
                }
            return Store("CrumpledPaper_" + seed, vertices, triangles, uv);
        }
    }
}
