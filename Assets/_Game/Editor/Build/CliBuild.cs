// =============================================================================
// 文件：CliBuild.cs
// 职责：提供验证后构建与连接 Editor 时的后台构建排队入口。
// 边界：只构建已保存且有效的场景；失败有报告，机器协议字段保持稳定。
// =============================================================================
using System;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Unity;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>提供验证后构建与连接 Editor 时的后台构建排队入口。</summary>
    public static class CliBuild
    {
        /// <summary>构建校验兼容入口。</summary>
        public static void ValidateScene() => DemoSceneValidator.ValidateScene();
        // Pipeline 请求的主线程超时较短，因此在下一次 Editor 更新中执行一次。
        // 使用 update 回调，使 Editor 在后台运行时也能及时开始构建。
        private static bool queued;
        /// <summary>验证后在 Editor 下一次更新排队构建，供连接式 CLI 使用。</summary>
        public static void QueueBuild()
        {
            // 构建先校验，任何无效场景都不能进入实际输出阶段。
            ValidateScene();
            if (queued || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("已有构建正在排队或执行。");
            queued = true; Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/build-summary.json", "{\"result\":\"Queued\"}");
            EditorApplication.update += RunQueuedBuild;
        }
        /// <summary>移除一次性回调，运行构建并在异常时写失败报告。</summary>
        private static void RunQueuedBuild()
        {
            // 先解除一次性回调，异常或后续 Editor 更新都不能重复执行。
            EditorApplication.update -= RunQueuedBuild; queued = false;
            try { BuildDemo(); }
            catch (Exception error)
            {
                // 报告保留稳定字段和值，CLI 使用机器协议判断是否停止流水线。
                File.WriteAllText("Logs/build-summary.json", JsonUtility.ToJson(new BuildSummaryData { result = "Failed", output = error.Message }, true));
                Debug.LogException(error);
            }
        }
        [MenuItem("游戏/构建试玩版（命令行配置）")]
        /// <summary>构建已验证的试玩场景，写报告并附带字体许可。</summary>
        public static void BuildDemo()
        {
            // 构建先校验，任何无效场景都不能进入实际输出阶段。
            ValidateScene();
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string suppliedTarget = Argument("-buildTarget");
            if (!string.IsNullOrEmpty(suppliedTarget))
            {
                if (!Enum.TryParse(suppliedTarget, true, out target)) throw new ArgumentException("无效的构建目标：" + suppliedTarget);
            }
            string extension;
            switch (target)
            {
                case BuildTarget.StandaloneOSX: extension = ".app"; break;
                case BuildTarget.StandaloneWindows64: extension = ".exe"; break;
                case BuildTarget.StandaloneLinux64: extension = ""; break;
                default: throw new NotSupportedException("试玩版仅支持桌面独立运行平台：" + target);
            }
            string output = Environment.GetEnvironmentVariable("ROOM_DEMO_BUILD_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = "Builds/" + target + "/RoomDemo" + extension;
            output = Path.GetFullPath(output); Directory.CreateDirectory(Path.GetDirectoryName(output));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(DemoSceneBuilder.ScenePath, true) };
            var options = new BuildPlayerOptions
            {
                scenes = new[] { DemoSceneBuilder.ScenePath }, locationPathName = output, target = target,
                options = BuildOptions.None
            };
            // BuildPipeline 报告是构建成功的依据，不能只检查输出文件存在。
            var report = BuildPipeline.BuildPlayer(options);
            Directory.CreateDirectory("Logs");
            // 报告保留稳定字段和值，CLI 使用机器协议判断是否停止流水线。
            File.WriteAllText("Logs/build-summary.json", JsonUtility.ToJson(new BuildSummaryData
            { result = report.summary.result.ToString(), output = output, target = target.ToString(), errors = report.summary.totalErrors, warnings = report.summary.totalWarnings, bytes = report.summary.totalSize }, true));
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("试玩版构建失败：" + report.summary.result);
            // 随试玩包提供字体许可原文；中文说明不能替代第三方许可证。
            File.Copy("Assets/ThirdParty/NotoSans/OFL.txt", Path.Combine(Path.GetDirectoryName(output), "中文字体许可原文.txt"), true);
            // 同时附带模型许可和中文来源清单，使试玩包可以独立核对第三方来源。
            File.Copy("Assets/ThirdParty/LICENSES.md", Path.Combine(Path.GetDirectoryName(output), "第三方素材来源.md"), true);
            File.Copy("Assets/ThirdParty/Kenney/FurnitureKit/License.txt", Path.Combine(Path.GetDirectoryName(output), "模型许可原文.txt"), true);
            File.Copy("Assets/ThirdParty/Kenney/NatureKit/License.txt", Path.Combine(Path.GetDirectoryName(output), "自然素材许可原文.txt"), true);
            Debug.Log("命令行构建：成功，输出位于 " + output);
        }
        /// <summary>读取 Unity 进程的命令行参数，支持显式构建目标。</summary>
        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
        /// <summary>提供验证后构建与连接 Editor 时的后台构建排队入口。</summary>
        [Serializable] private sealed class BuildSummaryData
        { public string result, output, target; public int errors, warnings; public ulong bytes; }
    }
}
