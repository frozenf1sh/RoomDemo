// =============================================================================
// 文件：PresentationSequenceTests.cs
// 职责：验证领域即时闭包与视觉延后顺序、取消、恢复和调试预览隔离。
// 边界：不验证 Shader 像素或玩家情绪，真人理解度仍需独立试玩。
// =============================================================================
using System.Collections;
using System.Linq;
using Game.Domain;
using Game.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
namespace Game.Tests
{
    /// <summary>对真实房间演出时序和生命周期进行功能验证。</summary>
    public sealed class PresentationSequenceTests
    {
        private GameBootstrap bootstrap;
        private PaintingSeaSequence sequence;
        private PaintingPresenter painting;
        private WaterPresenter water;
        private PlantPresenter plant;
        private WindowPresenter window;
        private EndingPresenter ending;
        /// <summary>每次重新加载真实场景，缓存所有主物体，不在测试循环中查找。</summary>
        [UnitySetUp] public IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Content/Scenes/Archive/DemoRoom.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("DemoRoom");
#endif
            yield return null;
            bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            // 只在旧演出回归夹具选择兼容数据，正式场景初始化仍走新的探索内容表。
            bootstrap.RestartLegacyForRegression(); // 保留旧样片的数据回归，正式新游戏使用探索规则。
            sequence = Object.FindAnyObjectByType<PaintingSeaSequence>();
            painting = Object.FindAnyObjectByType<PaintingPresenter>(); water = Object.FindAnyObjectByType<WaterPresenter>();
            plant = Object.FindAnyObjectByType<PlantPresenter>(); window = Object.FindAnyObjectByType<WindowPresenter>(); ending = Object.FindAnyObjectByType<EndingPresenter>();
            Assert.That(sequence, Is.Not.Null);
        }
        /// <summary>使用现有预设和正式组合 API，不能用视觉预览伪装领域链通过。</summary>
        private void Begin()
        {
            bootstrap.JumpTo(DebugPresets.BeforeSea);
            bootstrap.Session.Combine(new[] { new ClueId("sea"), new ClueId("painting") });
        }
        /// <summary>Fact 当帧全部成立，四个视觉节拍和最后氛围依次开始。</summary>
        [UnityTest] public IEnumerator DomainClosureIsImmediateWhileHeroBeatsAreStaged()
        {
            Begin();
            Assert.That(bootstrap.Session.State.HasFact(DemoRules.Complete));
            Assert.That(sequence.IsPlaying); Assert.That(sequence.IsBenchmarkPending);
            Assert.That(painting.TransitionProgress, Is.Zero); Assert.That(water.Puddle.activeSelf, Is.False);
            Assert.That(plant.AquaticVisual.activeSelf, Is.False); Assert.That(window.OceanBackground.activeSelf, Is.False);
            Assert.That(ending.EndingPanel.activeSelf, Is.False);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(painting.TransitionProgress, Is.GreaterThan(0)); Assert.That(painting.SeaAudio.isPlaying);
            Assert.That(water.Puddle.activeSelf, Is.False); Assert.That(plant.AquaticVisual.activeSelf, Is.False);
            yield return new WaitForSecondsRealtime(4.6f);
            Assert.That(water.Puddle.activeSelf); Assert.That(water.RevealProgress, Is.GreaterThan(0));
            Assert.That(plant.AquaticVisual.activeSelf, Is.False); Assert.That(window.OceanBackground.activeSelf, Is.False);
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(plant.AquaticProgress, Is.GreaterThan(0)); Assert.That(window.OceanBackground.activeSelf, Is.False);
            yield return new WaitForSecondsRealtime(3.2f);
            Assert.That(window.OceanProgress, Is.GreaterThan(0)); Assert.That(ending.EndingPanel.activeSelf, Is.False);
            yield return new WaitForSecondsRealtime(3.2f);
            Assert.That(ending.EndingPanel.activeSelf);
            yield return new WaitForSecondsRealtime(Mathf.Max(.2f, sequence.Duration - sequence.Elapsed + .2f));
            Assert.That(sequence.IsPlaying, Is.False); Assert.That(sequence.IsBenchmarkPending, Is.False);
            Assert.That(painting.TransitionProgress, Is.EqualTo(1)); Assert.That(plant.AquaticProgress, Is.EqualTo(1));
            Assert.That(window.OceanProgress, Is.EqualTo(1)); Assert.That(ending.AtmosphereProgress, Is.EqualTo(1));
        }
        /// <summary>重开、终态预设和跳过都取消旧协程；重复播放请求不能重置正在进行的演出。</summary>
        [UnityTest] public IEnumerator RestartPresetAndSkipCancelOldPresentation()
        {
            Begin(); yield return new WaitForSecondsRealtime(2.5f);
            bootstrap.Restart();
            Assert.That(sequence.IsPlaying, Is.False); Assert.That(painting.TransitionProgress, Is.Zero);
            Assert.That(Object.FindObjectsByType<AudioSource>().Any(x => x.isPlaying), Is.False);
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(water.Puddle.activeSelf, Is.False); Assert.That(bootstrap.Session.State.Facts, Is.Empty);
            bootstrap.JumpTo(DebugPresets.Emergence);
            Assert.That(sequence.IsPlaying, Is.False); Assert.That(painting.TransitionProgress, Is.EqualTo(1));
            Assert.That(plant.AquaticProgress, Is.EqualTo(1)); Assert.That(window.OceanProgress, Is.EqualTo(1));
            Begin(); yield return new WaitForSecondsRealtime(.4f);
            float before = sequence.Elapsed;
            sequence.PlayAnimated(); sequence.PlayAnimated();
            Assert.That(sequence.Elapsed, Is.GreaterThanOrEqualTo(before));
            sequence.ApplyFinalStateImmediately();
            Assert.That(sequence.IsPlaying, Is.False); Assert.That(painting.TransitionProgress, Is.EqualTo(1)); Assert.That(ending.EndingPanel.activeSelf);
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(window.OceanProgress, Is.EqualTo(1));
        }
        /// <summary>正式解释会接管调试预览，防止真实演出结束后一直显示未完成。</summary>
        [UnityTest] public IEnumerator RealInterpretationTakesOverAnActivePreview()
        {
            bootstrap.JumpTo(DebugPresets.BeforeSea);
            sequence.PlayBenchmarkPreview(); yield return new WaitForSecondsRealtime(1.5f);
            bootstrap.Session.Combine(new[] { new ClueId("sea"), new ClueId("painting") });
            Assert.That(sequence.IsPlaying); Assert.That(sequence.Elapsed, Is.LessThan(.1f));
            Assert.That(painting.TransitionProgress, Is.Zero); Assert.That(bootstrap.Session.State.HasFact(DemoRules.Complete));
            sequence.ApplyFinalStateImmediately(); Assert.That(sequence.IsBenchmarkPending, Is.False);
        }
        /// <summary>单独调演出不会产生事实、碎片、配方或反应；恢复回到实际领域初始状态。</summary>
        [UnityTest] public IEnumerator PreviewNeverChangesDomainAndRestoreUsesActualFacts()
        {
            sequence.PlayBenchmarkPreview(); yield return new WaitForSecondsRealtime(2.5f);
            sequence.ApplyFinalPreview();
            Assert.That(painting.TransitionProgress, Is.EqualTo(1)); Assert.That(ending.EndingPanel.activeSelf);
            Assert.That(bootstrap.Session.State.Facts, Is.Empty); Assert.That(bootstrap.Session.State.KnownClues, Is.Empty);
            Assert.That(bootstrap.Session.State.CompletedRecipes, Is.Empty); Assert.That(bootstrap.Session.State.FiredReactions, Is.Empty);
            sequence.ApplyFinalStateImmediately();
            Assert.That(painting.TransitionProgress, Is.Zero); Assert.That(plant.AquaticVisual.activeSelf, Is.False);
            Assert.That(window.OceanBackground.activeSelf, Is.False); Assert.That(water.Puddle.activeSelf, Is.False);
            Assert.That(ending.EndingPanel.activeSelf, Is.False);
            Assert.That(Object.FindObjectsByType<AudioSource>().Any(x => x.isPlaying), Is.False);
        }
    }
}
