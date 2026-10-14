// =============================================================================
// 文件：AtelierDeliveryRoutePlayTests.cs
// 职责：从可玩主场景验证主动定稿路线，并提供双路线测试共享夹具。
// 边界：通过应用会话和真实笔记画布提交；不模拟真人鼠标、绘画手势或验收像素效果。
// =============================================================================
using System;
using System.Collections;
using System.Linq;
using Game.Application;
using Game.Domain;
using Game.Unity;
using Game.Unity.ArtRebuild;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace Game.Tests
{
    /// <summary>为两条画室主线测试加载开发场景并保留正常表现速度。</summary>
    public abstract class AtelierPlayableFlowTestBase
    {
        private const string ScenePath = "Assets/_Game/Content/Scenes/AtelierPlayable.unity";
        private float originalTimeScale;
        private float originalMaximumDeltaTime;
        /// <summary>当前测试场景的游戏控制器。</summary>
        protected AtelierGameplay Game { get; private set; }
        /// <summary>当前测试场景使用的唯一事实会话。</summary>
        protected GameSession Session => Game.Session;

        /// <summary>绕过正式 Build Settings 的夜景入口，直接加载可玩开发副本。</summary>
        [UnitySetUp]
        public IEnumerator LoadPlayableScene()
        {
            originalTimeScale = Time.timeScale;
            originalMaximumDeltaTime = Time.maximumDeltaTime;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("该流程测试需要 Unity Editor 直接加载未加入 Build Settings 的 AtelierPlayable。");
            yield break;
#endif
            for (int frame = 0; frame < 8; frame++)
            {
                Game = UnityEngine.Object.FindAnyObjectByType<AtelierGameplay>();
                if (Game != null && Game.Session != null)
                    break;
                yield return null;
            }

            Assert.That(Game, Is.Not.Null, "可玩场景必须包含 AtelierGameplay。");
            Assert.That(Game.Session, Is.Not.Null, "可玩场景必须已创建 GameSession。");
            Assert.That(Game.Finale, Is.Not.Null, "可玩主场景必须配置后半段表现组件。");

            // 测试通过显式命令驱动场景，关闭设备轮询以免编辑器鼠标影响结果。
            Game.enabled = false;
            if (Game.Continuation != null)
                Game.Continuation.enabled = false;
            // 维持场景原速，让屋顶、星夜、镜头与全房间擦除能在 Game 视图中看清。
            Time.timeScale = 1f;
        }

        /// <summary>恢复 Unity 全局时间，避免影响后续 PlayMode 测试或编辑器。</summary>
        [UnityTearDown]
        public IEnumerator RestoreTimeSettings()
        {
            Time.timeScale = originalTimeScale;
            Time.maximumDeltaTime = originalMaximumDeltaTime;
            yield return null;
        }

        /// <summary>把稳定物体身份映射到其统一碎片身份。</summary>
        protected static ClueId Clue(ObjectId id) => new ClueId(id.Value);

        /// <summary>执行合法调查命令，并确认对应物体词条进入会话。</summary>
        protected void Inspect(ObjectId source)
        {
            Session.Inspect(source);
            Assert.That(Session.State.Knows(Clue(source)), Is.True,
                $"调查命令后应认识物体：{source.Value}");
        }

        /// <summary>通过会话提交一次细节观察，供快速准备有限配方输入。</summary>
        protected void See(ObjectId source, string detail)
        {
            Inspect(source);
            Session.ObserveDetail(source, detail);
            Assert.That(Session.State.Knows(AtelierRules.Observation(detail)), Is.True,
                $"观察细节后应记录：{detail}");
        }

        /// <summary>通过会话提交场景动作并核对领域事实。</summary>
        protected void Act(ObjectId source, string action, FactId expected)
        {
            Session.PerformAction(source, action);
            Assert.That(Session.State.HasFact(expected), Is.True,
                $"动作 {action} 应产生事实：{expected.Value}");
        }

        /// <summary>把已发现碎片放进真实笔记画布，再调用其解释按钮提交。</summary>
        protected void Interpret(params ClueId[] clues)
        {
            Assert.That(clues.Length, Is.InRange(2, 3), "画布只接受两个或三个词条。");
            Game.Notebook.SetOpen(true, false);
            var canvas = Game.Notebook.GetComponentInChildren<NotebookCanvasView>(true);
            Assert.That(canvas, Is.Not.Null, "主场景必须配置有限语义画布。");
            canvas.Clear();
            for (int i = 0; i < clues.Length; i++)
            {
                Assert.That(Session.State.Knows(clues[i]), Is.True,
                    $"不能把未发现的碎片放入笔记：{clues[i].Value}");
                var position = clues.Length == 2
                    ? new Vector2(i == 0 ? -90 : 90, 25)
                    : new Vector2(i == 0 ? -120 : i == 1 ? 0 : 120, 25);
                Assert.That(canvas.Add(clues[i], position), Is.Not.Null,
                    $"已知碎片应能放进笔记：{clues[i].Value}");
            }

            // 用场景中实际配置的按钮提交，配方匹配仍只发生在 GameSession。
            var submit = Game.Notebook.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "提交解释");
            Assert.That(submit.gameObject.activeInHierarchy && submit.interactable, Is.True,
                "放入两个以上词条后应显示可提交按钮。");
            submit.onClick.Invoke();
            canvas.Clear();
            Game.Notebook.SetOpen(false, false);
        }

        /// <summary>通过真实主场景输入入口观察指定交互目标。</summary>
        protected AtelierInteractionTarget Target(AtelierTargetKind kind)
        {
            return UnityEngine.Object.FindObjectsByType<AtelierInteractionTarget>()
                .Single(target => target.Kind == kind);
        }

        /// <summary>以最多若干帧等待正常速度的场景表现达到明确条件。</summary>
        protected IEnumerator WaitForFrames(Func<bool> condition, int maximumFrames, string description)
        {
            for (int frame = 0; frame < maximumFrames && !condition(); frame++)
                yield return null;
            Assert.That(condition(), Is.True,
                $"{description}（timeScale={Time.timeScale:F2}，deltaTime={Time.deltaTime:F4}，" +
                $"屋顶进度={Shader.GetGlobalFloat("_AtelierRoof"):F3}，" +
                $"表现组件启用={Game.Finale != null && Game.Finale.isActiveAndEnabled}）");
        }

        /// <summary>准备由实际擦前、擦后观察形成的“流动”概念。</summary>
        protected void PrepareFlowConcept()
        {
            See(StudioRules.Cup, "still");
            Act(StudioRules.Cup, "wash", StudioRules.Washed);
            See(StudioRules.Cup, "spread");
            Interpret(AtelierRules.Observation("still"), AtelierRules.Observation("spread"));
            Assert.That(Session.State.Knows(StudioRules.Flow), Is.True, "前后观察应联想出流动。");
        }

        /// <summary>准备由灯芯点燃前后观察形成的“点亮”概念。</summary>
        protected void PrepareLightConcept()
        {
            See(StudioRules.LampBody, "dark");
            Inspect(AtelierRules.Matches);
            Act(AtelierRules.Matches, "strike_match", AtelierRules.MatchStruck);
            Act(StudioRules.LampBody, "light_wick", StudioRules.LampTurned);
            See(StudioRules.LampBody, "bright");
            Interpret(AtelierRules.Observation("dark"), AtelierRules.Observation("bright"));
            Assert.That(Session.State.Knows(StudioRules.Light), Is.True, "灯芯前后观察应联想出点亮。");
        }
    }

    /// <summary>验证空白画布经三次画内解释后仍须主动确认定稿。</summary>
    [TestFixture]
    public sealed class AtelierDeliveryRoutePlayTests : AtelierPlayableFlowTestBase
    {
        /// <summary>从干净的 AtelierPlayable 启动并完成“就交这张”路线。</summary>
        [UnityTest]
        public IEnumerator CanConfirmCanvasDeliveryRoute()
        {
            Assert.That(Session.State.Facts, Is.Empty, "路线从空白会话开始。");
            Assert.That(Shader.GetGlobalFloat("_AtelierRoof"), Is.Zero, "开局屋顶擦除进度必须为零。");

            PrepareFlowConcept();
            PrepareLightConcept();
            See(StudioRules.Curtain, "hidden");
            Act(StudioRules.Curtain, "release_curtain", StudioRules.CurtainReleased);
            See(StudioRules.Curtain, "revealed");
            Interpret(AtelierRules.Observation("hidden"), AtelierRules.Observation("revealed"));
            Assert.That(Session.State.Knows(StudioRules.Open), Is.True, "帘子前后观察应联想出打开。");

            // 以四条正式静物配方建立画中来源，保留交稿路线的领域前置条件。
            Inspect(AtelierRules.Painting);
            Inspect(AtelierRules.Brush);
            Inspect(AtelierRules.StillLamp);
            Inspect(StudioRules.Window);
            Interpret(Clue(AtelierRules.Painting), Clue(StudioRules.Cup), Clue(AtelierRules.Brush));
            Interpret(Clue(AtelierRules.Painting), Clue(AtelierRules.Brush));
            Interpret(Clue(AtelierRules.Painting), Clue(AtelierRules.StillLamp), Clue(AtelierRules.Brush));
            Interpret(Clue(AtelierRules.Painting), Clue(StudioRules.Window), Clue(AtelierRules.Brush));
            Assert.That(Session.State.HasFact(AtelierRules.StillLifePainted), Is.True,
                "杯、笔、灯与窗都画上后，才能检视画内细节。");

            Inspect(StudioRules.PictureWater);
            Inspect(StudioRules.PictureLamp);
            Inspect(StudioRules.PictureWindow);
            Interpret(StudioRules.Flow, Clue(StudioRules.PictureWater));
            Interpret(StudioRules.Light, Clue(StudioRules.PictureLamp));
            Interpret(StudioRules.Open, Clue(StudioRules.PictureWindow));
            Assert.That(Session.State.HasFact(AtelierRules.ReadyToDeliver), Is.True,
                "三次画内解释应准备好定稿。");
            Assert.That(Session.State.HasFact(AtelierRules.CanvasDelivered), Is.False,
                "完成第三次解释不能自动定稿。");
            Assert.That(Session.State.CanvasLocked, Is.False,
                "只有再次看画并主动提交后才锁定画布。");

            var painting = Target(AtelierTargetKind.Painting);
            Game.Observe(painting, new Vector2(.1f, .1f));
            Game.Observe(painting, new Vector2(.1f, .1f));
            // 留出近看镜头和定稿按钮的观看时间，再执行自动确认。
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(Game.IsPaintingFocused, Is.True, "定稿入口只在近看画布后开放。");
            Assert.That(Game.ContextAction.gameObject.activeInHierarchy, Is.True,
                "近看画布后应显示主动定稿按钮。");
            yield return new WaitForSecondsRealtime(1.5f);
            Game.ContextAction.onClick.Invoke();

            Assert.That(Session.State.HasFact(AtelierRules.CanvasDelivered), Is.True,
                "点击定稿按钮后应记录画稿定下。");
            Assert.That(Session.State.CanvasLocked, Is.True, "主动定稿后解释输入应锁定。");
            Assert.That(Session.State.HasFact(AtelierRules.RealityTouched), Is.False,
                "纯画内路线不应登记现实偏离。");
            Assert.That(Session.State.HasFact(AtelierRules.RoofErased), Is.False,
                "普通定稿保留完整屋顶。");
            Assert.That(Session.State.HasFact(StudioRules.FullSky), Is.False,
                "普通定稿不能提前生成完整星夜。");
            Assert.That(Shader.GetGlobalFloat("_AtelierRoof"), Is.Zero,
                "普通定稿保留完整屋顶。");
            yield return new WaitForSecondsRealtime(3f);
        }
    }
}
