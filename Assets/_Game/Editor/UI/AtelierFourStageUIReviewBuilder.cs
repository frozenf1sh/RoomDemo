// 文件：AtelierFourStageUIReviewBuilder.cs
// 职责：在临时场景副本接入UI并显式构建验证Player。
// 边界：临时副本不交付，不保存主场景，不更改正式构建列表。
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Game.Unity.ArtRebuild;
using Game.Unity.UI.FourStage;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Game.Editor.UI
{
    /// <summary>验证装配与生产场景分开，避免覆盖用户打开的人工场景。</summary>
    public static class AtelierFourStageUIReviewBuilder
    {
        /// <summary>构建实际会话和新母版，finally移除临时场景。</summary>
        [MenuItem("游戏/四阶段UI/构建第一批真实会话验证")]
        public static void Build()
        {
            const string path = "Assets/_Game/Content/Scenes/Experiments/UI/AtelierUIBuildReview.unity";
            const string output = "Builds/AtelierUIFirstBatch";
            var original = SceneManager.GetActiveScene();
            Scene clone = default;
            bool resourcesDirectoryExisted = Directory.Exists("Assets/Resources");
            var protectedFiles = new Dictionary<string, byte[]>();
            foreach (var file in new[] { "ProjectSettings/QualitySettings.asset", "ProjectSettings/ProjectSettings.asset",
                "ProjectSettings/GraphicsSettings.asset", "Assets/Settings/DefaultVolumeProfile.asset",
                "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset",
                "Assets/_Game/Content/ArtRebuild/Rendering/画室专用管线.asset",
                "Assets/Resources/PerformanceTestRunInfo.json", "Assets/Resources/PerformanceTestRunInfo.json.meta",
                "Assets/Resources/PerformanceTestRunSettings.json", "Assets/Resources/PerformanceTestRunSettings.json.meta" })
                protectedFiles[file] = File.Exists(file) ? File.ReadAllBytes(file) : null;
            try
            {
                // Unity Test Framework 的预构建回调会写入 Resources；临时目录避免其把成功构建记为错误。
                Directory.CreateDirectory("Assets/Resources");
                // 此路径只属于本验证器。异常中断遗留的副本可以安全回收，避免阻塞下一次验证。
                if (File.Exists(path) && !AssetDatabase.DeleteAsset(path))
                    throw new InvalidOperationException("无法清理遗留的临时验证场景。");
                if (!AssetDatabase.CopyAsset("Assets/_Game/Content/Scenes/AtelierPlayable.unity",path))
                    throw new InvalidOperationException("临时验证场景复制失败。");
                clone = EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                SceneManager.SetActiveScene(clone);
                var flow = clone.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<AtelierFlowGameplay>(true)).Single();
                var root = new GameObject("第一批UI验证装配",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                root.GetComponent<Canvas>().sortingOrder = 30;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920,1080);
                scaler.matchWidthOrHeight = .5f;
                var workspace = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Game/Content/Prefabs/UI/FourStage/PF_AtelierCanvasWorkspace.prefab"),root.transform)).GetComponent<AtelierCanvasWorkspace>();
                var result = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Game/Content/Prefabs/UI/FourStage/PF_AtelierBrushResult.prefab"),root.transform)).GetComponent<AtelierBrushResultView>();
                var painting = root.AddComponent<AtelierPaintingSessionAdapter>();
                painting.Gameplay = flow;
                painting.Workspace = workspace;
                var brush = root.AddComponent<AtelierBrushResultAdapter>();
                brush.Gameplay = flow;
                brush.View = result;
                var review = root.AddComponent<AtelierUIFirstBatchPlayerReview>();
                review.Gameplay = flow;
                review.Workspace = workspace;
                review.Painting = painting;
                review.Result = result;
                EditorSceneManager.SaveScene(clone);
                Directory.CreateDirectory(output);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { path }, target = BuildTarget.StandaloneWindows64,
                    // 输出文件名保留 ASCII，避免 Windows 启动器因本地代码页误判而无法启动验证 Player。
                    locationPathName = output + "/AtelierUIFirstBatch.exe", options = BuildOptions.Development });
                File.Copy("Assets/ThirdParty/NotoSans/OFL.txt",output+"/NotoSans-OFL.txt",true);
                File.WriteAllText("Logs/FourStageUI-build.txt", "结果：" + report.summary.result +
                    "\n错误：" + report.summary.totalErrors + "\n警告：" + report.summary.totalWarnings +
                    "\n耗时：" + report.summary.totalTime + "\n验证范围：真实会话与UI临时装配，非正式场景交付。");
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("第一批UI验证构建失败。");
            }
            finally
            {
                if (original.IsValid()) SceneManager.SetActiveScene(original);
                if (clone.IsValid() && clone.isLoaded) EditorSceneManager.CloseScene(clone,true);
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                // 构建可能写回编辑器设置，恢复本次开始时的精确内容，保留用户本地设置。
                foreach (var file in protectedFiles)
                {
                    if (file.Value != null) File.WriteAllBytes(file.Key,file.Value);
                    else if (File.Exists(file.Key)) File.Delete(file.Key);
                }
                if (!resourcesDirectoryExisted && Directory.Exists("Assets/Resources") &&
                    !Directory.EnumerateFileSystemEntries("Assets/Resources").Any())
                    Directory.Delete("Assets/Resources");
                AssetDatabase.Refresh();
            }
        }
    }
}
