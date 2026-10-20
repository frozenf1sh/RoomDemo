// 文件：AtelierStageFourTests.cs
// 职责：验证点灯观察、暖星、当前版本收色、两步屋面与终局互斥及练习恢复。
// 边界：真实命令推进前三层，不使用调试授予；不能证明视觉质量或真人理解。
using System;
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    /// <summary>覆盖末阶段可以交换的顺序、拒绝路径和恢复边界。</summary>
    public sealed class AtelierStageFourTests
    {
        private GameSession game;
        private static ClueId C(string id)=>AtelierFlowRules.C(id);
        [SetUp]public void Setup()=>game=new GameSession(AtelierFlowRules.CreateOpening());
        private void Pair(string first,string second)=>game.Combine(new[] {C(first),C(second)});
        private void Form(string first,string second)=>game.Combine(new[] {new ClueId("atelier.flow."+first),new ClueId("atelier.flow."+second)});
        private void PaintThree()
        {
            for(int n=0;n<3;n++)
            {
                var doc=game.State.AtelierCanvas;
                Assert.IsTrue(game.CommitAtelierStroke(game.SessionEpoch,doc.Revision,new AtelierStroke(Guid.NewGuid().ToString(),doc.Current.Id,5000,5000,0,20)).Accepted);
            }
        }
        private void Finish()=>Assert.IsTrue(game.FinishAtelierLayer(game.SessionEpoch,game.State.AtelierCanvas.Revision).Accepted);
        private void ThirdStage()
        {
            game.Inspect(StudioRules.Cup);game.Inspect(AtelierRules.Brush);game.PerformAction(StudioRules.Cup,"wash_release_pigment");
            game.Inspect(AtelierFlowRules.Crayon);Pair("transparent","blue");Pair("flow","free");Form("ice_blue","swirl");PaintThree();Finish();
            foreach(var source in new[] {AtelierFlowRules.Plaster,AtelierFlowRules.HandMirror,AtelierFlowRules.DryBrush,AtelierFlowRules.MirrorShards}) game.Inspect(source);
            game.PerformAction(AtelierFlowRules.HandMirror,"mirror_compare");Pair("fixed","flip");Pair("black","sharp");Form("twisted","rough");PaintThree();Finish();
        }
        private void Window()
        {
            game.Inspect(StudioRules.Window);game.PerformAction(StudioRules.Window,"window_far");
            game.PerformAction(StudioRules.Window,"window_near");game.PerformAction(StudioRules.Window,"window_compare");
        }
        private void ReadyBrush()
        {
            Window();game.Inspect(AtelierFlowRules.DryPalette);game.PerformAction(AtelierFlowRules.DryPalette,"palette_inspect");
            Pair("chaos","night");Pair("reality","stars");Form("body","present");
        }

        private void FourthStage()
        {
            ThirdStage();ReadyBrush();game.Inspect(AtelierRules.Painting);game.Inspect(AtelierFlowRules.PaletteKnife);
            game.PerformAction(AtelierFlowRules.DryPalette,"palette_scrape");game.PerformAction(AtelierFlowRules.DryPalette,"palette_spread");PaintThree();Finish();
        }
        private void Light()
        {
            game.Inspect(StudioRules.LampBody);game.Inspect(AtelierRules.Matches);
            game.PerformAction(AtelierRules.Matches,"strike_match");game.PerformAction(StudioRules.LampBody,"light_wick");
        }
        private void FourthPaint() {Light();Pair("warm","bright");PaintThree();}
        private AtelierCanvasResult Tool(AtelierFinaleAction action,int a=4200,int b=5500)
            =>game.ApplyAtelierFinale(game.SessionEpoch,game.State.AtelierCanvas.Revision,action,new[] {new AtelierContactSegment(a,5000,b,5000)});
        private void OpenRoof()
        {
            game.Inspect(StudioRules.Ceiling);Assert.IsTrue(Tool(AtelierFinaleAction.ProbeRoof).Accepted);
            Assert.IsTrue(Tool(AtelierFinaleAction.ContinueRoof,6300,7800).Accepted);
        }
        [Test]public void FourthSourcesAndLampActionsStayClosedBeforeThirdLayer()
        {
            ThirdStage();
            foreach(var source in new[] {StudioRules.LampBody,AtelierRules.Matches,StudioRules.Ceiling,StudioRules.Sky,AtelierRules.SharedMark,StudioRules.Room})
            {game.Inspect(source);Assert.IsFalse(game.State.Knows(source));}
            game.PerformAction(StudioRules.LampBody,"light_wick");Assert.IsFalse(game.State.HasFact(StudioRules.LampTurned));
        }
        [Test]public void LookingAtLampDoesNotGiveWarmthAndStrikingRequiresPersonallyKnownMatches()
        {
            FourthStage();game.Inspect(StudioRules.LampBody);game.PerformAction(StudioRules.LampBody,"light_wick");
            game.PerformAction(AtelierRules.Matches,"strike_match");
            Assert.IsFalse(game.State.HasFact(AtelierRules.MatchStruck));Assert.IsFalse(game.State.Knows(C("warm")));
            Light();Assert.IsTrue(game.State.HasFact(StudioRules.LampTurned));Assert.IsTrue(game.State.Knows(C("bright")));
            Assert.IsTrue(game.State.Knows(C("warm")));Assert.IsFalse(game.State.Knows(StudioRules.Ceiling));
            Assert.IsEmpty(game.PerformAction(StudioRules.LampBody,"light_wick"));
        }
        [Test]public void WarmStarsConsumeExactlyTheTwoGrayMaterials()
        {
            FourthStage();Light();Pair("warm","bright");
            Assert.IsTrue(game.State.Knows(AtelierFlowRules.Brush(4)));
            Assert.IsFalse(game.State.CanUseClue(C("warm")));Assert.IsFalse(game.State.CanUseClue(C("bright")));
            Assert.IsTrue(game.State.Knows(StudioRules.LampBody));Assert.AreEqual(2,game.Rules.MaximumCombinationInputs);
        }
        [Test]public void BothRoutesRecheckThreeStrokesAndToolSource()
        {
            FourthStage();Light();Pair("warm","bright");game.Inspect(StudioRules.Ceiling);
            Assert.IsFalse(Tool(AtelierFinaleAction.SpreadCanvas).Accepted);Assert.IsFalse(Tool(AtelierFinaleAction.ProbeRoof).Accepted);
            PaintThree();Assert.IsTrue(Tool(AtelierFinaleAction.SpreadCanvas).Accepted);
            Assert.IsTrue(game.UndoAtelierStroke(game.SessionEpoch,game.State.AtelierCanvas.Revision).Accepted);
            Assert.IsFalse(Tool(AtelierFinaleAction.SpreadCanvas).Accepted);Assert.IsFalse(Tool(AtelierFinaleAction.ProbeRoof).Accepted);
        }
        [Test]public void SpreadUsesCurrentVersionAndNewOrUndoneStrokesInvalidateIt()
        {
            FourthStage();FourthPaint();int revision=game.State.AtelierCanvas.Revision;
            Assert.IsFalse(game.FinishAtelierLayer(game.SessionEpoch,revision).Accepted);
            Assert.IsTrue(Tool(AtelierFinaleAction.SpreadCanvas).Accepted);Assert.AreEqual(revision,game.State.AtelierCanvas.SpreadRevision);
            Assert.AreEqual(1,game.State.AtelierCanvas.SpreadPath.Count);
            Assert.IsTrue(game.CommitAtelierStroke(game.SessionEpoch,revision,new AtelierStroke("extra",4,5000,5000,0,12)).Accepted);
            Assert.AreEqual(-1,game.State.AtelierCanvas.SpreadRevision);Assert.IsEmpty(game.State.AtelierCanvas.SpreadPath);
            Assert.IsFalse(game.FinishAtelierLayer(game.SessionEpoch,game.State.AtelierCanvas.Revision).Accepted);
            Assert.IsFalse(game.ApplyAtelierFinale(game.SessionEpoch,revision,AtelierFinaleAction.SpreadCanvas,new[] {new AtelierContactSegment(4200,5000,5500,5000)}).Accepted);
        }
        [Test]public void EmptyOutsideAndTooShortContactsCannotProduceFacts()
        {
            FourthStage();FourthPaint();game.Inspect(StudioRules.Ceiling);var doc=game.State.AtelierCanvas;
            Assert.IsFalse(game.ApplyAtelierFinale(game.SessionEpoch,doc.Revision,AtelierFinaleAction.ProbeRoof,Array.Empty<AtelierContactSegment>()).Accepted);
            Assert.IsFalse(Tool(AtelierFinaleAction.ProbeRoof,-1,2000).Accepted);
            Assert.IsFalse(Tool(AtelierFinaleAction.ProbeRoof,5000,5100).Accepted);
            Assert.IsFalse(game.State.HasFact(AtelierFlowRules.RoofProbed));
            Assert.IsFalse(game.ApplyAtelierFinale(game.SessionEpoch,doc.Revision,AtelierFinaleAction.ProbeRoof,new[] {new AtelierContactSegment(5000,5000,5000,5000)},2).Accepted);
            Assert.IsTrue(game.ApplyAtelierFinale(game.SessionEpoch,doc.Revision,AtelierFinaleAction.ProbeRoof,new[] {new AtelierContactSegment(5000,5000,5000,5000)},3).Accepted);
        }
        [Test]public void FirstProbeLeavesNormalEndingAvailableAndNeverAwardsSkyOrRoom()
        {
            FourthStage();FourthPaint();game.Inspect(StudioRules.Ceiling);Assert.IsTrue(Tool(AtelierFinaleAction.ProbeRoof).Accepted);
            Assert.IsTrue(game.State.Knows(C("roof_night")));Assert.IsFalse(game.State.HasFact(AtelierFlowRules.WorldCommitted));
            Assert.IsFalse(game.State.Knows(StudioRules.Sky));Assert.IsFalse(game.State.Knows(StudioRules.Room));
            Assert.IsTrue(Tool(AtelierFinaleAction.SpreadCanvas).Accepted);Finish();Assert.IsTrue(game.State.CanvasLocked);
            Assert.IsFalse(Tool(AtelierFinaleAction.ContinueRoof,6300,7800).Accepted);
        }
        [Test]public void SecondProbeMustStartAtNewEdgeAndImmediatelyLocksNormalEnding()
        {
            FourthStage();FourthPaint();game.Inspect(StudioRules.Ceiling);
            Assert.IsFalse(Tool(AtelierFinaleAction.ContinueRoof,6300,7800).Accepted);
            Assert.IsTrue(Tool(AtelierFinaleAction.ProbeRoof).Accepted);
            Assert.IsFalse(Tool(AtelierFinaleAction.ContinueRoof,4200,5500).Accepted);
            Assert.IsFalse(Tool(AtelierFinaleAction.ContinueRoof,9000,10000).Accepted);
            Assert.IsTrue(Tool(AtelierFinaleAction.ContinueRoof,6300,7800).Accepted);
            Assert.IsTrue(game.State.HasFact(AtelierRules.RoofErased));Assert.IsTrue(game.State.HasFact(AtelierFlowRules.WorldCommitted));
            Assert.IsFalse(game.FinishAtelierLayer(game.SessionEpoch,game.State.AtelierCanvas.Revision).Accepted);
            Assert.IsFalse(game.State.Knows(StudioRules.Sky));Assert.IsFalse(game.State.Knows(StudioRules.Room));
        }
        [Test]public void OpenRoofStillNeedsPersonalSkyMarkAndRoomBeforeFinalContact()
        {
            FourthStage();FourthPaint();OpenRoof();
            Assert.IsFalse(Tool(AtelierFinaleAction.EraseRoom).Accepted);
            game.Inspect(AtelierRules.SharedMark);game.PerformAction(AtelierRules.SharedMark,"study_shared_mark");
            game.Inspect(StudioRules.Room);Assert.IsFalse(game.State.Knows(StudioRules.Room));
            game.Inspect(StudioRules.Sky);game.PerformAction(AtelierRules.SharedMark,"study_shared_mark");
            Assert.IsTrue(game.State.HasFact(AtelierRules.SharedMarkSeen));Assert.IsFalse(game.State.Knows(StudioRules.Room));
            Assert.IsFalse(Tool(AtelierFinaleAction.EraseRoom).Accepted);
            game.Inspect(StudioRules.Room);Assert.IsTrue(Tool(AtelierFinaleAction.EraseRoom).Accepted);
            Assert.IsTrue(game.State.HasFact(AtelierRules.RoomErased));Assert.IsFalse(game.State.CanvasLocked);
            Assert.IsFalse(Tool(AtelierFinaleAction.EraseRoom).Accepted);
        }
        [Test]public void PracticeSnapshotRestoresExactEarlierKnowledgePaintAndNewEpoch()
        {
            FourthStage();var snapshot=game.CaptureAtelierPractice();
            var clues=game.State.KnownClues.ToArray();var consumed=game.State.ConsumedClues.ToArray();var facts=game.State.Facts.ToArray();
            int revision=game.State.AtelierCanvas.Revision;string oldEpoch=game.SessionEpoch;
            FourthPaint();OpenRoof();
            game=GameSession.FromAtelierPractice(snapshot);
            CollectionAssert.AreEquivalent(clues,game.State.KnownClues);CollectionAssert.AreEquivalent(consumed,game.State.ConsumedClues);CollectionAssert.AreEquivalent(facts,game.State.Facts);
            Assert.AreNotEqual(oldEpoch,game.SessionEpoch);Assert.AreEqual(revision,game.State.AtelierCanvas.Revision);
            Assert.AreEqual(0,game.State.AtelierCanvas.Current.Count);Assert.AreEqual(100,game.State.AtelierCanvas.Current.RemainingPaint);
            Assert.IsTrue(game.State.AtelierCanvas.Layers.Take(3).All(l=>l.Sealed&&l.Count==3));
            Assert.IsFalse(game.CommitAtelierStroke(oldEpoch,revision,new AtelierStroke("stale",4,5000,5000,0,17)).Accepted);
            FourthPaint();Assert.IsFalse(GameSession.FromAtelierPractice(snapshot).State.HasFact(StudioRules.LampTurned));
        }
        [Test]public void InvalidPracticeCaptureAndDifferentRulesAreRejected()
        {
            Assert.Throws<InvalidOperationException>(()=>game.CaptureAtelierPractice());
            FourthStage();var snapshot=game.CaptureAtelierPractice();
            Assert.Throws<InvalidOperationException>(()=>snapshot.Restore(AtelierFlowRules.CreateOpening()));
            Light();Assert.Throws<InvalidOperationException>(()=>game.CaptureAtelierPractice());
        }
    }
}
