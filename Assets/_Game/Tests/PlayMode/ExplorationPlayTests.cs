// =============================================================================
// 文件：ExplorationPlayTests.cs
// 职责：加载实际美术场景，验证检视生命周期、相机与输入隔离。
// 边界：只断言可观察功能；不测试 Shader 像素或代替陌生玩家发现体验。
// =============================================================================
using System.Collections;
using System.Linq;
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
        [UnityTest] public IEnumerator PlantRequiresPersonalHotspotAndSuitableViewAngle()
        {
            var entity = System.Linq.Enumerable.Single(Object.FindObjectsByType<SceneEntity>(), x => x.Id == Game.Domain.DemoRules.Plant);
            Assert.That(inspection.Enter(entity.GetComponent<InspectableObject>()), Is.True); yield return null;
            Assert.That(bootstrap.Session.State.Knows(Game.Domain.DemoRules.Growth), Is.False);
            var sprout = System.Linq.Enumerable.Single(inspection.CurrentVisual.GetComponentsInChildren<InspectionHotspot>(), x => x.ActionId == "sprout");
            Assert.That(inspection.ObserveAt(Camera.main.WorldToScreenPoint(sprout.transform.position)), Is.False);
            inspection.Rotate(new Vector2(180 / .35f, 0)); yield return null; Physics.SyncTransforms();
            Assert.That(inspection.ObserveAt(Camera.main.WorldToScreenPoint(sprout.transform.position)), Is.True);
            Assert.That(bootstrap.Session.State.Knows(Game.Domain.ExplorationRules.Sprout), Is.True);
            Assert.That(bootstrap.Session.State.Knows(Game.Domain.DemoRules.Growth), Is.False);
            Assert.That(notebook.HasUnreadDiscoveries, Is.True);
            inspection.Exit(); yield return null;
        }
        [UnityTest] public IEnumerator BookCoverPagesAndQuoteUseInspectionStateAndDomainDiscovery()
        {
            var entity = System.Linq.Enumerable.Single(Object.FindObjectsByType<SceneEntity>(), x => x.Id == Game.Domain.DemoRules.Book);
            Assert.That(inspection.Enter(entity.GetComponent<InspectableObject>()), Is.True); yield return null;
            Assert.That(inspection.ActOnHotspot("quote"), Is.False);
            Assert.That(inspection.ActOnHotspot("cover"), Is.True);
            Assert.That(inspection.ActOnHotspot("quote"), Is.False);
            Assert.That(inspection.ActOnHotspot("next1"), Is.True); yield return new WaitForSecondsRealtime(.35f);
            var quote = System.Linq.Enumerable.Single(inspection.CurrentVisual.GetComponentsInChildren<InspectionHotspot>(), x => x.ActionId == "quote"); Physics.SyncTransforms();
            Assert.That(inspection.ObserveAt(Camera.main.WorldToScreenPoint(quote.transform.position)), Is.True);
            Assert.That(bootstrap.Session.State.Knows(Game.Domain.ExplorationRules.TowardLight), Is.True);
            Assert.That(bootstrap.Session.State.Knows(Game.Domain.DemoRules.Growth), Is.False);
            Assert.That(quote.transform.Find("ObservedUnderline").gameObject.activeSelf, Is.True);
            inspection.Exit(); yield return null;
        }
        [UnityTest] public IEnumerator GrowthInsightStaysOnPaperAndDoesNotChangeWorld()
        {
            bootstrap.JumpTo(Game.Domain.ExplorationPresets.BeforeGrowthInsight);
            notebook.SetOpen(true); notebook.SelectClue(Game.Domain.ExplorationRules.Sprout); notebook.SelectClue(Game.Domain.ExplorationRules.TowardLight); notebook.InterpretSelected();
            Assert.That(notebook.ConceptFormationPlaying, Is.True);
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(notebook.IsOpen, Is.True); Assert.That(bootstrap.Session.State.Facts, Is.Empty);
            Assert.That(bootstrap.Session.State.Knows(Game.Domain.DemoRules.Growth), Is.True);
            var word = System.Linq.Enumerable.Single(Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include), x => x.name == "InsightWord");
            // 文本字段有内容仍可能因行高不足而生成空网格，必须检查真实绘制数据。
            Canvas.ForceUpdateCanvases();
            Assert.That(word.preferredHeight, Is.LessThanOrEqualTo(word.rectTransform.rect.height + .1f));
            Assert.That(word.cachedTextGenerator.verts.Count, Is.GreaterThanOrEqualTo(8), "生长概念实际没有被绘制。");
            Assert.That(word.text, Is.EqualTo("生长"));
            notebook.SelectClue(Game.Domain.DemoRules.Clue("growth")); notebook.SelectClue(Game.Domain.DemoRules.Clue("plant"));
            Assert.That(notebook.SelectedClues.Count, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator GrowthInterpretationHasSmallImmediateChangeAndBookStaysOnShelf()
        {
            bootstrap.JumpTo(Game.Domain.ExplorationPresets.GrowthUnlocked);
            var plant = Object.FindAnyObjectByType<PlantPresenter>(); var scale = plant.Visual.localScale;
            notebook.SetOpen(true); notebook.SelectClue(Game.Domain.DemoRules.Clue("growth")); notebook.SelectClue(Game.Domain.DemoRules.Clue("plant")); notebook.InterpretSelected();
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(bootstrap.Session.State.HasFact(Game.Domain.ExplorationRules.PlantStartsGrowing), Is.True);
            Assert.That(plant.Visual.localScale, Is.EqualTo(scale));
            Assert.That(plant.GrowthProgress, Is.GreaterThan(0).And.LessThan(.53f));
            Assert.That(plant.GrownVisual.activeSelf, Is.True);
            Assert.That(bootstrap.Session.State.HasFact(Game.Domain.DemoRules.BookFallen), Is.False);
            Assert.That(notebook.IsOpen, Is.False);
        }
        [UnityTest] public IEnumerator NextDayUpdatesDomainImmediatelyThenRestoresMatureWorldBehindFade()
        {
            bootstrap.JumpTo(Game.Domain.ExplorationPresets.BeforeDayAdvance);
            var day = Object.FindAnyObjectByType<DayTransitionController>(); var plant = Object.FindAnyObjectByType<PlantPresenter>(); var scale = plant.Visual.localScale;
            var book = Object.FindAnyObjectByType<BookPresenter>(); var shelf = book.transform.localPosition;
            day.Begin(); Assert.That(bootstrap.Session.State.DayIndex, Is.EqualTo(2)); Assert.That(bootstrap.Session.State.HasFact(Game.Domain.DemoRules.BookFallen), Is.True);
            Assert.That(book.transform.localPosition, Is.EqualTo(shelf));
            yield return new WaitForSecondsRealtime(3.1f);
            Assert.That(day.IsTransitioning || notebook.ExternalInputBlocked, Is.False);
            Assert.That(plant.Visual.localScale, Is.EqualTo(scale)); Assert.That(plant.GrowthProgress, Is.EqualTo(1));
            Assert.That(Vector3.Distance(book.transform.localPosition, book.FallenPosition), Is.LessThan(.001f));
            bootstrap.JumpTo(Game.Domain.ExplorationPresets.AfterGrowthDay); yield return null;
            Assert.That(day.IsTransitioning, Is.False); Assert.That(book.transform.localPosition, Is.EqualTo(book.FallenPosition));
        }
        [UnityTest] public IEnumerator RestartDuringDayFadeCancelsOldSequenceAndRestoresDayOne()
        {
            bootstrap.JumpTo(Game.Domain.ExplorationPresets.BeforeDayAdvance); var day = Object.FindAnyObjectByType<DayTransitionController>(); day.Begin();
            yield return new WaitForSecondsRealtime(.2f); bootstrap.Restart(); yield return new WaitForSecondsRealtime(2.6f);
            Assert.That(bootstrap.Session.State.DayIndex, Is.EqualTo(1)); Assert.That(bootstrap.Session.State.Facts, Is.Empty);
            Assert.That(day.IsTransitioning || notebook.ExternalInputBlocked, Is.False);
        }
        /// <summary>完全新游戏，经真实房间射线与实际近景热点完成案例 A，不借调试预设。</summary>
        [UnityTest] public IEnumerator FreshGameExplorationInsightInterpretationAndNextDay()
        {
            var input = Object.FindAnyObjectByType<InteractionSystem>();
            var plant = System.Linq.Enumerable.Single(Object.FindObjectsByType<SceneEntity>(), x => x.Id == Game.Domain.DemoRules.Plant);
            Assert.That(input.InspectAt(Camera.main.WorldToScreenPoint(plant.GetComponent<Collider>().bounds.center)), Is.True);
            yield return null; Assert.That(bootstrap.Session.State.Knows(Game.Domain.DemoRules.Growth), Is.False);
            inspection.Rotate(new Vector2(180 / .35f, 0)); yield return null; Physics.SyncTransforms();
            var sprout = System.Linq.Enumerable.Single(inspection.CurrentVisual.GetComponentsInChildren<InspectionHotspot>(), x => x.ActionId == "sprout");
            Assert.That(inspection.ObserveAt(Camera.main.WorldToScreenPoint(sprout.transform.position)), Is.True); inspection.Exit(); yield return null;
            var book = System.Linq.Enumerable.Single(Object.FindObjectsByType<SceneEntity>(), x => x.Id == Game.Domain.DemoRules.Book);
            Assert.That(input.InspectAt(Camera.main.WorldToScreenPoint(book.GetComponent<Collider>().bounds.center)), Is.True); yield return null;
            Assert.That(inspection.IsOpen, Is.True); inspection.ActOnHotspot("cover"); inspection.ActOnHotspot("next1"); yield return new WaitForSecondsRealtime(.35f);
            var quote = System.Linq.Enumerable.Single(inspection.CurrentVisual.GetComponentsInChildren<InspectionHotspot>(), x => x.ActionId == "quote"); Physics.SyncTransforms();
            Assert.That(inspection.ObserveAt(Camera.main.WorldToScreenPoint(quote.transform.position)), Is.True); inspection.Exit(); yield return null;
            notebook.SetOpen(true); notebook.SelectClue(Game.Domain.ExplorationRules.Sprout); notebook.SelectClue(Game.Domain.ExplorationRules.TowardLight); notebook.InterpretSelected();
            yield return new WaitForSecondsRealtime(1.8f); notebook.SelectClue(Game.Domain.DemoRules.Clue("growth")); notebook.SelectClue(Game.Domain.DemoRules.Clue("plant")); notebook.InterpretSelected();
            yield return new WaitForSecondsRealtime(1.5f); Assert.That(bootstrap.Session.State.HasFact(Game.Domain.DemoRules.BookFallen), Is.False);
            Object.FindAnyObjectByType<DayTransitionController>().Begin(); yield return new WaitForSecondsRealtime(3.1f);
            Assert.That(bootstrap.Session.State.DayIndex, Is.EqualTo(2)); Assert.That(bootstrap.Session.State.HasFact(Game.Domain.DemoRules.BookFallen), Is.True);
            Assert.That(bootstrap.Session.State.KnownClues.Count, Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator RestartDuringInspectionReleasesOldCopyAndInputLock()
        {
            inspection.InspectDebugCube(); yield return null; var copy = inspection.CurrentVisual;
            bootstrap.Restart(); yield return null;
            Assert.That(copy == null, Is.True); Assert.That(inspection.IsOpen || notebook.ExternalInputBlocked, Is.False);
            Assert.That(bootstrap.Session.State.Facts, Is.Empty);
        }
        /// <summary>默认玩家 HUD 不泄露物体名或开发键，显式开发开关仍可进入检视。</summary>
        [UnityTest] public IEnumerator PlayerModeHidesLabelsAndDevelopmentToolsAreExplicit()
        {
            Assert.That(bootstrap.DevelopmentMode, Is.False);
            Assert.That(Object.FindObjectsByType<TextMesh>().Length, Is.Zero);
            var hud = notebook.transform.parent.Find("DebugCanvas");
            var controls = hud.Find("Footer/Controls").GetComponent<UnityEngine.UI.Text>();
            Assert.That(controls.text, Does.Not.Contain("F3").And.Not.Contain("F7"));
            Assert.That(hud.Find("DebugPanel").gameObject.activeSelf || hud.Find("WorldTools").gameObject.activeSelf, Is.False);
            bootstrap.SetDevelopmentMode(true); Assert.That(controls.text, Does.Contain("F7"));
            Assert.That(Object.FindObjectsByType<TextMesh>().Length, Is.GreaterThan(0));
            hud.Find("WorldTools").gameObject.SetActive(true);
            hud.Find("WorldTools/WorldAction7").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
            Assert.That(inspection.IsOpen, Is.True); Assert.That(inspection.Model.State, Is.EqualTo("closed"));
            Assert.That(hud.Find("Header/Title").gameObject.activeSelf, Is.False);
            Assert.That(hud.Find("Header/OpenNotebook").gameObject.activeInHierarchy, Is.False);
            Assert.That(hud.Find("OpenNotebook").gameObject.activeInHierarchy, Is.True);
            inspection.Exit(); bootstrap.SetDevelopmentMode(false); yield return null;
            Assert.That(Object.FindObjectsByType<TextMesh>().Length, Is.Zero);
            Assert.That(hud.Find("Header/Title").gameObject.activeSelf, Is.False);
        }
        /// <summary>近景字形要位于纸面内；验证几何尺寸，不进行 Shader 像素比对。</summary>
        [UnityTest] public IEnumerator BookTextAndNotebookColumnsFitTheirActualSurfaces()
        {
            var book = System.Linq.Enumerable.Single(Object.FindObjectsByType<SceneEntity>(), x => x.Id == Game.Domain.DemoRules.Book);
            inspection.Enter(book.GetComponent<InspectableObject>()); inspection.ActOnHotspot("cover"); inspection.ActOnHotspot("next1");
            yield return new WaitForSecondsRealtime(.4f);
            var words = inspection.CurrentVisual.GetComponentsInChildren<TextMesh>();
            foreach (var word in words)
            {
                var bounds = word.GetComponent<Renderer>().bounds;
                Assert.That(bounds.size.y, Is.LessThan(.35f), "书中文字过大：" + word.name);
                Assert.That(Mathf.Abs(bounds.center.x - inspection.CurrentVisual.transform.position.x) + bounds.extents.x, Is.LessThan(1.52f), "书中文字超过页边：" + word.name);
            }
            inspection.Exit(); bootstrap.JumpTo(Game.Domain.ExplorationPresets.BeforeGrowthInsight); notebook.SetOpen(true); yield return null;
            var body = notebook.transform.Find("NotebookPanel/Notebook");
            foreach (string list in new[] { "Objects", "Observations", "Concepts" }) Assert.That(body.Find(list), Is.Not.Null);
            Assert.That(notebook.transform.Find("NotebookPanel/Observations"), Is.Null, "分类列不得挂在全屏遮罩上。");
            // 检查正式主线全部八条关系，不只检查历史海画版的几行。
            var rules = bootstrap.Session.Rules;
            bootstrap.JumpTo(new Game.Domain.DebugPreset("layout", System.Linq.Enumerable.Select(rules.Clues, x => x.Id).ToArray(),
                new[] { Game.Domain.ExplorationRules.PlantStartsGrowing, Game.Domain.ExplorationRules.PlantOvergrown, Game.Domain.DemoRules.WindowFar, Game.Domain.DemoRules.PaintingSea, Game.Domain.MainlineRules.NightArrived, Game.Domain.MainlineRules.CeilingStarry, Game.Domain.MainlineRules.WorldForest },
                System.Linq.Enumerable.Select(rules.Recipes, x => x.Id).ToArray(), "完整笔记布局", 3));
            notebook.SetOpen(true); yield return null; Canvas.ForceUpdateCanvases();
            var history = body.Find("CompletedRecipes").GetComponent<UnityEngine.UI.Text>();
            Assert.That(history.text, Does.Contain("新芽 ─ 向光 → 生长"));
            Assert.That(history.preferredHeight, Is.LessThanOrEqualTo(history.rectTransform.rect.height + .1f), "左列关系被裁切。");
            var second=body.Find("CompletedRecipesSecond").GetComponent<UnityEngine.UI.Text>();
            Assert.That(second.text, Does.Contain("森林 ─ 房间"));
            Assert.That(second.preferredHeight, Is.LessThanOrEqualTo(second.rectTransform.rect.height + .1f), "右列关系被裁切。");
        }
    }
}
