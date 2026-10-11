// =============================================================================
// 文件：AtelierRouteTests.cs
// 职责：验证新画室的主动交稿、亲点来源和可换序的现实路线。
// 边界：只验证领域因果，不宣称证明画面质量、真人理解度或动画接触。
// =============================================================================
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>通过正常调查、比较和动作建立状态，不调试授予完整路线。</summary>
    public sealed class AtelierRouteTests
    {
        private GameSession game;
        [SetUp] public void Setup() => game = new GameSession(AtelierRules.Create());
        private void Pair(ClueId a, ClueId b) => game.Combine(new[] { a, b });
        private void Learn(ObjectId source, string before, string action, string after, ClueId idea)
        {
            game.Inspect(source);
            game.ObserveDetail(source, before);
            if (source == StudioRules.LampBody)
            {
                game.Inspect(AtelierRules.Matches);
                game.PerformAction(AtelierRules.Matches, "strike_match");
            }
            game.PerformAction(source, action);
            game.ObserveDetail(source, after);
            Pair(AtelierRules.Observation(before), AtelierRules.Observation(after));
            Assert.IsTrue(game.State.Knows(idea));
        }
        private void LearnThree()
        {
            Learn(StudioRules.Cup, "still", "wash", "spread", StudioRules.Flow);
            Learn(StudioRules.LampBody, "dark", "light_wick", "bright", StudioRules.Light);
            Learn(StudioRules.Curtain, "hidden", "release_curtain", "revealed", StudioRules.Open);
        }
        private void Apply(ClueId idea, ObjectId target)
        {
            game.Inspect(target);
            Pair(idea, new ClueId(target.Value));
        }
        private void FinishPainting()
        {
            SketchAll();
            Apply(StudioRules.Flow, StudioRules.PictureWater);
            Apply(StudioRules.Light, StudioRules.PictureLamp);
            Apply(StudioRules.Open, StudioRules.PictureWindow);
        }
        private void SketchAll()
        {
            game.Inspect(AtelierRules.Painting); game.Inspect(AtelierRules.Brush);
            foreach (var source in new[] { StudioRules.Cup, AtelierRules.Brush, AtelierRules.StillLamp, StudioRules.Window })
            {
                game.Inspect(source);
                game.Combine(source == AtelierRules.Brush
                    ? new[] { new ClueId(AtelierRules.Painting.Value), new ClueId(AtelierRules.Brush.Value) }
                    : new[] { new ClueId(AtelierRules.Painting.Value), new ClueId(source.Value), new ClueId(AtelierRules.Brush.Value) });
            }
        }
        [Test] public void BlankCanvasAndNoteNeverGrantStillLifeObjects()
        {
            game.Inspect(AtelierRules.Manuscript); game.Inspect(AtelierRules.Painting);
            game.Inspect(StudioRules.PictureWater);
            Assert.IsFalse(game.State.Knows(StudioRules.PictureWater));
            Assert.IsFalse(game.State.Knows(StudioRules.Cup));
            Assert.IsFalse(game.State.Knows(AtelierRules.Brush));
            Assert.IsFalse(game.State.HasFact(AtelierRules.CupSketched));
        }
        [Test] public void PaintingObjectsIsNeutralAndWaterMustBeClickedSeparately()
        {
            SketchAll();
            Assert.IsTrue(game.State.HasFact(AtelierRules.StillLifePainted));
            Assert.AreEqual(0, game.State.ConsecutiveCompliance);
            Assert.AreEqual(0, game.State.DeviationCount);
            Assert.IsFalse(game.State.HasFact(AtelierRules.ReadyToDeliver));
            Assert.IsFalse(game.State.Knows(StudioRules.PictureWater));
            game.Inspect(StudioRules.PictureWater);
            Assert.IsTrue(game.State.Knows(StudioRules.PictureWater));
            Assert.IsFalse(game.State.HasFact(StudioRules.PictureFlow));
        }
        [Test] public void ObservationAndPhysicalActionNeverGrantFinishedConceptOrAnotherObject()
        {
            game.Inspect(StudioRules.Cup);
            game.ObserveDetail(StudioRules.Cup, "spread");
            Assert.IsFalse(game.State.Knows(AtelierRules.Observation("spread")));
            game.ObserveDetail(StudioRules.Cup, "still");
            game.PerformAction(StudioRules.Cup, "wash");
            Assert.IsFalse(game.State.Knows(StudioRules.Flow));
            Assert.IsFalse(game.State.Knows(StudioRules.PictureWater));
            Assert.AreEqual(0, game.State.DeviationCount);
            game.ObserveDetail(StudioRules.Cup, "spread");
            Pair(AtelierRules.Observation("still"), AtelierRules.Observation("spread"));
            Assert.IsTrue(game.State.Knows(StudioRules.Flow));
        }
        [Test] public void ThirdPaintCompletionWaitsForExplicitDeliveryAndStillAllowsExperiment()
        {
            LearnThree(); FinishPainting();
            Assert.IsTrue(game.State.HasFact(AtelierRules.ReadyToDeliver));
            Assert.IsFalse(game.State.CanvasLocked);
            Apply(StudioRules.Flow, StudioRules.Cup);
            Assert.IsTrue(game.State.HasFact(StudioRules.WaterFlow));
            game.Inspect(AtelierRules.Painting);
            game.PerformAction(AtelierRules.Painting, "deliver");
            Assert.IsFalse(game.State.HasFact(AtelierRules.CanvasDelivered));
        }
        [Test] public void DeliveryClosesOnlyTheOrdinaryPaintingRoute()
        {
            LearnThree(); FinishPainting(); game.Inspect(AtelierRules.Painting);
            game.PerformAction(AtelierRules.Painting, "deliver");
            Assert.IsTrue(game.State.CanvasLocked);
            Assert.IsTrue(game.State.HasFact(AtelierRules.CanvasDelivered));
            Apply(StudioRules.Flow, StudioRules.Cup);
            Assert.IsFalse(game.State.HasFact(StudioRules.WaterFlow));
        }
        [TestCase(true)] [TestCase(false)] public void SkyAndRoofNeedNeitherWaterLightNorLocalFrameErasure(bool lightFirst)
        {
            LearnThree();
            Learn(AtelierRules.Trial, "wet", "wipe_trial", "base", AtelierRules.Erase);
            Apply(AtelierRules.Erase, StudioRules.Ceiling);
            Apply(lightFirst ? StudioRules.Light : StudioRules.Flow, StudioRules.Sky);
            Assert.IsFalse(game.State.HasFact(StudioRules.FullSky));
            Apply(lightFirst ? StudioRules.Flow : StudioRules.Light, StudioRules.Sky);
            Assert.IsTrue(game.State.HasFact(StudioRules.FullSky));
            Assert.IsFalse(game.State.HasFact(StudioRules.WaterLit));
            Assert.IsFalse(game.State.HasFact(AtelierRules.FrameErased));
            game.Inspect(StudioRules.Room);
            Assert.IsFalse(game.State.Knows(StudioRules.Room));
            game.Inspect(AtelierRules.SharedMark);
            game.PerformAction(AtelierRules.SharedMark, "observe_whole");
            Assert.IsFalse(game.State.Knows(StudioRules.Room));
            game.Inspect(StudioRules.Room);
            Apply(AtelierRules.Erase, StudioRules.Room);
            Assert.IsTrue(game.State.HasFact(AtelierRules.RoomErased));
        }
        [Test] public void UnconfirmedErasePreparationHasNoDomainSideEffects()
        {
            Learn(AtelierRules.Trial, "wet", "wipe_trial", "base", AtelierRules.Erase);
            game.Inspect(StudioRules.Ceiling);
            var recipe = game.PreviewCombination(new[] { AtelierRules.Erase, new ClueId(StudioRules.Ceiling.Value) });
            Assert.IsNotNull(recipe);
            Assert.IsFalse(game.State.HasFact(AtelierRules.RoofErased));
            Assert.AreEqual(0, game.State.DeviationCount);
            Assert.IsFalse(game.State.HasCompleted(recipe.Id));
        }
        [Test] public void StrikingRequiresOwnMatchClickAndNeverGrantsLampOrLight()
        {
            game.PerformAction(AtelierRules.Matches, "strike_match");
            Assert.IsFalse(game.State.HasFact(AtelierRules.MatchStruck));
            game.Inspect(AtelierRules.Matches);
            game.PerformAction(AtelierRules.Matches, "strike_match");
            Assert.IsTrue(game.State.HasFact(AtelierRules.MatchStruck));
            Assert.IsFalse(game.State.Knows(StudioRules.LampBody));
            Assert.IsFalse(game.State.Knows(StudioRules.Light));
            Assert.IsFalse(game.State.HasFact(StudioRules.LampTurned));
        }
        [Test] public void LightingNeedsStruckMatchAndSeparateBrightObservation()
        {
            game.Inspect(StudioRules.LampBody);
            game.ObserveDetail(StudioRules.LampBody, "dark");
            game.PerformAction(StudioRules.LampBody, "light_wick");
            Assert.IsFalse(game.State.HasFact(StudioRules.LampTurned));
            game.Inspect(AtelierRules.Matches);
            game.PerformAction(AtelierRules.Matches, "strike_match");
            game.PerformAction(StudioRules.LampBody, "light_wick");
            Assert.IsTrue(game.State.HasFact(StudioRules.LampTurned));
            Assert.IsFalse(game.State.Knows(AtelierRules.Observation("bright")));
            Assert.IsFalse(game.State.Knows(StudioRules.Light));
            game.ObserveDetail(StudioRules.LampBody, "bright");
            Pair(AtelierRules.Observation("dark"), AtelierRules.Observation("bright"));
            Assert.IsTrue(game.State.Knows(StudioRules.Light));
            Assert.IsFalse(game.State.Knows(StudioRules.PictureLamp));
        }
    }
}
