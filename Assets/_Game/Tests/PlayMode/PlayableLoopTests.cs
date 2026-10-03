using System.Collections;
using System.Linq;
using Game.Domain;
using Game.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Game.Tests
{
    public sealed class PlayableLoopTests
    {
        private const string ScenePath = "Assets/_Game/Content/Scenes/DemoRoom.unity";
        private GameBootstrap bootstrap;
        private NotebookView notebook;
        private InteractionSystem input;
        [UnitySetUp] public IEnumerator LoadDemo()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("DemoRoom");
#endif
            yield return null;
            bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            notebook = Object.FindFirstObjectByType<NotebookView>();
            input = Object.FindFirstObjectByType<InteractionSystem>();
            Assert.That(bootstrap.Session, Is.Not.Null);
        }
        private Button FindButton(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(x => x.name == name && x.gameObject.activeInHierarchy);
        private bool ClickObject(ObjectId id)
        {
            var entity = Object.FindObjectsByType<SceneEntity>(FindObjectsSortMode.None).Single(x => x.Id == id);
            var center = entity.GetComponent<Collider>().bounds.center;
            return input.InspectAt(Camera.main.WorldToScreenPoint(center));
        }
        [UnityTest] public IEnumerator FullLoopThroughSceneRaycastsAndNotebookButtons()
        {
            var plant = Object.FindFirstObjectByType<PlantPresenter>();
            var initialScale = plant.Visual.localScale;
            Assert.That(ClickObject(DemoRules.Plant));
            Assert.That(bootstrap.Session.State.Knows(DemoRules.Growth));
            FindButton("OpenNotebook").onClick.Invoke(); yield return null;
            Assert.That(notebook.IsOpen);
            FindButton("Object_plant").onClick.Invoke(); FindButton("Concept_growth").onClick.Invoke();
            FindButton("Interpret").onClick.Invoke();
            yield return new WaitForSeconds(1.4f);
            Assert.That(plant.Visual.localScale.x, Is.GreaterThan(initialScale.x * 1.7f));
            var book = Object.FindFirstObjectByType<BookPresenter>();
            Assert.That(Vector3.Distance(book.transform.localPosition, book.FallenPosition), Is.LessThan(.01f));
            Assert.That(bootstrap.Session.State.HasFact(DemoRules.WindEntering), Is.False);
            notebook.SetOpen(false); yield return null;
            Assert.That(ClickObject(DemoRules.Book));
            Assert.That(bootstrap.Session.State.Knows(DemoRules.Far));
            FindButton("OpenNotebook").onClick.Invoke(); yield return null;
            FindButton("Object_window").onClick.Invoke(); FindButton("Concept_far").onClick.Invoke();
            FindButton("Interpret").onClick.Invoke(); yield return null;
            var wind = Object.FindFirstObjectByType<WindPresenter>();
            Assert.That(Object.FindFirstObjectByType<WindowPresenter>().FarBackground.activeSelf);
            Assert.That(wind.IsWindVisible); Assert.That(wind.WindAudio.isPlaying);
            Assert.That(bootstrap.Session.State.Facts.Count, Is.EqualTo(6));
            var rotation = plant.Visual.localRotation;
            float maxAngle = 0;
            for (int i = 0; i < 15; i++)
            { yield return new WaitForSeconds(.04f); maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(rotation, plant.Visual.localRotation)); }
            Assert.That(maxAngle, Is.GreaterThan(.5f));
            Assert.That(bootstrap.Session.State.FiredReactions.Count, Is.EqualTo(4));
            yield return new WaitForSeconds(2.2f);
            Assert.That(notebook.IsOpen, Is.False);
            Assert.That(ClickObject(DemoRules.Paper)); Assert.That(bootstrap.Session.State.Knows(DemoRules.Sea));
            FindButton("OpenNotebook").onClick.Invoke(); yield return null;
            FindButton("Concept_sea").onClick.Invoke(); FindButton("Object_painting").onClick.Invoke();
            FindButton("Interpret").onClick.Invoke(); yield return new WaitForSeconds(3.2f);
            Assert.That(bootstrap.Session.State.HasFact(DemoRules.Complete)); Assert.That(bootstrap.Session.State.CompletedRecipes.Count, Is.EqualTo(3));
            Assert.That(bootstrap.Session.State.FiredReactions.Count, Is.EqualTo(8));
            Assert.That(plant.AquaticVisual.activeSelf); Assert.That(plant.Visual.localScale.y, Is.GreaterThan(2.3f));
            Assert.That(Object.FindFirstObjectByType<WindowPresenter>().OceanBackground.activeSelf);
            var painting = Object.FindFirstObjectByType<PaintingPresenter>(); Assert.That(painting.SeaPainting.activeSelf); Assert.That(painting.SeaAudio.isPlaying);
            Assert.That(Object.FindFirstObjectByType<WaterPresenter>().Puddle.activeSelf);
            Assert.That(Object.FindFirstObjectByType<EndingPresenter>().EndingPanel.activeSelf);
            Assert.That(notebook.IsOpen, Is.False);
        }
        [UnityTest] public IEnumerator SceneBootstrapsAllFiveEntitiesAndEightPresenters()
        {
            Assert.That(Object.FindObjectsByType<SceneEntity>(FindObjectsSortMode.None).Length, Is.EqualTo(5));
            Assert.That(Object.FindObjectsByType<WorldPresenter>(FindObjectsSortMode.None).Length, Is.EqualTo(8));
            Assert.That(notebook, Is.Not.Null); Assert.That(input, Is.Not.Null);
            Assert.That(ClickObject(DemoRules.Book)); Assert.That(bootstrap.Session.State.Knows(DemoRules.Book), Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator InvalidUiInterpretationLeavesWorldUnchanged()
        {
            ClickObject(DemoRules.Plant); ClickObject(DemoRules.Window);
            FindButton("OpenNotebook").onClick.Invoke(); yield return null;
            FindButton("Object_window").onClick.Invoke(); FindButton("Concept_growth").onClick.Invoke();
            FindButton("Interpret").onClick.Invoke(); yield return null;
            Assert.That(bootstrap.Session.State.Facts, Is.Empty);
            Assert.That(Object.FindFirstObjectByType<WindowPresenter>().FarBackground.activeSelf, Is.False);
        }
        [UnityTest] public IEnumerator CameraIsFixedAndClamped()
        {
            var look = Object.FindFirstObjectByType<FixedLook>(); var position = look.transform.position;
            var origin = look.transform.localRotation;
            look.ApplyLook(new Vector2(100000, -100000)); yield return null;
            var relative = Quaternion.Inverse(origin) * look.transform.localRotation;
            Assert.That(Mathf.DeltaAngle(0, relative.eulerAngles.y), Is.EqualTo(80).Within(.1));
            Assert.That(Mathf.DeltaAngle(0, relative.eulerAngles.x), Is.EqualTo(45).Within(.1));
            Assert.That(look.transform.position, Is.EqualTo(position));
        }
    }
}
