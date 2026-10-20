// 文件：AtelierStageFourTests.cs
// 职责：验证屋顶与房间解释、后移分流、天空两次点击和旧画保护。
// 边界：使用正常命令；演出时长、Shader表现与真人理解度另行检查。
using System;
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    /// <summary>覆盖普通定稿与现实路线仍能主动选择的边界。</summary>
    public sealed class AtelierStageFourTests
    {
        private GameSession game;
        [SetUp]public void Setup(){game=new GameSession(AtelierFlowRules.CreateOpening());AtelierFlowTestPath.Fourth(game);}
        private AtelierCanvasResult Tool(AtelierFinaleAction action,int a=4200,int b=5500)
            =>game.ApplyAtelierFinale(game.SessionEpoch,game.State.AtelierCanvas.Revision,action,new[] {new AtelierContactSegment(a,5000,b,5000)});
        private void Explain(ObjectId source,string property)
        {
            game.Inspect(source);game.Combine(new[] {new ClueId(source.Value),AtelierFlowRules.C(property)});
        }
        private void Roof()
        {
            Explain(StudioRules.Ceiling,"dry_paint");Assert.IsTrue(Tool(AtelierFinaleAction.ProbeRoof).Accepted);
            Assert.IsTrue(Tool(AtelierFinaleAction.ContinueRoof,6300,7800).Accepted);
        }
        private void Paint(){AtelierFlowTestPath.Pair(game,"warm","bright");AtelierFlowTestPath.Paint(game);}
        private void Erase(){Roof();Explain(StudioRules.Room,"wet_paint");Assert.IsTrue(Tool(AtelierFinaleAction.EraseRoom).Accepted);}
        [Test]public void LookingAtLampDoesNotGiveWarmthAndMatchesMustBePersonal()
        {
            game.Inspect(StudioRules.LampBody);game.PerformAction(StudioRules.LampBody,"light_wick");
            Assert.IsFalse(game.State.Knows(AtelierFlowRules.C("warm")));
            AtelierFlowTestPath.Light(game);Assert.IsTrue(game.State.Knows(AtelierFlowRules.C("warm")));
            Assert.IsFalse(game.State.Knows(StudioRules.Ceiling));
        }
        [Test]public void RoofRequiresAnInterpretationAndCanBeExploredBeforePaintingFourthLayer()
        {
            AtelierFlowTestPath.Light(game);game.Inspect(StudioRules.Ceiling);
            Assert.IsFalse(Tool(AtelierFinaleAction.ProbeRoof).Accepted);
            Roof();Assert.IsTrue(game.State.HasFact(AtelierRules.RoofErased));
            Assert.IsFalse(game.State.HasFact(AtelierFlowRules.WorldCommitted));
            Assert.IsFalse(game.State.Knows(StudioRules.Room));Assert.IsFalse(game.State.Knows(StudioRules.Sky));
            Assert.IsFalse(game.State.CanUseClue(AtelierFlowRules.C("dry_paint")));
        }
        [Test]public void BothRoofContactsStillAllowOrdinaryFinalization()
        {
            AtelierFlowTestPath.Light(game);Roof();Paint();
            Assert.IsTrue(Tool(AtelierFinaleAction.SpreadCanvas).Accepted);AtelierFlowTestPath.Finish(game);
            Assert.IsTrue(game.State.CanvasLocked);Assert.IsFalse(Tool(AtelierFinaleAction.EraseRoom).Accepted);
        }
        [Test]public void RoomNeedsItsOwnClickInterpretationButNotTheFourthLayer()
        {
            AtelierFlowTestPath.Light(game);Roof();
            game.Combine(new[] {new ClueId(StudioRules.Room.Value),AtelierFlowRules.C("wet_paint")});Assert.IsFalse(game.State.HasFact(AtelierFlowRules.RoomAsPaint));
            Explain(StudioRules.Room,"wet_paint");Assert.IsTrue(Tool(AtelierFinaleAction.EraseRoom).Accepted);
            Assert.IsTrue(game.State.HasFact(AtelierFlowRules.WorldCommitted));Assert.IsFalse(game.State.HasFact(AtelierFlowRules.SkyFinished));
            Assert.IsFalse(game.FinishAtelierLayer(game.SessionEpoch,game.State.AtelierCanvas.Revision).Accepted);
        }
        [Test]public void SkyMustBePersonallyObservedAndInterpretedBeforeClicks()
        {
            AtelierFlowTestPath.Light(game);Paint();Erase();
            Assert.IsFalse(game.State.Knows(StudioRules.Sky));Assert.IsFalse(game.ClickAtelierSky(game.SessionEpoch,false).Accepted);
            game.Inspect(StudioRules.Sky);Assert.IsFalse(game.ClickAtelierSky(game.SessionEpoch,false).Accepted);
            game.Combine(new[] {new ClueId(StudioRules.Sky.Value),new ClueId(AtelierRules.Painting.Value)});
            Assert.IsTrue(game.State.HasFact(AtelierFlowRules.SkyAsCanvas));Assert.IsFalse(game.State.HasFact(AtelierFlowRules.SkyCloudPainted));
            Assert.IsTrue(game.State.CanUseClue(new ClueId(StudioRules.Sky.Value)));
        }
        [Test]public void SkyStarsThenCloudEachTakeOneClickAndDoNotTouchTheOldPainting()
        {
            AtelierFlowTestPath.Light(game);Paint();Erase();game.Inspect(StudioRules.Sky);
            game.Combine(new[] {new ClueId(StudioRules.Sky.Value),new ClueId(AtelierRules.Painting.Value)});
            int revision=game.State.AtelierCanvas.Revision,count=game.State.AtelierCanvas.Current.Count,paint=game.State.AtelierCanvas.Current.RemainingPaint;
            // 云必须先有星；旧世代和跳步都不能写入天空事实。
            Assert.IsFalse(game.ClickAtelierSky("old-epoch",false).Accepted);Assert.IsFalse(game.ClickAtelierSky(game.SessionEpoch,false).Accepted);
            Assert.IsTrue(game.ClickAtelierSky(game.SessionEpoch,true).Accepted);Assert.IsFalse(game.State.HasFact(AtelierFlowRules.SkyFinished));
            Assert.IsFalse(game.ClickAtelierSky(game.SessionEpoch,true).Accepted);
            Assert.IsTrue(game.ClickAtelierSky(game.SessionEpoch,false).Accepted);Assert.IsTrue(game.State.HasFact(AtelierFlowRules.SkyFinished));
            Assert.IsFalse(game.ClickAtelierSky(game.SessionEpoch,false).Accepted);
            Assert.AreEqual(revision,game.State.AtelierCanvas.Revision);Assert.AreEqual(count,game.State.AtelierCanvas.Current.Count);Assert.AreEqual(paint,game.State.AtelierCanvas.Current.RemainingPaint);
        }
        [Test]public void SpreadIsVersionedAndUndoRechecksTheThreeStrokeGate()
        {
            AtelierFlowTestPath.Light(game);Paint();Assert.IsTrue(Tool(AtelierFinaleAction.SpreadCanvas).Accepted);
            Assert.IsTrue(game.UndoAtelierStroke(game.SessionEpoch,game.State.AtelierCanvas.Revision).Accepted);
            Assert.AreEqual(-1,game.State.AtelierCanvas.SpreadRevision);Assert.IsFalse(Tool(AtelierFinaleAction.SpreadCanvas).Accepted);
        }
        [Test]public void EmptyOutsideShortAndRepeatedRoofContactsAreRejected()
        {
            AtelierFlowTestPath.Light(game);Explain(StudioRules.Ceiling,"dry_paint");
            Assert.IsFalse(game.ApplyAtelierFinale(game.SessionEpoch,game.State.AtelierCanvas.Revision,AtelierFinaleAction.ProbeRoof,Array.Empty<AtelierContactSegment>()).Accepted);
            Assert.IsFalse(Tool(AtelierFinaleAction.ProbeRoof,-1,2000).Accepted);Assert.IsFalse(Tool(AtelierFinaleAction.ProbeRoof,5000,5100).Accepted);
            Assert.IsTrue(Tool(AtelierFinaleAction.ProbeRoof).Accepted);Assert.IsFalse(Tool(AtelierFinaleAction.ContinueRoof).Accepted);
        }
        [Test]public void PracticeRestoresTrueKnowledgeAndUsesANewEpoch()
        {
            var snapshot=game.CaptureAtelierPractice();var known=game.State.KnownClues.ToArray();string old=game.SessionEpoch;
            AtelierFlowTestPath.Light(game);Paint();Roof();game=GameSession.FromAtelierPractice(snapshot);
            CollectionAssert.AreEquivalent(known,game.State.KnownClues);Assert.AreNotEqual(old,game.SessionEpoch);
            Assert.AreEqual(0,game.State.AtelierCanvas.Current.Count);Assert.IsFalse(game.State.HasFact(AtelierFlowRules.RoofAsPaint));
            Assert.IsFalse(game.ClickAtelierSky(old,false).Accepted);
        }
    }
}
