// 文件：AtelierFourStageUIBuilder.cs
// 职责：通过Editor API生成独立UI母版和预览场景。
// 边界：只写本批专用资源；新增附加场景后关闭，保留用户原场景与未保存改动。
using System;
using System.IO;
using System.Linq;
using Game.Unity.UI.FourStage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Game.Editor.UI
{
    /// <summary>显式菜单或一次性本地请求执行，不修改主场景与Build Settings。</summary>
    [InitializeOnLoad]
    public static class AtelierFourStageUIBuilder
    {
        public const string ScenePath = "Assets/_Game/Content/Scenes/Experiments/UI/AtelierFourStageUIPreview.unity";
        private const string Prefabs = "Assets/_Game/Content/Prefabs/UI/FourStage";
        private const string Request = "Logs/FourStageUI.request";
        private static Font font;
        // 第一批画具使用暖棕纸面和焦糖色操作反馈，避免与世界画布的冷色夜景争夺注意力。
        private static readonly Color Ink = new Color(.97f, .89f, .75f);
        private static readonly Color Paper = new Color(.20f, .125f, .075f, .97f);
        private static readonly Color ButtonBrown = new Color(.36f, .21f, .12f);
        private static readonly Color TrackBrown = new Color(.39f, .25f, .16f);
        private static readonly Color PaintBrown = new Color(.72f, .43f, .22f);
        static AtelierFourStageUIBuilder() { EditorApplication.update += Poll; }
        private static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string action;
            try
            {
                action = File.ReadAllText(Request).Trim();
                File.Delete(Request);
            }
            catch (IOException) { return; }
            try
            {
                if (action == "build-review") AtelierFourStageUIReviewBuilder.Build();
                else if (action == "install-playable") InstallInPlayable();
                else if (action == "install-brush-collection") InstallBrushesInCollection();
                else Generate();
            }
            catch (Exception e) { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/FourStageUI-generation.txt", e.ToString()); Debug.LogException(e); }
        }
        /// <summary>只生成独立资源；附加场景在finally关闭，不保存用户当前场景。</summary>
        [MenuItem("游戏/四阶段UI/生成第一批母版与独立预览")]
        public static void Generate()
        {
            Directory.CreateDirectory(Prefabs);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSans/NotoSansCJKsc-Regular.otf");
            if (font == null) throw new InvalidOperationException("缺少项目中文字体。");
            var active = SceneManager.GetActiveScene();
            var preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(preview);
            try
            {
                var root = new GameObject("四阶段UI独立预览", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = .5f;
                var background = Image("固定背景", root.transform, new Vector2(0, 0), Vector2.one, new Color(.085f,.048f,.028f));
                background.raycastTarget = false;
                var workspace = BuildWorkspace(root.transform);
                var result = BuildResult(root.transform);
                string workspacePath = Prefabs + "/PF_AtelierCanvasWorkspace.prefab";
                string resultPath = Prefabs + "/PF_AtelierBrushResult.prefab";
                PrefabUtility.SaveAsPrefabAsset(workspace.gameObject, workspacePath);
                PrefabUtility.SaveAsPrefabAsset(result.gameObject, resultPath);
                Object.DestroyImmediate(workspace.gameObject);
                Object.DestroyImmediate(result.gameObject);
                workspace = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(workspacePath), root.transform)).GetComponent<AtelierCanvasWorkspace>();
                result = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(resultPath), root.transform)).GetComponent<AtelierBrushResultView>();
                var fake = root.AddComponent<FakePaintingPreview>();
                fake.Workspace = workspace;
                fake.Result = result;
                fake.Brushes = new Texture2D[4];
                for (int i = 0; i < 4; i++)
                    fake.Brushes[i] = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Content/ArtRebuild/Brushes/FourStage/" + (i+1) + ".png");
                var surfaceArea = Rect("预览画布边界", root.transform, new Vector2(.245f,.18f), new Vector2(.79f,.79f));
                surfaceArea.SetSiblingIndex(1);
                var surface = Image("仅预览_画布替身", surfaceArea, Vector2.zero, Vector2.one, new Color(.12f,.062f,.032f));
                surface.raycastTarget = false;
                var aspect = surface.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = 1;
                // 用外部容器限幅，保持预览画布正方形且不挤压左侧控件。
                fake.Surface = surface.rectTransform;
                var ghost = new GameObject("仅预览_手势图样", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                ghost.transform.SetParent(surface.transform, false);
                ghost.raycastTarget = false;
                ghost.color = new Color(1,1,1,.65f);
                ghost.gameObject.SetActive(false);
                fake.Ghost = ghost;
                var controls = Rect("预览控制_仅编辑器", root.transform, new Vector2(.22f,.015f), new Vector2(.98f,.13f));
                fake.Evidence = Label("用例说明", controls, "编辑器固定快照：不实例化规则、不扣量。上一状态 / 下一状态切换用例。", new Vector2(0,.52f), new Vector2(1,1), 24);
                fake.Previous = Button("上一状态", controls, new Vector2(0,0), new Vector2(.19f,.45f));
                fake.Next = Button("下一状态", controls, new Vector2(.21f,0), new Vector2(.4f,.45f));
                fake.ShowResult = Button("查看笔刷结果", controls, new Vector2(.42f,0), new Vector2(.7f,.45f));
                Label("预览标记", root.transform, "UI开发预览 · 固定快照，仅编辑器", new Vector2(.03f,.92f), new Vector2(.56f,.99f), 26);
                var events = new GameObject("UI事件系统", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                // 独立场景有自己的相机，避免运行时出现无相机提示。
                var camera = new GameObject("预览相机", typeof(Camera)).GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.085f,.048f,.028f);
                camera.cullingMask = 0;
                if (!EditorSceneManager.SaveScene(preview, ScenePath)) throw new InvalidOperationException("预览场景保存失败。");
                // Prefab和场景已经分别保存，不保存用户其他尚未保存的材质资源。
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/FourStageUI-generation.txt", "生成成功：独立预览场景、Workspace与笔刷结果Prefab。主场景和构建列表未修改。");
                Debug.Log("四阶段UI第一批母版与独立预览已生成。");
            }
            finally
            {
                SceneManager.SetActiveScene(active);
                EditorSceneManager.CloseScene(preview, true);
            }
        }
        /// <summary>把第一批组合根接入开发入口；重复执行只复用现有根，不触碰正式构建场景列表。</summary>
        [MenuItem("游戏/四阶段UI/接入AtelierPlayable第一批UI")]
        public static void InstallInPlayable()
        {
            const string playablePath = "Assets/_Game/Content/Scenes/AtelierPlayable.unity";
            const string rootName = "第一批UI组合根";
            const string workspacePath = Prefabs + "/PF_AtelierCanvasWorkspace.prefab";
            const string resultPath = Prefabs + "/PF_AtelierBrushResult.prefab";
            var workspacePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(workspacePath);
            var resultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(resultPath);
            if (workspacePrefab == null || resultPrefab == null)
                throw new InvalidOperationException("请先生成第一批UI母版。");
            var original = SceneManager.GetActiveScene();
            var loaded = SceneManager.GetSceneByPath(playablePath);
            bool closeWhenDone = !(loaded.IsValid() && loaded.isLoaded);
            var playable = closeWhenDone ? EditorSceneManager.OpenScene(playablePath, OpenSceneMode.Additive) : loaded;
            try
            {
                var roots = playable.GetRootGameObjects();
                var existing = roots.FirstOrDefault(item => item.name == rootName);
                if (existing != null) return;
                var flow = roots.SelectMany(item => item.GetComponentsInChildren<Game.Unity.ArtRebuild.AtelierFlowGameplay>(true)).Single();
                var root = new GameObject(rootName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = .5f;
                var workspace = ((GameObject)PrefabUtility.InstantiatePrefab(workspacePrefab, root.transform)).GetComponent<AtelierCanvasWorkspace>();
                var result = ((GameObject)PrefabUtility.InstantiatePrefab(resultPrefab, root.transform)).GetComponent<AtelierBrushResultView>();
                var painting = root.AddComponent<AtelierPaintingSessionAdapter>();
                painting.Gameplay = flow;
                painting.Workspace = workspace;
                var brush = root.AddComponent<AtelierBrushResultAdapter>();
                brush.Gameplay = flow;
                brush.View = result;
                EditorSceneManager.SaveScene(playable);
            }
            finally
            {
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                if (closeWhenDone && playable.isLoaded) EditorSceneManager.CloseScene(playable, true);
            }
        }
        /// <summary>把四张已存在的原始笔刷 PNG 注入既有藏品簿，不调整笔记本结构或 Build Settings。</summary>
        [MenuItem("游戏/四阶段UI/接入四阶段笔刷藏品")]
        public static void InstallBrushesInCollection()
        {
            const string playablePath = "Assets/_Game/Content/Scenes/AtelierPlayable.unity";
            EnsureBrushDisplayOnNotebookPrefab();
            var original = SceneManager.GetActiveScene();
            var loaded = SceneManager.GetSceneByPath(playablePath);
            bool closeWhenDone = !(loaded.IsValid() && loaded.isLoaded);
            var playable = closeWhenDone ? EditorSceneManager.OpenScene(playablePath, OpenSceneMode.Additive) : loaded;
            try
            {
                var gameplay = playable.GetRootGameObjects()
                    .SelectMany(item => item.GetComponentsInChildren<Game.Unity.ArtRebuild.AtelierGameplay>(true))
                    .Single();
                if (gameplay.Collection == null)
                    throw new InvalidOperationException("AtelierPlayable 缺少既有藏品簿引用。");
                var artwork = new Texture2D[4];
                for (int index = 0; index < artwork.Length; index++)
                {
                    artwork[index] = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        "Assets/_Game/Content/ArtRebuild/Brushes/FourStage/" + (index + 1) + ".png");
                    if (artwork[index] == null)
                        throw new InvalidOperationException("缺少四阶段笔刷原图：" + (index + 1) + ".png");
                }
                gameplay.Collection.ConfigureFourStageBrushArtwork(artwork);
                var canvas = playable.GetRootGameObjects()
                    .SelectMany(item => item.GetComponentsInChildren<Game.Unity.NotebookCanvasView>(true))
                    .SingleOrDefault();
                if (canvas == null)
                    throw new InvalidOperationException("AtelierPlayable 缺少既有笔记组合区。");
                var oldDisplay = canvas.transform.Find("笔刷形成图样");
                if (oldDisplay != null && PrefabUtility.GetPrefabInstanceStatus(oldDisplay.gameObject) == PrefabInstanceStatus.NotAPrefab)
                    Object.DestroyImmediate(oldDisplay.gameObject);
                if (!EditorSceneManager.SaveScene(playable))
                    throw new InvalidOperationException("四阶段笔刷藏品接入未能保存 AtelierPlayable 场景。");
            }
            finally
            {
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                if (closeWhenDone && playable.isLoaded) EditorSceneManager.CloseScene(playable, true);
            }
        }
        /// <summary>清理 AtelierPlayable 场景中残留的场景级笔刷图卡。
        /// 场景通过 PrefabInstance 的 m_AddedGameObjects 把"笔刷形成图样"附加到笔记根节点，
        /// 与 NotebookPanel.prefab 内烘焙的同名节点形成显示冲突。
        /// 这里只删除"没有对应 prefab 源(GetCorrespondingObjectFromSource==null)"的附加节点，保留 prefab 烘焙节点。
        /// 边界：只改当前场景，不触碰 NotebookPanel.prefab 与 Build Settings。</summary>
        [MenuItem("游戏/四阶段UI/清理场景残留笔刷图卡")]
        public static void CleanupResidualBrushDisplayInScene()
        {
            const string playablePath = "Assets/_Game/Content/Scenes/AtelierPlayable.unity";
            var original = SceneManager.GetActiveScene();
            var loaded = SceneManager.GetSceneByPath(playablePath);
            bool closeWhenDone = !(loaded.IsValid() && loaded.isLoaded);
            var playable = closeWhenDone ? EditorSceneManager.OpenScene(playablePath, OpenSceneMode.Additive) : loaded;
            try
            {
                // 遍历场景内全部笔记组合区，把其中的附加笔刷图卡都收集后再统一删除。
                var targets = playable.GetRootGameObjects()
                    .SelectMany(item => item.GetComponentsInChildren<Game.Unity.NotebookCanvasView>(true))
                    .SelectMany(canvas => canvas.transform.Cast<Transform>())
                    .Where(child => child.name == "\u7B14\u5237\u5F62\u6210\u56FE\u6837")
                    .Where(child => PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == null)
                    .ToList();
                if (targets.Count <= 0)
                {
                    Directory.CreateDirectory("Logs");
                    File.WriteAllText("Logs/FourStageUI-generation.txt", "清理完成：未发现残留的场景级笔刷图卡。");
                    Debug.Log("四阶段UI：未发现残留的场景级笔刷图卡。");
                    return;
                }
                foreach (var target in targets) Object.DestroyImmediate(target.gameObject);
                if (!EditorSceneManager.SaveScene(playable))
                    throw new InvalidOperationException("清理残留笔刷图卡后保存 AtelierPlayable 失败。");
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/FourStageUI-generation.txt", "清理完成：已移除 " + targets.Count + " 个残留场景级笔刷图卡。");
                Debug.Log("四阶段UI：已移除残留场景级笔刷图卡 " + targets.Count + " 个。");
            }
            finally
            {
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                if (closeWhenDone && playable.isLoaded) EditorSceneManager.CloseScene(playable, true);
            }
        }

        /// <summary>把图卡写入既有笔记 Prefab，便于美术在 Prefab Mode 中直接手调组合区位置。</summary>
        private static void EnsureBrushDisplayOnNotebookPrefab()
        {
            const string notebookPrefabPath = "Assets/_Game/Content/Prefabs/UI/NotebookPanel.prefab";
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/NotoSans/NotoSansCJKsc-Regular.otf");
            if (font == null) throw new InvalidOperationException("缺少项目中文字体。");
            var prefabRoot = PrefabUtility.LoadPrefabContents(notebookPrefabPath);
            try
            {
                var canvas = prefabRoot.GetComponentInChildren<Game.Unity.NotebookCanvasView>(true);
                if (canvas == null) throw new InvalidOperationException("NotebookPanel Prefab 缺少组合区组件。");
                if (canvas.transform.Find("笔刷形成图样") != null) return;
                // 图卡是组合区的普通子节点；不新增材料栏、页面或点击入口。
                var panel = Image("笔刷形成图样", canvas.transform, new Vector2(.68f, .28f), new Vector2(.93f, .73f), Paper);
                panel.raycastTarget = false;
                var group = panel.gameObject.AddComponent<CanvasGroup>();
                var title = Label("笔刷形成标题", panel.transform, "", new Vector2(.08f, .80f), new Vector2(.92f, .96f), 20);
                title.alignment = TextAnchor.MiddleCenter;
                var box = Rect("笔刷原图边界", panel.transform, new Vector2(.13f, .17f), new Vector2(.87f, .75f));
                var preview = new GameObject("笔刷原始PNG", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter)).GetComponent<RawImage>();
                preview.transform.SetParent(box, false);
                preview.raycastTarget = false;
                var aspect = preview.GetComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                var display = panel.gameObject.AddComponent<AtelierBrushAcquisitionDisplay>();
                display.Group = group;
                display.Preview = preview;
                display.Aspect = aspect;
                display.Title = title;
                panel.transform.SetAsLastSibling();
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, notebookPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        private static AtelierCanvasWorkspace BuildWorkspace(Transform parent)
        {
            var root = Rect("画布工作区", parent, Vector2.zero, Vector2.one);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var r = root.gameObject.AddComponent<AtelierCanvasWorkspaceReferences>();
            r.Group = group;
            var left = Image("左侧画具", root, new Vector2(.025f,.17f), new Vector2(.21f,.83f), Paper);
            left.raycastTarget = true;
            Label("画具标题", left.transform, "画具", new Vector2(.08f,.9f), new Vector2(.92f,.99f), 30);
            var brushBox = Rect("图样边界", left.transform, new Vector2(.16f,.68f), new Vector2(.84f,.88f));
            r.Brush = new GameObject("原始笔刷预览", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter)).GetComponent<RawImage>();
            r.Brush.transform.SetParent(brushBox,false);
            r.Brush.raycastTarget = false;
            r.BrushAspect = r.Brush.GetComponent<AspectRatioFitter>();
            r.BrushAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            r.SizeLabel = Label("大小文字", left.transform, "笔印大小", new Vector2(.08f,.59f), new Vector2(.95f,.68f), 25);
            r.Size = Slider("大小", left.transform, new Vector2(.08f,.51f), new Vector2(.92f,.59f), 12, 24);
            r.AngleLabel = Label("旋转文字", left.transform, "旋转", new Vector2(.08f,.42f), new Vector2(.95f,.51f), 25);
            r.Angle = Slider("旋转", left.transform, new Vector2(.08f,.34f), new Vector2(.92f,.42f), 0, 359);
            r.CountLabel = Label("笔数", left.transform, "已落 0 笔", new Vector2(.08f,.25f), new Vector2(.98f,.34f), 24);
            r.Paint = Slider("剩余颜料", left.transform, new Vector2(.08f,.18f), new Vector2(.92f,.25f), 0, 100);
            r.Paint.interactable = false;
            r.Undo = Button("撤销", left.transform, new Vector2(.08f,.055f), new Vector2(.92f,.15f));
            r.Knife = Button("刀面涂抹", root, new Vector2(.025f,.1f), new Vector2(.21f,.16f));
            r.Knife.gameObject.SetActive(false);
            r.Return = Button("返回画室", root, new Vector2(.64f,.86f), new Vector2(.795f,.925f));
            r.Finish = Button("完成绘画", root, new Vector2(.81f,.86f), new Vector2(.975f,.925f));
            r.Status = Label("条件与反馈", root, "", new Vector2(.79f,.45f), new Vector2(.98f,.8f), 26);
            r.Status.alignment = TextAnchor.UpperLeft;
            var artwork = new GameObject("作品纹理_可选预览", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            artwork.transform.SetParent(root,false);
            artwork.raycastTarget = false;
            artwork.rectTransform.anchorMin = new Vector2(.26f,.19f);
            artwork.rectTransform.anchorMax = new Vector2(.77f,.79f);
            artwork.rectTransform.offsetMin = artwork.rectTransform.offsetMax = Vector2.zero;
                artwork.color = Color.white;
            r.Artwork = artwork;
            var view = root.gameObject.AddComponent<AtelierCanvasWorkspace>();
            view.References = r;
            return view;
        }
        private static AtelierBrushResultView BuildResult(Transform parent)
        {
            var root = Rect("笔刷结果控制器", parent, Vector2.zero, Vector2.one);
            var view = root.gameObject.AddComponent<AtelierBrushResultView>();
            var panel = Image("形成结果页", root, new Vector2(.22f,.14f), new Vector2(.79f,.84f), Paper);
            view.Panel = panel.gameObject;
            view.Title = Label("结果名", panel.transform, "形成了卷涡笔刷", new Vector2(.08f,.84f), new Vector2(.92f,.97f), 36);
            var box = Rect("原图边界", panel.transform, new Vector2(.26f,.35f), new Vector2(.74f,.81f));
            view.Preview = new GameObject("原始PNG",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter)).GetComponent<RawImage>();
            view.Preview.transform.SetParent(box,false);
            view.Preview.raycastTarget = false;
            view.Aspect = view.Preview.GetComponent<AspectRatioFitter>();
            view.Aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            view.Sources = Label("来源与下一步", panel.transform, "", new Vector2(.08f,.17f), new Vector2(.92f,.35f), 26);
            view.Back = Button("返回组合", panel.transform, new Vector2(.08f,.035f), new Vector2(.47f,.14f));
            view.Close = Button("收起笔记", panel.transform, new Vector2(.53f,.035f), new Vector2(.92f,.14f));
            view.Panel.SetActive(false);
            return view;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent,false);
            r.anchorMin = min; r.anchorMax = max;
            r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }
        private static Image Image(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var image = Rect(name,parent,min,max).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }
        private static Text Label(string name, Transform parent, string copy, Vector2 min, Vector2 max, int size)
        {
            var label = Rect(name,parent,min,max).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.color = Ink;
            label.text = copy; label.raycastTarget = false;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        private static Button Button(string copy, Transform parent, Vector2 min, Vector2 max)
        {
            var image = Image(copy,parent,min,max,ButtonBrown);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = Label("文字",image.transform,copy,new Vector2(.025f,0),new Vector2(.975f,1),26);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        private static Slider Slider(string name, Transform parent, Vector2 min, Vector2 max, int low, int high)
        {
            var root = Rect(name,parent,min,max);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0,0,0,.01f);
            Image("轨道",root,new Vector2(0,.42f),new Vector2(1,.58f),TrackBrown).raycastTarget = false;
            var fill = Image("颜料带",root,new Vector2(0,.42f),new Vector2(1,.58f),PaintBrown);
            fill.raycastTarget = false;
            var handle = Image("滑块",root,new Vector2(0,.12f),new Vector2(0,.88f),Ink);
            handle.rectTransform.sizeDelta = new Vector2(18,0);
            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = low; slider.maxValue = high; slider.wholeNumbers = true;
            return slider;
        }
    }
}
