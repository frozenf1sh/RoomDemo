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
    public static class CliBuild
    {
        public static void ValidateScene() => DemoSceneValidator.ValidateScene();
        // A live Pipeline request has a short server deadline. Queue the build on the next
        // Editor update and let the CLI poll the file; batch executeMethod calls BuildDemo directly.
        public static void QueueBuild()
        {
            ValidateScene();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/build-summary.json", "{\"result\":\"Queued\"}");
            EditorApplication.delayCall += () =>
            {
                try { BuildDemo(); }
                catch (Exception error)
                {
                    File.WriteAllText("Logs/build-summary.json", JsonUtility.ToJson(new BuildSummaryData { result = "Failed", output = error.Message }, true));
                    Debug.LogException(error);
                }
            };
        }
        [MenuItem("Game/Build Demo (CLI settings)")]
        public static void BuildDemo()
        {
            ValidateScene();
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string suppliedTarget = Argument("-buildTarget");
            if (!string.IsNullOrEmpty(suppliedTarget))
            {
                if (!Enum.TryParse(suppliedTarget, true, out target)) throw new ArgumentException("Invalid build target: " + suppliedTarget);
            }
            string extension;
            switch (target)
            {
                case BuildTarget.StandaloneOSX: extension = ".app"; break;
                case BuildTarget.StandaloneWindows64: extension = ".exe"; break;
                case BuildTarget.StandaloneLinux64: extension = ""; break;
                default: throw new NotSupportedException("Demo supports desktop standalone targets only: " + target);
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
            var report = BuildPipeline.BuildPlayer(options);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/build-summary.json", JsonUtility.ToJson(new BuildSummaryData
            { result = report.summary.result.ToString(), output = output, target = target.ToString(), errors = report.summary.totalErrors, warnings = report.summary.totalWarnings, bytes = report.summary.totalSize }, true));
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Demo build failed: " + report.summary.result);
            Debug.Log("CliBuild: succeeded at " + output);
        }
        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
        [Serializable] private sealed class BuildSummaryData
        { public string result, output, target; public int errors, warnings; public ulong bytes; }
    }
}
