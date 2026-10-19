// 文件：AtelierFlowOpeningBuilder.cs
// 职责：增量把首批新流程、真实画布和已验收底稿/洗笔装配进AtelierPlayable。
// 边界：保留美术和Prefab引用；禁用旧输入，拒绝未保存场景，不改正式构建列表。
using System;
using System.IO;
using System.Linq;
using Game.Unity;
using Game.Unity.ArtRebuild;
using Game.Unity.TechnicalArt.RealtimePainterly.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace Game.Editor.ArtRebuild
{
    /// <summary>只做一次装配，后续在Inspector明确调整，不重建人工房间。</summary>
    public static class AtelierFlowOpeningBuilder
    {
        private const string Main="Assets/_Game/Content/Scenes/AtelierPlayable.unity";
        private const string Root="Assets/_Game/Content/ArtRebuild/FourStage";
        [Serializable]
        private sealed class BuildEvidence
        {
            public string scene, result;
            public int errors, warnings;
            public ulong bytes;
            public double seconds;
        }
        /// <summary>按P00至P07装配首批；已经装配时拒绝重复添加。</summary>
        [MenuItem("游戏/新四阶段流程/安装首批开场与第一层")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出Play。");
            for(int n=0;n<UnityEngine.SceneManagement.SceneManager.sceneCount;n++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(n).isDirty) throw new InvalidOperationException("有未保存场景，停止主流程装配。");
            var scene=EditorSceneManager.OpenScene(Main);
            if(Object.FindAnyObjectByType<AtelierFlowGameplay>()!=null) throw new InvalidOperationException("首批已经安装，请直接调整现有组件。");
            Directory.CreateDirectory(Root+"/Prefabs");
            Directory.CreateDirectory("Assets/_Game/Content/ArtRebuild/Brushes/FourStage");
            for(int i=1;i<=4;i++)
            {
                string path="Assets/_Game/Content/ArtRebuild/Brushes/FourStage/"+i+".png";
                if(!File.Exists(path)) File.Copy("Docs/Design/全流程重设计20261019/概念图/策划笔刷/"+i+".png",path);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            for(int i=1;i<=4;i++)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/_Game/Content/ArtRebuild/Brushes/FourStage/"+i+".png");
                importer.sRGBTexture=true;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.maxTextureSize=2048;importer.SaveAndReimport();
            }
            var old=Object.FindAnyObjectByType<AtelierGameplay>();
            // 先保存旧引用，再禁用所有旧输入和Presenter。新旧规则绝不同时争抢世界输入。
            var wash=old.Wash;
            var painting=old.Painting;
            foreach(var script in old.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(script==null) continue;
                if((script.GetType().Namespace??"")=="Game.Unity.ArtRebuild") script.enabled=false;
            }
            old.enabled=false;
            old.Painting.enabled=false;
            old.Narrator.enabled=false;
            if(old.Collection!=null) old.Collection.enabled=false;
            var draft=old.ViewCamera.gameObject.AddComponent<AtelierDraftCamera>();
            var driver=old.gameObject.AddComponent<AtelierFlowGameplay>();
            driver.References=old;driver.Draft=draft;
            var water=old.gameObject.AddComponent<AtelierFlowWash>();
            water.Water=wash.Water;water.Brush=wash.SupportedBrush;water.Bristles=wash.BrushSurface;
            water.PigmentShader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/杯口接触散色.shader");
            driver.Wash=water;
            var canvas=old.gameObject.AddComponent<AtelierFlowCanvas>();
            canvas.Surface=painting.Surface;
            canvas.Brushes=Enumerable.Range(1,4).Select(i=>AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Content/ArtRebuild/Brushes/FourStage/"+i+".png")).ToArray();
            canvas.CanvasShader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/作品与临时预览.shader");
            canvas.StampShader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/原始图样笔印.shader");
            canvas.BrushCalibration=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Game/Content/ArtRebuild/Brushes/FourStage/笔刷标定.json");
            driver.CanvasPort=canvas;
            var voice=old.gameObject.AddComponent<AtelierFlowNarration>();
            var narration=new SerializedObject(old.Narrator);
            voice.Label=(Text)narration.FindProperty("label").objectReferenceValue;
            voice.Opacity=(CanvasGroup)narration.FindProperty("opacity").objectReferenceValue;
            driver.Voice=voice;
            // 直接复用通过人工验收的物件视觉；独立测试控制器不会进入主场景。
            string prefab=Root+"/Prefabs/PF_首批新增物件视觉.prefab";
            if(!File.Exists(prefab))
            {
                var originalSetup=EditorSceneManager.GetSceneManagerSetup();
                var probeScene=EditorSceneManager.OpenScene(AtelierTAProbeBuilder.Folder+"/TA01_局部草稿遮挡.unity",OpenSceneMode.Additive);
                var extras=probeScene.GetRootGameObjects().First(x=>x.name=="TA验证_额外视觉与演示对象");
                PrefabUtility.SaveAsPrefabAsset(extras,prefab);
                EditorSceneManager.CloseScene(probeScene,true);
            }
            var objects=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefab),scene);
            objects.name="四阶段新增物件_视觉";
            var picture=objects.GetComponentsInChildren<Transform>(true).First(x=>x.name=="TA占位_窗台蜡笔画位置");
            var logic=new GameObject("四阶段_蜡笔画Logic");
            logic.transform.SetParent(old.transform,false);
            logic.layer=old.GetComponentsInChildren<AtelierInteractionTarget>().First(x=>x.Kind==AtelierTargetKind.WashWater).gameObject.layer;
            logic.transform.position=picture.position;
            var picking=logic.AddComponent<BoxCollider>();picking.size=new Vector3(.28f,.33f,.06f);picking.isTrigger=true;
            driver.CrayonPicking=picking;
            var pose=new GameObject("四阶段_蜡笔画固定近景").transform;
            pose.SetParent(old.transform,false);pose.position=new Vector3(2.12f,1.40f,.85f);
            pose.rotation=Quaternion.LookRotation(picture.position-pose.position);
            driver.CrayonFocus=pose;
            InstallFeature();
            ApplyOpeningPresentation(driver);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new IOException("主流程场景保存失败。");
            AssetDatabase.SaveAssets();
            Debug.Log("[四阶段] P00至P07已装配；旧静物输入已停用，后续阶段仍待接入。");
        }
        /// <summary>只构建开发副本，不改正式Build Settings。</summary>
        [MenuItem("游戏/新四阶段流程/构建首批功能演示")]
        public static void BuildOpening()
        {
            string folder="Builds/AtelierOpening20261020";
            Directory.CreateDirectory(folder);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{Main}, locationPathName=folder+"/四阶段首批功能演示.app",
                target=BuildTarget.StandaloneOSX, options=BuildOptions.Development });
            File.Copy("Assets/ThirdParty/NotoSans/OFL.txt",folder+"/NotoSans-OFL.txt",true);
            File.WriteAllText(folder+"/构建结果.txt",report.summary.result+"，错误："+report.summary.totalErrors);
            // 留下本次真实构建摘要，不把截图或编译检查当作构建成功依据。
            File.WriteAllText(folder+"/构建摘要.json",JsonUtility.ToJson(new BuildEvidence {
                scene = Main,
                result = report.summary.result.ToString(),
                errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings,
                bytes = report.summary.totalSize,
                seconds = report.summary.totalTime.TotalSeconds
            },true));
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("首批演示构建失败。");
        }

        /// <summary>增量更新已安装首批的开场、便签和当前可见物件，不重建房间。</summary>
        [MenuItem("游戏/新四阶段流程/更新开场与第一阶段遮挡")]
        public static void ReviseOpening()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出Play。");
            for (int index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("有未保存场景，停止开场更新。");
            var scene = EditorSceneManager.OpenScene(Main);
            var flow = Object.FindAnyObjectByType<AtelierFlowGameplay>();
            if (flow == null) throw new InvalidOperationException("请先安装首批流程。");
            ApplyOpeningPresentation(flow);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("开场更新保存失败。");
            AssetDatabase.SaveAssets();
        }

        /// <summary>场景实例临时布局与世界锚定清晰区；UI同学替换控件时无需改账本或Shader。</summary>
        private static void ApplyOpeningPresentation(AtelierFlowGameplay flow)
        {
            var transforms = flow.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var note = transforms.First(target => target.name == "Visual_交稿便条");
            var picture = transforms.First(target => target.name == "TA占位_窗台蜡笔画位置");
            flow.Draft.ClearObjects = flow.Wash.Water.transform.parent.GetComponentsInChildren<Renderer>(true)
                .Concat(new[] { flow.CanvasPort.Surface, note.GetComponent<Renderer>() })
                // 小画线条的LineRenderer带有很大的保守包围盒，清晰区由纸面和画框覆盖即可。
                .Concat(picture.GetComponentsInChildren<Renderer>(true).Where(target => !(target is LineRenderer)))
                .Distinct().ToArray();
            if (flow.Draft.ClearObjects.Length > 16)
                throw new InvalidOperationException("第一阶段清晰物件超过Shader容量，请先分组合并。");
            flow.Draft.Density = 1;
            flow.Draft.RestoreStage(1,true);

            // 只更新场景实例覆盖，不修改UI同学维护的母版或NotebookView源码。
            var body = flow.References.ManuscriptPanel.GetComponentsInChildren<Text>(true)
                .First(text => text.name == "便签正文" || text.name == "便条正文");
            body.text = AtelierOpeningCopy.NoteBody;
            body.rectTransform.sizeDelta = new Vector2(440,225);
            body.fontSize = 22;
            body.lineSpacing = 1.25f;
            body.alignment = TextAnchor.UpperLeft;
            foreach (var text in note.GetComponentsInChildren<Text>(true))
            {
                text.text = AtelierOpeningCopy.NoteBody;
                text.fontSize = 22;
                text.lineSpacing = 1.18f;
                text.alignment = TextAnchor.UpperLeft;
            }
            var caption = flow.Voice.Opacity.GetComponent<RectTransform>();
            caption.anchorMin = caption.anchorMax = new Vector2(0,1);
            caption.pivot = new Vector2(0,1);
            caption.anchoredPosition = new Vector2(36,-28);
            caption.sizeDelta = new Vector2(760,110);
            var label = flow.Voice.Label.rectTransform;
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = label.offsetMax = Vector2.zero;
            flow.Voice.Label.alignment = TextAnchor.UpperLeft;
            flow.Voice.Label.fontSize = 26;
            flow.Voice.Label.lineSpacing = 1.3f;
            var context = flow.References.ContextPanel.GetComponent<RectTransform>();
            context.anchoredPosition = new Vector2(35,-170);
            // Prefab实例上的直接字段修改须显式记录覆盖，否则保存重开后会回到旧字句与布局。
            var changed = new Object[] { flow.Draft, body, body.rectTransform,
                caption, label, flow.Voice.Label, context };
            foreach (var target in changed.Concat(note.GetComponentsInChildren<Text>(true)))
            {
                EditorUtility.SetDirty(target);
                if (PrefabUtility.IsPartOfPrefabInstance(target))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
        }

        private static void InstallFeature()
        {
            var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            if(pipeline==null) throw new InvalidOperationException("当前质量档缺少画室管线。");
            var data=new SerializedObject(pipeline);
            var renderer=(ScriptableRendererData)data.FindProperty("m_RendererDataList").GetArrayElementAtIndex(data.FindProperty("m_DefaultRendererIndex").intValue).objectReferenceValue;
            if(renderer.rendererFeatures.Any(x=>x is AtelierDraftFeature))
            {
                SynchronizeFeatureMap(renderer);
                return;
            }
            var feature=ScriptableObject.CreateInstance<AtelierDraftFeature>();feature.name="阶段局部底稿_保留现有通道";
            feature.Configure(AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/阶段局部底稿.shader"));
            AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);
            SynchronizeFeatureMap(renderer);
            EditorUtility.SetDirty(renderer);
        }

        /// <summary>同步URP恢复映射，防止脚本重载时只保留旧通道数的索引。</summary>
        private static void SynchronizeFeatureMap(ScriptableRendererData renderer)
        {
            var serialized = new SerializedObject(renderer);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int index = 0; index < renderer.rendererFeatures.Count; index++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[index], out string guid, out long localId);
                map.GetArrayElementAtIndex(index).longValue = localId;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
        }
    }
}
