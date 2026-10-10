// 文件：AtelierPreviewBuild.cs
// 职责：只构建独立美术场景，保存真实构建结果并附来源／许可说明。
// 架构边界：不调用旧主线构建器、校验器或测试，不修改玩家设置或领域代码。
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Game.Editor.ArtRebuild
{
    /// <summary>新的画室预览包构建入口。</summary>
    public static class AtelierPreviewBuild
    {
        /// <summary>把已经保存的独立画室打包为 macOS 本地预览；失败时保留报告。</summary>
        /// <returns>构建结果与路径。</returns>
        public static string BuildMac()
        {
            AtelierSceneBuilder.Save();
            const string output = "Builds/AtelierNight_20261010";
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { AtelierSceneBuilder.ScenePath },
                locationPathName = output + "/AtelierNight.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            });
            var s = report.summary;
            string summary = $"结果={s.result}\n场景={AtelierSceneBuilder.ScenePath}\n输出={s.outputPath}\n错误={s.totalErrors}\n警告={s.totalWarnings}\n大小={s.totalSize}\n耗时={s.totalTime.TotalSeconds:F1}s\n";
            File.WriteAllText("Logs/ArtRebuild20261010/独立构建报告.txt", summary);
            File.Copy("Assets/_Game/Content/ArtRebuild/来源与许可.md", output + "/来源与许可.md", true);
            File.Copy("Assets/ThirdParty/NotoSans/OFL.txt", output + "/NotoSans-OFL.txt", true);
            if (s.result != BuildResult.Succeeded) throw new InvalidOperationException("独立画室构建失败，请检查独立构建报告。\n" + summary);
            return summary;
        }
    }
}
