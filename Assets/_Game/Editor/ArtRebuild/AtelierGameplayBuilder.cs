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
        private static Font font;
        private static readonly Color Ink = new Color(.23f, .21f, .17f);

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
            BuildUi(root, game);
            AddOcclusion(art, root);
            // 新对象不进入旧领域绑定；正式入口暂保留已交付美术场景，完整路线后再切换。
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "已建立 " + ScenePath + "；美术基线未覆盖，Build Settings 未切换。";
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

        private static Texture2D Repaint(Texture2D source)
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
            var body=Panel("笔记纸面",canvas.transform,new Vector2(980,570),new Vector2(0,16),new Color(.88f,.84f,.73f,.98f));
            var opacity=body.gameObject.AddComponent<CanvasGroup>();
            Label("纸页标题",body,"笔记本",new Vector2(0,251),new Vector2(220,40),25,Ink);
            var close=Button("关闭笔记",body,"合上",new Vector2(427,251),new Vector2(80,38));
            var scrollRoot=Panel("左侧材料",body,new Vector2(306,432),new Vector2(-305,-8),Color.clear);
            var viewport=Panel("材料视窗",scrollRoot,new Vector2(280,432),new Vector2(-10,0),Color.clear); viewport.gameObject.AddComponent<RectMask2D>();
            var content=Panel("所有已知材料",viewport,new Vector2(280,0),Vector2.zero,Color.clear);
            content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.spacing=5;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var bar=Panel("材料滚动条",scrollRoot,new Vector2(10,432),new Vector2(145,0),new Color(.3f,.27f,.21f,.15f));
            var handle=Panel("滚动柄",bar,new Vector2(10,80),Vector2.zero,new Color(.3f,.35f,.35f,.5f));
            var scrollbar=bar.gameObject.AddComponent<Scrollbar>();scrollbar.direction=Scrollbar.Direction.BottomToTop;scrollbar.handleRect=handle;scrollbar.targetGraphic=handle.GetComponent<Image>();
            var scroll=scrollRoot.gameObject.AddComponent<ScrollRect>();scroll.content=content;scroll.viewport=viewport;scroll.horizontal=false;scroll.verticalScrollbar=scrollbar;scroll.scrollSensitivity=24;scroll.movementType=ScrollRect.MovementType.Clamped;
            var workspace=Panel("有限解释区",body,new Vector2(530,350),new Vector2(150,34),new Color(1,1,1,.04f));
            var template=Button("已知材料模板",body,"",Vector2.zero,new Vector2(260,43));template.gameObject.AddComponent<LayoutElement>().preferredHeight=43;template.gameObject.SetActive(false);
            var card=Button("活动词条模板",workspace,"",Vector2.zero,new Vector2(142,62));card.gameObject.SetActive(false);
            var submit=Button("提交解释",workspace,"解释",new Vector2(0,-140),new Vector2(110,42));
            var hidden=Group("兼容绑定",body);hidden.gameObject.SetActive(false);
            var selection=Label("临时选择",hidden,"",Vector2.zero,Vector2.one,22,Ink);var feedback=Label("反馈",hidden,"",Vector2.zero,Vector2.one,22,Ink);
            var history=Label("历史",hidden,"",Vector2.zero,Vector2.one,22,Ink);var marks=Label("记号",hidden,"",Vector2.zero,Vector2.one,22,Ink);
            var invitation=Label("纸面反馈",workspace,"",new Vector2(0,135),new Vector2(440,44),22,Ink);
            var insight=Label("形成的新词",workspace,"",new Vector2(0,135),new Vector2(440,44),24,new Color(.25f,.39f,.63f));
            var description=Label("词语说明",body,"",new Vector2(150,-211),new Vector2(530,65),20,Ink);
            var reminder=Button("未读笔记提醒",canvas.transform,"笔记 · N",new Vector2(-550,-322),new Vector2(124,38));
            reminder.GetComponentInChildren<Text>().color=new Color(.9f,.86f,.76f);
            var notice=Panel("短暂新发现",canvas.transform,new Vector2(420,45),new Vector2(370,-285),Color.clear);
            var noticeText=Label("发现文字",notice,"",Vector2.zero,new Vector2(420,45),18,new Color(.93f,.89f,.78f));
            var lines=new Image[2];for(int i=0;i<2;i++)lines[i]=Panel("近邻淡线"+i,workspace,new Vector2(140,1),Vector2.zero,new Color(.4f,.34f,.24f,.14f)).GetComponent<Image>();
            var paper=Sound(root,"翻页",.04f);var ink=Sound(root,"落笔",.04f);var scratch=Sound(root,"纸面轻反馈",.02f);
            var notes=canvas.gameObject.AddComponent<NotebookView>();
            notes.Configure(body.gameObject,Group("旧物体占位",hidden),Group("旧概念占位",hidden),template,submit,selection,feedback);
            notes.ConfigureSlots(Array.Empty<Button>(),history);notes.ConfigureControls(reminder,close);notes.ConfigureDiscoveryFeedback(notice.gameObject,noticeText);
            notes.ConfigurePresentation(body,opacity,lines[0],paper,ink,scratch);notes.ConfigureUnifiedWords(content,marks);notes.ConfigureExploration(null,insight);notes.ConfigureQuietStudio(true);notes.ConfigureDescription(description);
            var semantic=body.gameObject.AddComponent<NotebookCanvasView>();semantic.Configure(workspace,card,submit,lines,invitation);semantic.ConfigureBlindComposition(true);semantic.ConfigureQuietStudio(true);notes.ConfigureCanvas(semantic);
            var caption=Label("世界字幕",canvas.transform,"",new Vector2(0,-325),new Vector2(850,40),19,new Color(.84f,.89f,.94f));
            var captions=caption.gameObject.AddComponent<OneLineSubtitle>();captions.Configure(caption,caption.gameObject.AddComponent<CanvasGroup>());
            var narrationArea=Panel("旁白区域",canvas.transform,new Vector2(960,70),new Vector2(0,-313),Color.clear);
            var narrationText=Label("旁白",narrationArea,"",Vector2.zero,new Vector2(950,70),23,new Color(.95f,.91f,.82f));
            var audio=canvas.gameObject.AddComponent<ContentFeedback>();audio.Configure(Sound(root,"旁白轻声",0),Array.Empty<AudioCueDefinition>(),Array.Empty<RecipePresentation>(),captions,notes,null,null,null,null);
            var narrator=canvas.gameObject.AddComponent<NarrationSystem>();narrator.Configure(narrationText,narrationArea.gameObject.AddComponent<CanvasGroup>(),audio,Array.Empty<NarrationDefinition>());
            var context=Panel("调查与动作",canvas.transform,new Vector2(440,100),new Vector2(-385,277),new Color(.06f,.07f,.08f,.82f));
            game.ObservationText=Label("观察描述",context,"",new Vector2(-40,12),new Vector2(330,75),19,new Color(.93f,.89f,.78f));
            game.ContextAction=Button("当前动作",context,"",new Vector2(155,-15),new Vector2(110,42));game.ContextAction.GetComponentInChildren<Text>().color=new Color(.94f,.89f,.78f);
            game.ContextPanel=context.gameObject;
            game.ReturnButton=Button("返回画室",canvas.transform,"返回画室",new Vector2(532,316),new Vector2(150,42));game.ReturnButton.GetComponentInChildren<Text>().color=new Color(.94f,.9f,.8f);
            var pause=Panel("暂停菜单",canvas.transform,new Vector2(390,290),Vector2.zero,new Color(.07f,.09f,.12f,.97f));
            game.ResumeButton=Button("继续",pause,"继续",new Vector2(0,78),new Vector2(230,44));game.RestartButton=Button("重新开始",pause,"重新开始",new Vector2(0,18),new Vector2(230,44));game.QuitButton=Button("退出",pause,"退出",new Vector2(0,-42),new Vector2(230,44));
            foreach(var button in new[]{game.ResumeButton,game.RestartButton,game.QuitButton})button.GetComponentInChildren<Text>().color=new Color(.94f,.90f,.80f);
            Label("操作说明",pause,"按住右键或空格转头 · 滚轮看近\nN 打开笔记 · 近看时右键返回",new Vector2(0,-111),new Vector2(375,58),16,new Color(.84f,.82f,.75f));
            game.HoverText=Label("悬停物体名",canvas.transform,"",Vector2.zero,new Vector2(220,35),18,new Color(.91f,.89f,.81f));
            game.Notebook=notes;game.Narrator=narrator;game.Captions=captions;game.PausePanel=pause.gameObject;game.Reminder=reminder;
        }

        private static AudioSource Sound(Transform parent,string name,float volume)
        {
            // 首阶段不拿电子提示音冒充洗笔或纸页 Foley，保留显式声源供后续录音绑定。
            var audio=Group(name,parent).gameObject.AddComponent<AudioSource>();audio.playOnAwake=false;audio.volume=volume;return audio;
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
