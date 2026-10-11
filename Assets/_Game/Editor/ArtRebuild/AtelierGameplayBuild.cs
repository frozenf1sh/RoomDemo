// 文件：AtelierGameplayBuild.cs
// 职责：只构建新画室第一阶段试玩副本，保存报告与分发许可证。
// 边界：不恢复旧流水线、不切换正式入口、不覆盖既有美术试玩包。
using System;
using System.IO;
using Game.Unity.ArtRebuild;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using System.Linq;

namespace Game.Editor.ArtRebuild
{
    /// <summary>本阶段构建入口与原美术预览明确分开。</summary>
    public static class AtelierGameplayBuild
    {
        /// <summary>由稳定 Editor 程序集排入构建，避免长构建占用 eval 请求而被五秒超时记为错误。</summary>
        public static string QueueOpeningBuild()
        {
            EditorApplication.delayCall -= BuildOpening;
            EditorApplication.update -= BuildOpening;
            EditorApplication.update += BuildOpening;
            return "已排入开场修订构建";
        }
        private static void BuildOpening()
        {
            EditorApplication.update -= BuildOpening;
            EditorApplication.delayCall -= BuildOpening;
            try { Debug.Log(BuildMac("AtelierOpening_20261011")); }
            catch(Exception exception) { Debug.LogException(exception); }
        }
        /// <summary>保存已审核的新场景并构建 macOS 试玩包；失败报告保留供诊断。</summary>
        public static string BuildMac(string outputName = "AtelierPlayable_20261011")
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("请先退出运行模式。");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=AtelierGameplayBuilder.ScenePath)throw new InvalidOperationException("当前不是新玩法场景。");
            var game=UnityEngine.Object.FindAnyObjectByType<AtelierGameplay>();
            if(game==null)throw new InvalidOperationException("场景缺少玩法入口。");
            if(game.GetComponent<AtelierGameplayEvidence>()==null)game.gameObject.AddComponent<AtelierGameplayEvidence>();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            string output="Builds/"+outputName+"/AtelierPlayable.app";
            Directory.CreateDirectory("Builds/"+outputName);Directory.CreateDirectory("Logs/AtelierGameplay20261011");
            BuildReport report;
            var oldDefault=GraphicsSettings.defaultRenderPipeline;
            var oldQuality=QualitySettings.renderPipeline;
            string defaultPath=AssetDatabase.GetAssetPath(oldDefault);
            string qualityPath=AssetDatabase.GetAssetPath(oldQuality);
            try
            {
                // Unity 6.6 的 Player 需要构建时明确包含 URP GlobalSettings；仅在场景中动态切管线会被剥离。
                var scenePipeline=UnityEngine.Object.FindAnyObjectByType<AtelierArtState>().ScenePipeline;
                GraphicsSettings.defaultRenderPipeline=scenePipeline;
                QualitySettings.renderPipeline=scenePipeline;
                report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{AtelierGameplayBuilder.ScenePath},locationPathName=output,target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
            }
            finally
            {
                // 构建的临时设置必须恢复，不能改变用户原有的共享图形和质量配置。
                // 构建会重新导入管线资源，按稳定资产路径重新取得引用，避免已释放的 Unity 对象变成空引用。
                GraphicsSettings.defaultRenderPipeline=string.IsNullOrEmpty(defaultPath)?null:AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(defaultPath);
                QualitySettings.renderPipeline=string.IsNullOrEmpty(qualityPath)?null:AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(qualityPath);
                AssetDatabase.SaveAssets();
            }
            string result="结果="+report.summary.result+"\n错误="+report.summary.totalErrors+"\n警告="+report.summary.totalWarnings+"\n大小="+report.summary.totalSize+"\n时长="+report.summary.totalTime+"\n场景="+AtelierGameplayBuilder.ScenePath;
            string messages=string.Join("\n",report.steps.SelectMany(step=>step.messages).Where(m=>m.type==LogType.Warning||m.type==LogType.Error||m.type==LogType.Exception).Select(m=>m.content));
            File.WriteAllText("Logs/AtelierGameplay20261011/"+outputName+"_构建报告.txt",result+"\n"+messages);
            var licenses="Builds/"+outputName+"/许可证";Directory.CreateDirectory(licenses);
            File.Copy("Assets/_Game/Content/ArtRebuild/来源与许可.md",licenses+"/场景素材来源与许可.md",true);
            if(File.Exists(AtelierGameplayBuilder.Content+"来源与许可.md"))File.Copy(AtelierGameplayBuilder.Content+"来源与许可.md",licenses+"/玩法新增素材来源与许可.md",true);
            foreach(string file in Directory.GetFiles("Assets/_Game/Content/TechnicalArt/LivingMatter/Licenses","*"))
                if(!file.EndsWith(".meta"))File.Copy(file,licenses+"/"+Path.GetFileName(file),true);
            File.Copy("Assets/_Game/Content/ArtRebuild/Shaders/Unity-URP-LICENSE.md",licenses+"/Unity-URP-LICENSE.md",true);
            foreach(string file in Directory.GetFiles("Assets/ThirdParty/PainterlyRendering","*-LICENSE.txt"))File.Copy(file,licenses+"/"+Path.GetFileName(file),true);
            foreach(string file in Directory.GetFiles("Assets/ThirdParty/NotoSans","*",SearchOption.AllDirectories))
                if(!file.EndsWith(".meta")&&(Path.GetFileName(file).Contains("OFL")||Path.GetFileName(file).Contains("LICENSE")))File.Copy(file,licenses+"/"+Path.GetFileName(file),true);
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException(result);
            return result;
        }
    }
}
