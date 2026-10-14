// =============================================================================
// 文件：AtelierRoomErasureRoutePlayTests.cs
// 职责：按正常游戏速度验证从湿色擦除到整间画室收束的演出流程。
// 边界：通过应用会话和场景交互入口驱动规则；不把自动输入写成真人鼠标验收。
// =============================================================================
using System.Collections;
using Game.Domain;
using Game.Unity.ArtRebuild;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>验证由现实解释转入擦开整间画室的收束路线。</summary>
    [TestFixture]
    public sealed class AtelierRoomErasureRoutePlayTests : AtelierPlayableFlowTestBase
    {
        /// <summary>从干净的 AtelierPlayable 启动并完成整间画室擦除路线。</summary>
        [UnityTest]
        public IEnumerator CanEraseWholeRoomRoute()
        {
            Assert.That(Session.State.Facts, Is.Empty, "路线从空白会话开始。");
            PrepareFlowConcept();
            PrepareLightConcept();

            // 擦前观察、实际擦除动作与擦后观察共同形成规则需要的概念。
            See(AtelierRules.Trial, "wet");
            Act(AtelierRules.Trial, "wipe_trial", AtelierRules.TrialWiped);
            See(AtelierRules.Trial, "base");
            Interpret(AtelierRules.Observation("wet"), AtelierRules.Observation("base"));
            Assert.That(Session.State.Knows(AtelierRules.Erase), Is.True,
                "湿色前后观察应联想出擦除。");

            Inspect(StudioRules.Ceiling);
            Interpret(AtelierRules.Erase, Clue(StudioRules.Ceiling));
            Assert.That(Session.State.HasFact(AtelierRules.RoofErased), Is.True,
                "擦除与屋顶组合后应启动屋顶显露。");
            yield return WaitForFrames(() => Game.Finale.RoofSettled, 3000,
                "屋顶表现落稳后应开放天空拾取。");
            // 屋顶显露后先停在普通夜色，方便检查梁木、擦痕与开口边缘。
            yield return new WaitForSecondsRealtime(2f);

            var sky = Target(AtelierTargetKind.Sky);
            Game.Observe(sky, Vector2.zero);
            Assert.That(Session.State.Knows(Clue(StudioRules.Sky)), Is.True,
                "只有屋顶实际显露后才能取得天空词条。");
            // 给天空近景和拾取反馈留出完整展示时间，再开始写入解释。
            yield return new WaitForSecondsRealtime(2f);
            Interpret(StudioRules.Flow, Clue(StudioRules.Sky));
            Assert.That(Session.State.HasFact(AtelierRules.RealityTouched), Is.True,
                "把流动用于天空后应进入现实偏离路线。");
            Interpret(StudioRules.Light, Clue(StudioRules.Sky));
            Assert.That(Session.State.HasFact(StudioRules.FullSky), Is.True,
                "点亮和流动两种解释应汇成完整星夜。");
            // 留在完整星夜一会儿，让 GPU 颜料与星点变化能被看见。
            yield return new WaitForSecondsRealtime(3f);

            // 统一笔迹需先完整显露，再由玩家分别调查笔迹与房间。
            yield return WaitForFrames(
                () => Shader.GetGlobalFloat("_AtelierMark") >= .99f,
                3000,
                "完整星夜后应显露可调查的接连笔迹。");
            var mark = Target(AtelierTargetKind.SharedMark);
            Game.ViewCamera.transform.LookAt(mark.transform.position);
            Game.Observe(mark, Vector2.zero);
            Assert.That(Session.State.HasFact(AtelierRules.SharedMarkSeen), Is.True,
                "调用接连笔迹交互入口后应记录整体观察。");
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(Session.State.Knows(Clue(StudioRules.Room)), Is.False,
                "调查接连笔迹不能代替亲点整间画室。");

            var room = Target(AtelierTargetKind.Room);
            Game.ViewCamera.transform.LookAt(room.transform.position);
            Game.Observe(room, Vector2.zero);
            Assert.That(Session.State.Knows(Clue(StudioRules.Room)), Is.True,
                "调用房间交互入口后才应认识房间。");
            yield return new WaitForSecondsRealtime(1.5f);
            Interpret(AtelierRules.Erase, Clue(StudioRules.Room));
            Assert.That(Session.State.HasFact(AtelierRules.RoomErased), Is.True,
                "提交擦除与房间后应启动统一世界笔势。");
            Assert.That(Session.State.HasFact(AtelierRules.CanvasDelivered), Is.False,
                "现实路线不能伪装成已定稿交稿。");

            yield return WaitForFrames(() => Game.Finale.Finished, 3000,
                "统一笔势应按正常表现速度擦完整间画室。");
            Assert.That(Shader.GetGlobalFloat("_AtelierRoom"), Is.GreaterThan(.999f),
                "全房间表面应共用同一个最终擦除进度。");
            yield return new WaitForSecondsRealtime(4f);
        }
    }
}
