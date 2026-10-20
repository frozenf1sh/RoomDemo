// 文件：AtelierStageThreeTests.cs
// 职责：验证第三阶段的来源、近远比较、灰词消费、工具经验与主动收层。
// 边界：只验证领域命令因果，不把自动命令当作真人鼠标或视觉验收。
using System;
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    /// <summary>每项通过真实前三阶段配方推进，不用调试事实制造可完成状态。</summary>
    public sealed class AtelierStageThreeTests
    {
        private GameSession game;
        private static ClueId C(string id)=>AtelierFlowRules.C(id);
        [SetUp] public void Setup() => game=new GameSession(AtelierFlowRules.CreateOpening());
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
        [Test] public void ThirdSourcesAndActionsStayClosedUntilSecondLayerFinishes()
        {
            foreach(var source in new[] {StudioRules.Window,AtelierFlowRules.DryPalette,AtelierFlowRules.PaletteKnife})
            { Assert.IsFalse(game.IsInspectable(source));game.Inspect(source);Assert.IsFalse(game.State.Knows(source)); }
            game.PerformAction(StudioRules.Window,"window_near");Assert.IsFalse(game.State.HasFact(AtelierFlowRules.WindowNearSeen));
            Assert.IsFalse(game.State.Knows(C("reality")));
        }
        [Test] public void WindowClickDoesNotAwardNearFarOrComparison()
        {
            ThirdStage();game.Inspect(StudioRules.Window);
            foreach(var word in new[] {"reality","night","stars"}) Assert.IsFalse(game.State.Knows(C(word)));
            game.PerformAction(StudioRules.Window,"window_compare");Assert.IsFalse(game.State.HasFact(AtelierFlowRules.WindowCompared));
            game.PerformAction(StudioRules.Window,"window_far");Assert.IsTrue(game.State.Knows(C("night")));
            game.PerformAction(StudioRules.Window,"window_compare");Assert.IsFalse(game.State.Knows(C("stars")));
            game.PerformAction(StudioRules.Window,"window_near");game.PerformAction(StudioRules.Window,"window_compare");
            Assert.IsTrue(game.State.Knows(C("reality")));Assert.IsTrue(game.State.Knows(C("stars")));
            Assert.IsFalse(game.State.Knows(AtelierFlowRules.DryPalette));Assert.IsFalse(game.State.Knows(StudioRules.Ceiling));
            Assert.IsEmpty(game.PerformAction(StudioRules.Window,"window_compare"));
        }
        [Test] public void StudyingCrustOnlyAwardsOneUsefulGrayWordAndNeverKnife()
        {
            ThirdStage();game.Inspect(AtelierFlowRules.DryPalette);
            Assert.IsFalse(game.State.Knows(C("chaos")));Assert.IsFalse(game.State.Knows(AtelierFlowRules.PaletteKnife));
            game.PerformAction(AtelierFlowRules.DryPalette,"palette_inspect");
            Assert.IsTrue(game.State.Knows(C("chaos")));Assert.IsFalse(game.State.HasFact(AtelierFlowRules.DryLearned));
            Assert.IsFalse(game.State.HasFact(AtelierFlowRules.WetLearned));
        }
        [Test] public void BothKnifeActionsRequirePersonallyKnownBoardToolAndStudy()
        {
            ThirdStage();game.Inspect(AtelierFlowRules.DryPalette);
            game.PerformAction(AtelierFlowRules.DryPalette,"palette_scrape");Assert.IsFalse(game.State.HasFact(AtelierFlowRules.DryLearned));
            game.Inspect(AtelierFlowRules.PaletteKnife);
            game.PerformAction(AtelierFlowRules.DryPalette,"palette_spread");Assert.IsFalse(game.State.HasFact(AtelierFlowRules.WetLearned));
            game.PerformAction(AtelierFlowRules.DryPalette,"palette_inspect");
            game.PerformAction(AtelierFlowRules.DryPalette,"palette_scrape");game.PerformAction(AtelierFlowRules.DryPalette,"palette_spread");
            Assert.IsTrue(game.State.HasFact(AtelierFlowRules.DryLearned));Assert.IsTrue(game.State.HasFact(AtelierFlowRules.WetLearned));
            Assert.IsEmpty(game.PerformAction(AtelierFlowRules.DryPalette,"palette_scrape"));
            Assert.IsFalse(game.State.Knows(StudioRules.Room));Assert.IsFalse(game.State.Knows(StudioRules.Ceiling));
        }
        [Test] public void NightCityConsumesFourGrayWordsWhileConceptsAndSourceHistoryRemain()
        {
            ThirdStage();ReadyBrush();
            Assert.IsTrue(game.State.Knows(AtelierFlowRules.Brush(3)));
            foreach(var word in new[] {"chaos","night","reality","stars"})
            { Assert.IsTrue(game.State.Knows(C(word)));Assert.IsFalse(game.State.CanUseClue(C(word))); }
            Assert.IsTrue(game.State.CanUseClue(new ClueId("atelier.flow.body")));
            Assert.IsTrue(game.State.CanUseClue(new ClueId("atelier.flow.present")));
            game.PerformAction(StudioRules.Window,"window_near");Assert.IsFalse(game.State.CanUseClue(C("reality")));
        }
        [Test] public void ThirdLayerCanBePaintedBeforeKnifeButCannotFinishUntilBothContacts()
        {
            ThirdStage();ReadyBrush();PaintThree();var doc=game.State.AtelierCanvas;int version=doc.Revision;
            Assert.IsFalse(game.FinishAtelierLayer(game.SessionEpoch,version).Accepted);
            Assert.IsNotNull(AtelierCanvasOperations.FinishBlockedReason(game.State,game.Rules,version));
            Assert.AreEqual(version,doc.Revision);Assert.AreEqual(40,doc.Current.RemainingPaint);
            game.Inspect(AtelierFlowRules.PaletteKnife);game.PerformAction(AtelierFlowRules.DryPalette,"palette_spread");
            Assert.IsFalse(game.FinishAtelierLayer(game.SessionEpoch,version).Accepted);
            game.PerformAction(AtelierFlowRules.DryPalette,"palette_scrape");
            Assert.IsNull(AtelierCanvasOperations.FinishBlockedReason(game.State,game.Rules,version));Finish();
            Assert.IsTrue(game.State.HasFact(AtelierFlowRules.Finished(3)));Assert.AreEqual(4,AtelierFlowRules.Stage(game.State));
            Assert.AreEqual(3,doc.Layers[0].Count);Assert.AreEqual(3,doc.Layers[1].Count);Assert.AreEqual(3,doc.Layers[2].Count);
            Assert.AreEqual(100,doc.Layers[2].CommittedUnits+doc.Layers[2].DiscardedUnits);
            Assert.IsFalse(game.State.CanvasLocked);Assert.IsFalse(game.State.Knows(AtelierFlowRules.Brush(4)));
        }
    }
}
