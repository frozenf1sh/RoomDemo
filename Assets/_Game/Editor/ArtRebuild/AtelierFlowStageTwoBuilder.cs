// 文件：AtelierFlowStageTwoBuilder.cs
// 职责：增量导入正式蜡笔画，装配第二阶段Logic、固定近景、真实手镜与逐物件显色清单。
// 边界：只修改可玩副本和本批资源；不重建房间、不修改UI母版、正式构建列表与星空。
using System;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Unity.ArtRebuild;
using Game.Unity.TechnicalArt.HoverGold;
using Game.Unity.TechnicalArt.RealtimePainterly.Experiments;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Game.Editor.ArtRebuild
{
    /// <summary>第二阶段装配入口，可在安装前后单独更新蜡笔画贴图。</summary>
    public static class AtelierFlowStageTwoBuilder
    {
        private const string Main = "Assets/_Game/Content/Scenes/AtelierPlayable.unity";
        private const string Root = "Assets/_Game/Content/ArtRebuild/FourStage";
        /// <summary>拒绝覆盖未保存的人工场景。</summary>
        private static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出Play。");
            for (int index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("有未保存场景，停止第二阶段装配。");
        }
        /// <summary>保持原始PNG字节，使用正方形纸面避免把蜡笔画拉长或裁边。</summary>
        [MenuItem("游戏/新四阶段流程/接入第二阶段与正式蜡笔画")]
        public static void Install()
        {
            Guard();
            Directory.CreateDirectory(Root+"/Textures");
            Directory.CreateDirectory(Root+"/Materials");
            string imagePath = Root+"/Textures/童年蜡笔画.png";
            string source = "/Users/frozenf1sh/Downloads/20261020-005646.png";
            if (File.Exists(source)) File.Copy(source,imagePath,true);
            else if (!File.Exists(imagePath)) throw new FileNotFoundException("缺少正式蜡笔画，请先补齐仓库图片。",imagePath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            importer.sRGBTexture = true;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/童年蜡笔画.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material,Root+"/Materials/童年蜡笔画.mat");
            }
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath));
            material.SetColor("_BaseColor",Color.white);
            EditorUtility.SetDirty(material);
            string extrasPath = Root+"/Prefabs/PF_首批新增物件视觉.prefab";
            var contents = PrefabUtility.LoadPrefabContents(extrasPath);
            try
            {
                var picture = contents.GetComponentsInChildren<Transform>(true).First(x => x.name == "TA占位_窗台蜡笔画位置");
                foreach (var line in picture.GetComponentsInChildren<LineRenderer>(true)) Object.DestroyImmediate(line.gameObject);
                var oldPaper = picture.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "正面仅彩色笔触占位");
                if (oldPaper != null) Object.DestroyImmediate(oldPaper.gameObject);
                var frame = picture.Find("纸模占位框");
                frame.localScale = new Vector3(.29f,.29f,.02f);
                if (picture.Find("Visual_童年蜡笔画") == null)
                {
                    var paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    paper.name = "Visual_童年蜡笔画";
                    Object.DestroyImmediate(paper.GetComponent<Collider>());
                    paper.transform.SetParent(picture,false);
                    paper.transform.localPosition = new Vector3(0,0,-.011f);
                    paper.transform.localRotation = Quaternion.identity;
                    paper.transform.localScale = Vector3.one*.26f;
                    paper.GetComponent<Renderer>().sharedMaterial = material;
                }
                picture.Find("Visual_童年蜡笔画").localRotation = Quaternion.identity;
                PrefabUtility.SaveAsPrefabAsset(contents,extrasPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            var scene = EditorSceneManager.OpenScene(Main);
            var flow = Object.FindAnyObjectByType<AtelierFlowGameplay>();
            if (flow == null) throw new InvalidOperationException("缺少首批流程。");
            var all = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform Find(string name) => all.First(x => x.name == name);
            var pictureVisual = Find("TA占位_窗台蜡笔画位置");
            flow.CrayonPicking.GetComponent<BoxCollider>().size = new Vector3(.30f,.30f,.06f);
            var note = Find("Visual_交稿便条").GetComponent<Renderer>();
            flow.Draft.ClearObjects = flow.Wash.Water.transform.parent.GetComponentsInChildren<Renderer>(true)
                .Concat(new[] { flow.CanvasPort.Surface,note })
                .Concat(pictureVisual.GetComponentsInChildren<Renderer>(true).Where(x => !(x is LineRenderer))).Distinct().ToArray();
            if (flow.Mirror == null)
            {
                var root = new GameObject("四阶段第二阶段_Logic与近景");
                root.transform.SetParent(flow.transform,false);
                var bust = Find("TA占位_策划石膏像");
                var mirror = Find("TA占位_完整手镜与转轴");
                var shards = all.Where(x => x.name.StartsWith("TA占位_已有碎片_")).ToArray();
                var dry = Object.Instantiate(flow.Wash.Brush.gameObject);
                dry.name = "Visual_黑色干画笔";
                dry.transform.SetParent(root.transform,false);
                dry.transform.position = new Vector3(-1.94f,.974f,-1.32f);
                dry.transform.rotation = Quaternion.Euler(0,18,0);
                foreach (var collider in dry.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                var dark = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                dark.SetColor("_BaseColor",new Color(.014f,.012f,.010f,1));
                string darkPath = Root+"/Materials/干涸黑颜料.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(darkPath);
                if (existing == null) AssetDatabase.CreateAsset(dark,darkPath);
                else { Object.DestroyImmediate(dark); dark = existing; }
                foreach (var renderer in dry.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = dark;
                PrefabUtility.SaveAsPrefabAssetAndConnect(dry,Root+"/Prefabs/PF_黑色干画笔.prefab",InteractionMode.AutomatedAction);
                var mirrorDriver = root.AddComponent<AtelierFlowMirror>();
                mirrorDriver.Flow = flow;
                mirrorDriver.Pivot = mirror;
                var reflection = root.AddComponent<AtelierMirrorProbe>();
                reflection.enabled = false;
                reflection.Source = flow.References.ViewCamera;
                reflection.Surface = Find("TA镜面_真实反射层").GetComponent<Renderer>();
                mirrorDriver.Reflection = reflection;
                flow.Mirror = mirrorDriver;
                flow.PlasterFocus = Pose(root.transform,"石膏像固定近景",new Vector3(-2.15f,1.47f,-1.04f),bust.GetComponent<Renderer>().bounds.center);
                flow.MirrorFocus = Pose(root.transform,"手镜比较固定近景",new Vector3(-1.65f,1.60f,-2.60f),new Vector3(-1.75f,.98f,-.45f));
                flow.DryBrushFocus = Pose(root.transform,"黑色干画笔固定近景",new Vector3(-1.80f,1.53f,-1.99f),dry.GetComponentInChildren<Renderer>().bounds.center);
                flow.ShardsFocus = Pose(root.transform,"已有碎镜固定近景",new Vector3(-1.38f,1.50f,-1.95f),new Vector3(-1.34f,.955f,-1.23f));
                var held = Pose(root.transform,"手镜拿起姿态",new Vector3(-1.15f,1.14f,-1.12f),Vector3.zero);
                Vector3 normal = (flow.MirrorFocus.position-held.position).normalized+(bust.GetComponent<Renderer>().bounds.center-held.position).normalized;
                held.rotation = Quaternion.LookRotation(normal.normalized,Vector3.up);
                mirrorDriver.HeldPose = held;
                int layer = flow.CrayonPicking.gameObject.layer;
                CreateTarget(root.transform,bust,new[] { bust.GetComponent<Renderer>() },AtelierTargetKind.Plaster,AtelierFlowRules.Plaster,"石膏像",layer);
                var mirrorTarget = CreateTarget(mirror,mirror,mirror.GetComponentsInChildren<Renderer>(),AtelierTargetKind.HandMirror,AtelierFlowRules.HandMirror,"手镜",layer);
                mirrorTarget.transform.localPosition = new Vector3(0,-.07f,0);
                mirrorTarget.transform.localRotation = Quaternion.identity;
                mirrorTarget.GetComponent<BoxCollider>().size = new Vector3(.43f,.65f,.05f);
                mirrorDriver.Picking = mirrorTarget.GetComponent<Collider>();
                CreateTarget(root.transform,dry.transform,dry.GetComponentsInChildren<Renderer>(),AtelierTargetKind.DryBrush,AtelierFlowRules.DryBrush,"干画笔",layer);
                var shardRenderers = shards.Select(x => x.GetComponent<Renderer>()).ToArray();
                CreateTarget(root.transform,shards[0],shardRenderers,AtelierTargetKind.MirrorShards,AtelierFlowRules.MirrorShards,"碎镜片",layer);
                flow.Draft.StageTwoObjects = new[] { bust.GetComponent<Renderer>() }.Concat(mirror.GetComponentsInChildren<Renderer>())
                    .Concat(dry.GetComponentsInChildren<Renderer>()).Concat(shardRenderers).Distinct().ToArray();
            }
            var shardObjects = all.Where(x => x.name.StartsWith("TA占位_已有碎片_")).OrderBy(x => x.name).ToArray();
            for (int index = 0; index < shardObjects.Length; index++)
            {
                shardObjects[index].position = new Vector3(-.96f+index*.10f,.958f,-1.55f-index%2*.06f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(shardObjects[index]);
            }
            var shardTarget = Object.FindObjectsByType<AtelierInteractionTarget>().First(x => x.Kind == AtelierTargetKind.MirrorShards);
            var shardBounds = shardObjects[0].GetComponent<Renderer>().bounds;
            foreach (var shardObject in shardObjects.Skip(1)) shardBounds.Encapsulate(shardObject.GetComponent<Renderer>().bounds);
            shardTarget.transform.position = shardBounds.center;
            shardTarget.GetComponent<BoxCollider>().size = shardBounds.size+Vector3.one*.018f;
            flow.ShardsFocus.SetPositionAndRotation(new Vector3(-.81f,1.50f,-2.20f),Quaternion.LookRotation(shardBounds.center-new Vector3(-.81f,1.50f,-2.20f)));
            // 实时镜面模板从编辑态起不进入常规批处理；动态材质/UV仅由专用通道绘制。
            var mirrorSurface = flow.Mirror.Reflection.Surface;
            if (flow.Mirror.Pivot.Find("Visual_镜面暗色背衬") == null)
            {
                var backing = new GameObject("Visual_镜面暗色背衬");
                backing.layer = 30;
                backing.transform.SetParent(mirrorSurface.transform.parent,false);
                backing.transform.localPosition = mirrorSurface.transform.localPosition+Vector3.forward*.002f;
                backing.transform.localRotation = mirrorSurface.transform.localRotation;
                backing.transform.localScale = mirrorSurface.transform.localScale;
                backing.AddComponent<MeshFilter>().sharedMesh = mirrorSurface.GetComponent<MeshFilter>().sharedMesh;
                var renderer = backing.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = mirrorSurface.sharedMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            var frameMaterial = MakeUnlit("手镜木框",new Color(.12f,.066f,.025f,1));
            var backingMaterial = MakeUnlit("手镜暗色背衬",new Color(.012f,.017f,.023f,1));
            foreach (var part in flow.Mirror.Pivot.GetComponentsInChildren<Renderer>())
            {
                if (part == mirrorSurface) continue;
                part.sharedMaterial = part.name == "Visual_镜面暗色背衬" ? backingMaterial : frameMaterial;
                if (PrefabUtility.IsPartOfPrefabInstance(part)) PrefabUtility.RecordPrefabInstancePropertyModifications(part);
            }
            mirrorSurface.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(mirrorSurface);
            var mirrorTargetForHover = flow.Mirror.Picking.GetComponent<AtelierInteractionTarget>();
            mirrorTargetForHover.HoverVisual.Surfaces = flow.Mirror.Pivot.GetComponentsInChildren<Renderer>();
            flow.Draft.StageTwoObjects = flow.Draft.StageTwoObjects.Concat(new[] {
                flow.Mirror.Pivot.Find("Visual_镜面暗色背衬").GetComponent<Renderer>() }).Distinct().ToArray();
            flow.Mirror.Reflection.CompositeAfterPost = true;
            flow.Mirror.Reflection.CompositeShader = AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/后处理后真实镜面.shader");
            InstallMirrorFeature();
            flow.Mirror.Picking.transform.localPosition = new Vector3(0,-.07f,0);
            flow.Mirror.Picking.transform.localRotation = Quaternion.identity;
            ((BoxCollider)flow.Mirror.Picking).size = new Vector3(.43f,.65f,.05f);
            // 三、四阶段未接入的目标不提前显色；屋顶始终单列，普通阶段调用不会暴露它。
            flow.Draft.StageThreeObjects = Array.Empty<Renderer>();
            flow.Draft.StageFourObjects = Array.Empty<Renderer>();
            flow.Draft.RoofObjects = Array.Empty<Renderer>();
            flow.Draft.RestoreStage(0,true);
            foreach (var target in new Object[] { flow,flow.Draft,flow.CrayonPicking })
            {
                EditorUtility.SetDirty(target);
                if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("第二阶段保存失败。");
            AssetDatabase.SaveAssets();
            Debug.Log("[四阶段] 正式蜡笔画与第二阶段已接入，阶段显色等待完整旁白。");
        }
        /// <summary>框和背衬使用有限亮度材质，避免近景金属高光进入Bloom形成白斑。</summary>
        private static Material MakeUnlit(string label,Color colour)
        {
            string path = Root+"/Materials/"+label+".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material,path);
            }
            material.SetColor("_BaseColor",colour);
            EditorUtility.SetDirty(material);
            return material;
        }
        /// <summary>在原Renderer追加镜面通道，不替换现有Feature与正式管线。</summary>
        private static void InstallMirrorFeature()
        {
            var pipeline = new SerializedObject(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline);
            var renderer = (UnityEngine.Rendering.Universal.ScriptableRendererData)pipeline.FindProperty("m_RendererDataList")
                .GetArrayElementAtIndex(pipeline.FindProperty("m_DefaultRendererIndex").intValue).objectReferenceValue;
            if (!renderer.rendererFeatures.Any(x => x is Game.Unity.TechnicalArt.RealtimePainterly.Production.AtelierMirrorFeature))
            {
                var feature = ScriptableObject.CreateInstance<Game.Unity.TechnicalArt.RealtimePainterly.Production.AtelierMirrorFeature>();
                feature.name = "手镜真实反射_后处理后合成";
                AssetDatabase.AddObjectToAsset(feature,renderer);
                renderer.rendererFeatures.Add(feature);
            }
            var data = new SerializedObject(renderer);
            var map = data.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int index = 0; index < renderer.rendererFeatures.Count; index++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[index],out string guid,out long id);
                map.GetArrayElementAtIndex(index).longValue = id;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }
        /// <summary>Logic包围体只覆盖自己的视觉，不包含桌子和旁边物品。</summary>
        private static AtelierInteractionTarget CreateTarget(Transform parent,Transform visual,Renderer[] surfaces,AtelierTargetKind kind,ObjectId id,string label,int layer)
        {
            var node = new GameObject("Logic_"+label);
            node.transform.SetParent(parent,false);
            node.layer = layer;
            var bounds = surfaces[0].bounds;
            foreach (var surface in surfaces.Skip(1)) bounds.Encapsulate(surface.bounds);
            node.transform.position = bounds.center;
            var box = node.AddComponent<BoxCollider>();
            box.size = bounds.size+Vector3.one*.018f;
            box.isTrigger = true;
            var target = node.AddComponent<AtelierInteractionTarget>();
            target.Kind = kind;
            target.ObjectId = id.Value;
            target.DisplayName = label;
            var hover = node.AddComponent<HoverGoldTarget>();
            hover.Surfaces = surfaces;
            target.HoverVisual = hover;
            return target;
        }
        /// <summary>固定机位不改变玩家主镜头的默认姿态。</summary>
        private static Transform Pose(Transform parent,string label,Vector3 position,Vector3 look)
        {
            var pose = new GameObject(label).transform;
            pose.SetParent(parent,false);
            pose.position = position;
            if ((look-position).sqrMagnitude > .001f) pose.rotation = Quaternion.LookRotation(look-position);
            return pose;
        }
        /// <summary>CLI使用稳定Editor程序集回调，避开长构建占用请求线程的超时日志。</summary>
        public static void QueueBuild()
        {
            Directory.CreateDirectory("Logs/AtelierStageTwo20261020");
            EditorApplication.update -= RunQueuedBuild;
            EditorApplication.update += RunQueuedBuild;
        }
        private static void RunQueuedBuild()
        {
            EditorApplication.update -= RunQueuedBuild;
            try
            {
                File.WriteAllText("Logs/AtelierStageTwo20261020/队列构建开始.txt","开始");
                Build();
                File.WriteAllText("Logs/AtelierStageTwo20261020/队列构建结束.txt","成功");
            }
            catch (Exception error)
            {
                File.WriteAllText("Logs/AtelierStageTwo20261020/队列构建结束.txt",error.ToString());
            }
        }
        [Serializable]
        private sealed class Evidence
        {
            public string result;
            public int errors, warnings;
            public ulong bytes;
            public double seconds;
            public string[] messages;
        }
        /// <summary>只构建开发副本，输出新目录保留上一批演示。</summary>
        [MenuItem("游戏/新四阶段流程/构建第二阶段演示")]
        public static void Build()
        {
            string folder = "Builds/AtelierStageTwo20261020";
            Directory.CreateDirectory(folder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { Main },locationPathName = folder+"/四阶段前两阶段演示.app",
                target = BuildTarget.StandaloneOSX,options = BuildOptions.Development });
            File.Copy("Assets/ThirdParty/NotoSans/OFL.txt",folder+"/NotoSans-OFL.txt",true);
            File.WriteAllText(folder+"/构建结果.txt",report.summary.result+"，错误："+report.summary.totalErrors+"，警告："+report.summary.totalWarnings+"，秒数："+report.summary.totalTime.TotalSeconds);
            File.WriteAllText(folder+"/构建摘要.json",JsonUtility.ToJson(new Evidence {
                result = report.summary.result.ToString(),errors = report.summary.totalErrors,warnings = report.summary.totalWarnings,
                bytes = report.summary.totalSize,seconds = report.summary.totalTime.TotalSeconds,
                messages = report.steps.SelectMany(step => step.messages).Where(message => message.type == LogType.Warning || message.type == LogType.Error)
                    .Select(message => message.type+": "+message.content).ToArray()
            },true));
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("第二阶段演示构建失败。");
        }
    }
}
