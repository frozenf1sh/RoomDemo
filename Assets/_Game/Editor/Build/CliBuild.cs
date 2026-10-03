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
        public static void ValidateScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before validating/building.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoSceneBuilder.ScenePath) == null)
                throw new FileNotFoundException("Generate DemoRoom with DemoSceneBuilder.Build first.");
            var scene = SceneManager.GetSceneByPath(DemoSceneBuilder.ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(DemoSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().Single(x => x.name == "DemoRoom");
                foreach (var name in new[] { "_Bootstrap", "_Systems", "CameraRig", "Environment", "Interactables", "Effects", "UI" })
                    if (root.transform.Find(name) == null) throw new InvalidOperationException("Missing scene group: " + name);
                if (root.GetComponentsInChildren<GameBootstrap>(true).Length != 1 || root.GetComponentsInChildren<NotebookView>(true).Length != 1 ||
                    root.GetComponentsInChildren<InteractionSystem>(true).Length != 1 || root.GetComponentsInChildren<Camera>(true).Length != 1)
                    throw new InvalidOperationException("Scene systems are missing or duplicated.");
                var entities = root.GetComponentsInChildren<SceneEntity>(true);
                if (entities.Length != 3 || entities.Select(x => x.Id).Distinct().Count() != 3 ||
                    DemoRules.Create().Objects.Any(x => !entities.Any(e => e.Id == x.Id)))
                    throw new InvalidOperationException("Expected exactly Plant, Book, Window.");
                foreach (var entity in entities)
                    if (entity.GetComponent<Collider>() == null || entity.GetComponent<Clickable>() == null || entity.transform.parent.Find("Visual") == null)
                        throw new InvalidOperationException("Invalid Logic/Visual composition: " + entity.Id);
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null) throw new InvalidOperationException("Scene contains a missing script.");
                    var serialized = new SerializedObject(component); var property = serialized.GetIterator();
                    while (property.NextVisible(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script" && property.objectReferenceValue == null &&
                            component.GetType().Namespace == "Game.Unity")
                            throw new InvalidOperationException($"Missing reference: {component.name}/{component.GetType().Name}.{property.propertyPath}");
                }
                var audio = root.GetComponentInChildren<WindPresenter>(true);
                if (audio.WindAudio.clip == null) throw new InvalidOperationException("Wind audio missing.");
                Debug.Log("CliBuild: scene validation passed.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
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
