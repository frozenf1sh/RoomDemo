// =============================================================================
// 文件：PlayableLoopTests.cs
// 职责：加载真实场景，验证射线、笔记本、动画、音频状态、恢复与字体。
// 边界：自动测试证明功能，不替代首次真人理解度、时长与声音听感验收。
// =============================================================================
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
    /// <summary>加载真实场景，验证射线、笔记本、动画、音频状态、恢复与字体。</summary>
    public sealed class PlayableLoopTests
    {
        private const string ScenePath = "Assets/_Game/Content/Scenes/DemoRoom.unity";
        private GameBootstrap bootstrap;
        private NotebookView notebook;
        private InteractionSystem input;
        /// <summary>每个测试加载真实 DemoRoom，等待启动组件创建 Session。</summary>
        [UnitySetUp] public IEnumerator LoadDemo()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("DemoRoom");
#endif
            yield return null;
            bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.RestartLegacyForRegression(); // 保留旧样片的数据回归，正式新游戏使用探索规则。
            notebook = Object.FindAnyObjectByType<NotebookView>();
            input = Object.FindAnyObjectByType<InteractionSystem>();
            // 同时校验领域与真实场景的可观察表现，防止状态正确但画面未同步。
            Assert.That(bootstrap.Session, Is.Not.Null);
        }
        /// <summary>按稳定结构名称找到当前可交互按钮，不依赖中文显示文案。</summary>
        private Button FindButton(string name)
        {
            // 小提醒平时隐藏，但按钮入口仍须可验证；其他操作只从实际打开的面板取。
            if (name == "OpenNotebook") return notebook.transform.parent.Find("DebugCanvas/OpenNotebook").GetComponent<Button>();
            return Object.FindObjectsByType<Button>(FindObjectsInactive.Include).First(x => x.name == name && x.gameObject.activeInHierarchy);
        }
        private void RestartFromPause()
        {
            Object.FindAnyObjectByType<PlayerHudView>().SetPaused(true);
            FindButton("Restart").onClick.Invoke();
        }
        /// <summary>通过实体 Collider 的屏幕投影提交真实相机射线调查。</summary>
        private bool ClickObject(ObjectId id)
        {
            var entity = Object.FindObjectsByType<SceneEntity>().Single(x => x.Id == id);
            var center = entity.GetComponent<Collider>().bounds.center;
            return input.InspectAt(Camera.main.WorldToScreenPoint(center));
        }
        /// <summary>暂停菜单和小型笔记提醒的中文必须有足够行高，正式画面没有标题栏。</summary>
        [UnityTest] public IEnumerator PauseAndReminderLabelsFitTheirActualRects()
        {
            Object.FindAnyObjectByType<PlayerHudView>().SetPaused(true);
            Canvas.ForceUpdateCanvases(); yield return null;
            foreach (var name in new[] { "OpenNotebook", "Restart" })
            {
                var text = FindButton(name).GetComponentInChildren<Text>();
                Assert.That(text.preferredHeight, Is.GreaterThan(0));
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + .1f), name);
            }
        }
        /// <summary>首次获取有完整通知与实际按钮闪烁，打开即确认，重复观察不重新提醒。</summary>
        [UnityTest] public IEnumerator DiscoveryNoticeBlinksUntilReadAndRestartClearsIt()
        {
            var open = FindButton("OpenNotebook");
            var original = open.colors;
            var notice = Object.FindObjectsByType<Text>(FindObjectsInactive.Include).Single(x => x.name == "DiscoveryNoticeText");
            Assert.That(notice.gameObject.activeInHierarchy, Is.False);
            Assert.That(ClickObject(DemoRules.Plant));
            yield return null;
            Assert.That(notice.gameObject.activeInHierarchy, Is.True);
            Assert.That(notice.text, Does.Contain("新发现").And.Contain("盆栽").And.Contain("生长"));
            Canvas.ForceUpdateCanvases();
            Assert.That(notice.preferredHeight, Is.LessThanOrEqualTo(notice.rectTransform.rect.height + .1f));
            Assert.That(notebook.HasUnreadDiscoveries, Is.True);
            // Selectable 把颜色过渡写入 CanvasRenderer，Image.color 本身是固定底色。
            var firstColor = open.targetGraphic.canvasRenderer.GetColor();
            yield return new WaitForSecondsRealtime(.17f);
            Assert.That(open.targetGraphic.canvasRenderer.GetColor(), Is.Not.EqualTo(firstColor));
            // 短通知消失后仍提醒；重复观察不能刷新五秒期限。
            Assert.That(ClickObject(DemoRules.Plant));
            yield return new WaitForSecondsRealtime(5f);
            Assert.That(notice.gameObject.activeInHierarchy, Is.False);
            Assert.That(notebook.HasUnreadDiscoveries, Is.True);
            open.onClick.Invoke();
            yield return null;
            Assert.That(notebook.HasUnreadDiscoveries, Is.False);
            Assert.That(open.colors.normalColor, Is.EqualTo(original.normalColor));
            Assert.That(open.GetComponentInChildren<Text>().text, Is.EqualTo("笔记 · N"));
            notebook.SetOpen(false);
            Assert.That(ClickObject(DemoRules.Plant));
            yield return null;
            Assert.That(notebook.HasUnreadDiscoveries, Is.False);
            Assert.That(notice.gameObject.activeInHierarchy, Is.False);
            // 新的独立物体会再提醒，重开同时清除通知和按钮动画。
            Assert.That(ClickObject(DemoRules.Window));
            yield return null;
            Assert.That(notebook.HasUnreadDiscoveries, Is.True);
            Assert.That(notice.text, Does.Contain("窗户"));
            RestartFromPause();
            yield return null;
            Assert.That(notice.gameObject.activeInHierarchy, Is.False);
            Assert.That(notebook.HasUnreadDiscoveries, Is.False);
            Assert.That(open.colors.normalColor, Is.EqualTo(original.normalColor));
        }
        /// <summary>从新会话完成三次组合，确认视觉、声音状态和涌现链一致。</summary>
        [UnityTest] public IEnumerator FullLoopThroughSceneRaycastsAndNotebookButtons()
        {
            var plant = Object.FindAnyObjectByType<PlantPresenter>();
            var initialScale = plant.Visual.localScale;
            Assert.That(ClickObject(DemoRules.Plant));
            Assert.That(bootstrap.Session.State.Knows(DemoRules.Growth));
            FindButton("OpenNotebook").onClick.Invoke(); yield return null;
            Assert.That(notebook.IsOpen);
            FindButton("Object_plant").onClick.Invoke(); FindButton("Concept_growth").onClick.Invoke();
            FindButton("Interpret").onClick.Invoke();
            yield return new WaitForSeconds(1.4f);
            Assert.That(plant.GrowthProgress, Is.GreaterThan(0)); Assert.That(plant.Visual.localScale, Is.EqualTo(initialScale));
            var book = Object.FindAnyObjectByType<BookPresenter>();
            Assert.That(Vector3.Distance(book.transform.localPosition, book.FallenPosition), Is.LessThan(.01f));
            Assert.That(bootstrap.Session.State.HasFact(DemoRules.WindEntering), Is.False);
            notebook.SetOpen(false); yield return null;
            Assert.That(ClickObject(DemoRules.Book));
            Assert.That(bootstrap.Session.State.Knows(DemoRules.Far));
            Assert.That(bootstrap.Session.State.Knows(DemoRules.Window), Is.False);
            Assert.That(ClickObject(DemoRules.Window));
            FindButton("OpenNotebook").onClick.Invoke(); yield return null;
            FindButton("Object_window").onClick.Invoke(); FindButton("Concept_far").onClick.Invoke();
            FindButton("Interpret").onClick.Invoke(); yield return null;
            var wind = Object.FindAnyObjectByType<WindPresenter>();
            Assert.That(Object.FindAnyObjectByType<WindowPresenter>().FarBackground.activeSelf);
            Assert.That(wind.IsWindVisible); Assert.That(wind.WindAudio.isPlaying);
            Assert.That(Object.FindAnyObjectByType<DebugView>().Objective, Does.Contain("纸"));
            Assert.That(Object.FindAnyObjectByType<EndingPresenter>().EndingPanel.activeSelf, Is.False);
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
            Assert.That(bootstrap.Session.State.Knows(DemoRules.Painting), Is.False);
            Assert.That(ClickObject(DemoRules.Painting));
            FindButton("OpenNotebook").onClick.Invoke(); yield return null;
            FindButton("Concept_sea").onClick.Invoke(); FindButton("Object_painting").onClick.Invoke();
            FindButton("Interpret").onClick.Invoke(); yield return new WaitForSeconds(Object.FindAnyObjectByType<PaintingSeaSequence>().Duration + .2f);
            Assert.That(bootstrap.Session.State.HasFact(DemoRules.Complete)); Assert.That(bootstrap.Session.State.CompletedRecipes.Count, Is.EqualTo(3));
            Assert.That(bootstrap.Session.State.FiredReactions.Count, Is.EqualTo(8));
            Assert.That(plant.AquaticVisual.activeSelf); Assert.That(plant.AquaticProgress, Is.EqualTo(1));
            Assert.That(Object.FindAnyObjectByType<WindowPresenter>().OceanBackground.activeSelf);
            var painting = Object.FindAnyObjectByType<PaintingPresenter>(); Assert.That(painting.SeaPainting.activeSelf); Assert.That(painting.SeaAudio.isPlaying);
            Assert.That(Object.FindAnyObjectByType<WaterPresenter>().Puddle.activeSelf);
            Assert.That(Object.FindAnyObjectByType<EndingPresenter>().EndingPanel.activeSelf);
            Assert.That(notebook.IsOpen, Is.False);
        }
        /// <summary>验证核心实体与表现组件数量，并确认书可见但初始不可调查。</summary>
        [UnityTest] public IEnumerator SceneBootstrapsAllFiveEntitiesAndEightPresenters()
        {
            Assert.That(Object.FindObjectsByType<SceneEntity>().Length, Is.EqualTo(5));
            Assert.That(Object.FindObjectsByType<WorldPresenter>().Length, Is.EqualTo(8));
            Assert.That(notebook, Is.Not.Null); Assert.That(input, Is.Not.Null);
            Assert.That(ClickObject(DemoRules.Book)); Assert.That(bootstrap.Session.State.Knows(DemoRules.Book), Is.False);
            yield return null;
        }
        /// <summary>验证即时恢复、重开初始姿态与重复绑定后的事件唯一性。</summary>
        [UnityTest] public IEnumerator DebugPresetsRestoreFinalPosesAndRestartRestoresInitialWorld()
        {
            var plant = Object.FindAnyObjectByType<PlantPresenter>(); var initialScale = plant.Visual.localScale;
            var book = Object.FindAnyObjectByType<BookPresenter>(); var shelfPosition = book.transform.localPosition;
            var paper = Object.FindAnyObjectByType<PaperPresenter>(); var hidden = paper.Sheet.localPosition;
            bootstrap.JumpTo(DebugPresets.Emergence);
            Assert.That(plant.AquaticVisual.activeSelf); Assert.That(plant.AquaticProgress, Is.EqualTo(1));
            Assert.That(book.transform.localPosition, Is.EqualTo(book.FallenPosition));
            Assert.That(Object.FindAnyObjectByType<WindowPresenter>().OceanBackground.transform.localScale.y, Is.EqualTo(1));
            Assert.That(Object.FindAnyObjectByType<WaterPresenter>().Puddle.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Object.FindAnyObjectByType<EndingPresenter>().EndingPanel.activeSelf);
            RestartFromPause(); yield return null;
            Assert.That(bootstrap.Session.State.Facts, Is.Empty); Assert.That(bootstrap.Session.State.KnownClues, Is.Empty);
            Assert.That(plant.Visual.localScale, Is.EqualTo(initialScale)); Assert.That(plant.AquaticVisual.activeSelf, Is.False);
            Assert.That(book.transform.localPosition, Is.EqualTo(shelfPosition)); Assert.That(paper.Sheet.localPosition, Is.EqualTo(hidden));
            Assert.That(Object.FindObjectsByType<AudioSource>().Any(x => x.isPlaying), Is.False);
            Assert.That(Object.FindAnyObjectByType<EndingPresenter>().EndingPanel.activeSelf, Is.False);
            RestartFromPause(); ClickObject(DemoRules.Plant);
            FindButton("OpenNotebook").onClick.Invoke(); yield return null;
            FindButton("Object_plant").onClick.Invoke(); FindButton("Concept_growth").onClick.Invoke();
            var events = new System.Collections.Generic.List<DomainEvent>(); bootstrap.Session.EventRaised += events.Add;
            FindButton("Interpret").onClick.Invoke(); yield return new WaitForSeconds(1.4f);
            Assert.That(events.OfType<CombinationSucceeded>().Count(), Is.EqualTo(1)); Assert.That(plant.GrowthProgress, Is.GreaterThan(0));
        }
        /// <summary>使用实际调试按钮验证授予、跳转、非法事实与重置。</summary>
        [UnityTest] public IEnumerator DebugButtonsGrantCluesAndJumpToBeforeSeaThenAddFact()
        {
            var debug = Object.FindAnyObjectByType<DebugView>(); debug.transform.Find("DebugPanel").gameObject.SetActive(true);
            FindButton("GrantAll").onClick.Invoke(); Assert.That(bootstrap.Session.State.KnownClues.Count, Is.EqualTo(7)); Assert.That(bootstrap.Session.State.Facts, Is.Empty);
            FindButton("Preset2").onClick.Invoke(); Assert.That(bootstrap.Session.State.Facts.Count, Is.EqualTo(6)); Assert.That(bootstrap.Session.State.HasFact(DemoRules.Complete), Is.False);
            var field = Object.FindAnyObjectByType<InputField>(); field.text = "plant.gorwn"; FindButton("AddFact").onClick.Invoke(); Assert.That(bootstrap.Session.State.Facts.Count, Is.EqualTo(6));
            field.text = "painting.sea"; FindButton("AddFact").onClick.Invoke();
            Assert.That(bootstrap.Session.State.HasFact(DemoRules.Complete)); Assert.That(Object.FindAnyObjectByType<PlantPresenter>().AquaticProgress, Is.EqualTo(1));
            FindButton("DebugReset").onClick.Invoke(); yield return null; Assert.That(bootstrap.Session.State.Facts, Is.Empty);
        }
        /// <summary>多余第三槽不能误命中二元配方，清除后正常完成。</summary>
        [UnityTest] public IEnumerator ThirdNotebookSlotRejectsExtraClueWithoutConsumingIt()
        {
            bootstrap.Session.DebugGrantAllClues(); FindButton("OpenNotebook").onClick.Invoke(); yield return null;
            FindButton("Object_plant").onClick.Invoke(); FindButton("Concept_growth").onClick.Invoke(); FindButton("Concept_sea").onClick.Invoke();
            Assert.That(notebook.SelectedClues.Count, Is.EqualTo(3)); FindButton("Interpret").onClick.Invoke(); yield return null;
            Assert.That(bootstrap.Session.State.Facts, Is.Empty); Assert.That(bootstrap.Session.State.KnownClues.Count, Is.EqualTo(7)); Assert.That(notebook.IsOpen);
            FindButton("Slot2").onClick.Invoke(); FindButton("Interpret").onClick.Invoke(); yield return new WaitForSeconds(.8f);
            Assert.That(bootstrap.Session.State.HasFact(DemoRules.PlantGrown)); Assert.That(notebook.IsOpen, Is.False);
        }
        /// <summary>错误组合保持事实为空，窗景也不应改变。</summary>
        [UnityTest] public IEnumerator InvalidUiInterpretationLeavesWorldUnchanged()
        {
            ClickObject(DemoRules.Plant); ClickObject(DemoRules.Window);
            FindButton("OpenNotebook").onClick.Invoke(); yield return null;
            FindButton("Object_window").onClick.Invoke(); FindButton("Concept_growth").onClick.Invoke();
            FindButton("Interpret").onClick.Invoke(); yield return null;
            Assert.That(bootstrap.Session.State.Facts, Is.Empty);
            Assert.That(Object.FindAnyObjectByType<WindowPresenter>().FarBackground.activeSelf, Is.False);
        }
        /// <summary>验证实际 UI 和场景标签的中文字形来自嵌入字体，覆盖动态状态。</summary>
        [UnityTest] public IEnumerator ChinesePanelsAndSceneLabelsHaveRenderableGlyphs()
        {
            // 先授予碎片，再分别收集初始与最终状态的动态文案，覆盖列表和调试反馈。
            bootstrap.Session.DebugGrantAllClues();
            notebook.SetOpen(true);
            yield return null;
            var initialText = Object.FindObjectsByType<Text>(FindObjectsInactive.Include).Select(x => x.text).ToArray();
            Assert.That(initialText, Has.Some.Contains("笔记本"));
            Assert.That(initialText, Has.Some.Contains("盆栽"));
            bootstrap.JumpTo(DebugPresets.Emergence);
            notebook.SetOpen(true);
            yield return null;
            // 完成三次组合后的历史需要四行，检查实际布局没有截掉最后一个解释。
            Canvas.ForceUpdateCanvases();
            var history = notebook.transform.Find("NotebookPanel/Notebook/CompletedRecipes").GetComponent<Text>();
            Assert.That(history.text, Does.Contain("画 ─ 海"));
            Assert.That(history.preferredHeight, Is.LessThanOrEqualTo(history.rectTransform.rect.height + .1f), "中文解释历史发生截断。");
            var texts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include);
            Assert.That(texts.Select(x => x.text), Has.Some.Contains("房间记得"));
            foreach (var text in texts)
            {
                // 请求动态纹理后检查实际字形信息；源代码是中文并不代表字体能够显示。
                text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                foreach (char ch in text.text.Where(IsChinese).Distinct())
                    Assert.That(text.font.GetCharacterInfo(ch, out _, text.fontSize, text.fontStyle), Is.True, $"面板 {text.name} 缺少字形：{ch}");
            }
            foreach (var text in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include))
            {
                Assert.That(text.text.Any(IsChinese), Is.True, $"场景标签未使用中文：{text.name}");
                text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                foreach (char ch in text.text.Where(IsChinese).Distinct())
                    Assert.That(text.font.GetCharacterInfo(ch, out _, text.fontSize, text.fontStyle), Is.True, $"场景标签缺少字形：{ch}");
            }
            // 初始动态列表的字形也要验证，避免最终状态覆盖后漏查。
            var font = texts[0].font;
            foreach (var value in initialText)
            {
                font.RequestCharactersInTexture(value, 18, FontStyle.Normal);
                foreach (char ch in value.Where(IsChinese).Distinct())
                    Assert.That(font.GetCharacterInfo(ch, out _, 18), Is.True, $"初始面板缺少字形：{ch}");
            }
        }

        /// <summary>识别常用汉字范围，技术 ID、快捷键与标点不属于语言断言。</summary>
        private static bool IsChinese(char value) => value >= '\u4e00' && value <= '\u9fff';

        /// <summary>极大鼠标增量也受角度限制，观察点位置始终不变。</summary>
        [UnityTest] public IEnumerator CameraIsFixedAndClamped()
        {
            var look = Object.FindAnyObjectByType<FixedLook>(); var position = look.transform.position;
            var origin = look.transform.localRotation;
            look.ApplyLook(new Vector2(100000, -100000)); yield return null;
            var relative = Quaternion.Inverse(origin) * look.transform.localRotation;
            Assert.That(Mathf.DeltaAngle(0, relative.eulerAngles.y), Is.EqualTo(80).Within(.1));
            Assert.That(Mathf.DeltaAngle(0, relative.eulerAngles.x), Is.EqualTo(45).Within(.1));
            Assert.That(look.transform.position, Is.EqualTo(position));
        }
    }
}
