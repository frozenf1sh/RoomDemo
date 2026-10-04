// =============================================================================
// 文件：ExplorationPlayTests.cs
// 职责：加载实际美术场景，验证检视生命周期、相机与输入隔离。
// 边界：只断言可观察功能；不测试 Shader 像素或代替陌生玩家发现体验。
// =============================================================================
using System.Collections;
using Game.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
namespace Game.Tests
{
    public sealed class ExplorationPlayTests
    {
        private GameBootstrap bootstrap;
        private InspectionController inspection;
        private NotebookView notebook;
        [UnitySetUp] public IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Content/Scenes/DemoRoom.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            bootstrap = Object.FindAnyObjectByType<GameBootstrap>(); inspection = Object.FindAnyObjectByType<InspectionController>(); notebook = Object.FindAnyObjectByType<NotebookView>();
        }
        [UnityTest] public IEnumerator CubeInspectionRestoresCameraAndDoesNotWriteWorld()
        {
            var camera = Camera.main; var position = camera.transform.position; var rotation = camera.transform.rotation; int mask = camera.cullingMask;
            inspection.InspectDebugCube(); yield return null;
            Assert.That(inspection.IsOpen && notebook.ExternalInputBlocked, Is.True);
            Assert.That(camera.cullingMask, Is.EqualTo(1 << InspectionController.InspectionLayer));
            Assert.That(inspection.ActOnHotspot("open"), Is.True); Assert.That(inspection.Model.State, Is.EqualTo("open"));
            var before = inspection.CurrentVisual.transform.rotation; inspection.Rotate(new Vector2(80, 40)); inspection.Zoom(240);
            Assert.That(inspection.CurrentVisual.transform.rotation, Is.Not.EqualTo(before)); Assert.That(inspection.ZoomDistance, Is.LessThan(3));
            Assert.That(bootstrap.Session.State.KnownClues, Is.Empty); Assert.That(bootstrap.Session.State.Facts, Is.Empty);
            notebook.SetOpen(true); Assert.That(notebook.IsOpen, Is.False);
            inspection.Exit(); yield return null;
            Assert.That(inspection.IsOpen || notebook.ExternalInputBlocked, Is.False);
            Assert.That(camera.transform.position, Is.EqualTo(position)); Assert.That(camera.transform.rotation, Is.EqualTo(rotation)); Assert.That(camera.cullingMask, Is.EqualTo(mask));
        }
        [UnityTest] public IEnumerator RestartDuringInspectionReleasesOldCopyAndInputLock()
        {
            inspection.InspectDebugCube(); yield return null; var copy = inspection.CurrentVisual;
            bootstrap.Restart(); yield return null;
            Assert.That(copy == null, Is.True); Assert.That(inspection.IsOpen || notebook.ExternalInputBlocked, Is.False);
            Assert.That(bootstrap.Session.State.Facts, Is.Empty);
        }
    }
}
