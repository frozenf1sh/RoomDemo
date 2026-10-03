using System;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Game.Editor
{
    public static class DemoSceneValidator
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
                if (entities.Length != 5 || entities.Select(x => x.Id).Distinct().Count() != 5 ||
                    DemoRules.Create().Objects.Any(x => !entities.Any(e => e.Id == x.Id)))
                    throw new InvalidOperationException("Expected exactly Plant, Book, Window, Painting, Paper.");
                var required = new[] { typeof(PlantPresenter), typeof(BookPresenter), typeof(WindowPresenter), typeof(WindPresenter), typeof(PaintingPresenter), typeof(PaperPresenter), typeof(WaterPresenter), typeof(EndingPresenter) };
                var presenters = root.GetComponentsInChildren<WorldPresenter>(true);
                if (required.Any(t => presenters.Count(p => p.GetType() == t) != 1)) throw new InvalidOperationException("Core presenters are missing or duplicated.");
                var bindings = new SerializedObject(root.GetComponentInChildren<GameBootstrap>(true)).FindProperty("presenters");
                if (bindings.arraySize != presenters.Length || Enumerable.Range(0, bindings.arraySize).Select(i => bindings.GetArrayElementAtIndex(i).objectReferenceValue).Distinct().Count() != presenters.Length ||
                    presenters.Any(p => !Enumerable.Range(0, bindings.arraySize).Any(i => bindings.GetArrayElementAtIndex(i).objectReferenceValue == p)))
                    throw new InvalidOperationException("Bootstrap must bind every presenter exactly once.");
                if (scene.isDirty) throw new InvalidOperationException("Save DemoRoom before validation/build; unsaved edits must not be silently built.");
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
                foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
                    if (source.clip == null) throw new InvalidOperationException("Missing audio clip: " + source.name);
                if (audio.WindAudio.clip == null) throw new InvalidOperationException("Wind audio missing.");
                Debug.Log("DemoSceneValidator: scene validation passed.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
