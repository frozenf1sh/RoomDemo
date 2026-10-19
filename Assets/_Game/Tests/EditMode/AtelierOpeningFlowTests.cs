// 文件：AtelierOpeningFlowTests.cs
// 职责：验证新开场的来源因果、同批观察、末笔预算、撤销、过期输入和主动阶段推进。
// 边界：不证明视觉方向、真人理解、接触听感或主场景输入已经验收。
using System;
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    /// <summary>只走正常调查、动作和精确组合；不调试授予首支笔刷。</summary>
    public sealed class AtelierOpeningFlowTests
    {
        private GameSession game;
        [SetUp] public void Setup() => game = new GameSession(AtelierFlowRules.CreateOpening());
        private void Read()
        { game.Inspect(AtelierRules.Manuscript);game.PerformAction(AtelierRules.Manuscript,"read_note"); }
        private void Ready()
        {
            game.Inspect(StudioRules.Cup);game.Inspect(AtelierRules.Brush);
            game.PerformAction(StudioRules.Cup,"wash_release_pigment");game.Inspect(AtelierFlowRules.Crayon);
            game.Combine(new[]{AtelierFlowRules.C("transparent"),AtelierFlowRules.C("blue")});
            game.Combine(new[]{AtelierFlowRules.C("flow"),AtelierFlowRules.C("free")});
            game.Combine(new[]{new ClueId("atelier.flow.ice_blue"),new ClueId("atelier.flow.swirl")});
            Assert.IsTrue(game.State.Knows(AtelierFlowRules.Brush(1)));
        }
        private AtelierCanvasResult Stroke(int cost,int? revision=null,string id=null)
        {
            var doc=game.State.AtelierCanvas;
            return game.CommitAtelierStroke(game.SessionEpoch,revision??doc.Revision,
                new AtelierStroke(id??Guid.NewGuid().ToString("N"),doc.Current.Id,5000,5000,0,cost));
        }
        [Test] public void OptionalNoteNeverGivesCupAndOtherSourcesStillRequireTheirOwnClick()
        {
            Assert.IsTrue(game.IsInspectable(StudioRules.Cup));
            Read();
            Assert.IsFalse(game.State.Knows(StudioRules.Cup));
            Assert.IsFalse(game.State.Knows(AtelierRules.Brush));
            Assert.IsFalse(game.State.Knows(AtelierFlowRules.Crayon));
        }
        [Test] public void FirstBrushCanBeFormedWithoutReadingTheOptionalNote()
        {
            Ready();
            Assert.IsFalse(game.State.HasFact(AtelierFlowRules.IntroRead));
            Assert.IsFalse(game.State.Knows(AtelierRules.Manuscript));
        }
        [Test] public void WashRequiresBothSourcesAndCollectsResultsInSameBatch()
        {
            Read();game.Inspect(StudioRules.Cup);
            game.PerformAction(StudioRules.Cup,"wash_release_pigment");Assert.IsFalse(game.State.HasFact(AtelierFlowRules.Washed));
            game.Inspect(AtelierRules.Brush);
            var events=game.PerformAction(StudioRules.Cup,"wash_release_pigment");
            Assert.IsTrue(game.State.Knows(AtelierFlowRules.C("blue")));
            Assert.IsTrue(game.State.Knows(AtelierFlowRules.C("flow")));
            Assert.IsTrue(events.OfType<ClueDiscovered>().Any(x=>x.Clue==AtelierFlowRules.C("flow")));
            Assert.IsFalse(game.State.Knows(AtelierFlowRules.Brush(1)));
            Assert.IsEmpty(game.PerformAction(StudioRules.Cup,"wash_release_pigment"));
        }
        [Test] public void ThreeRecipesDoNotLockOrAdvanceTheCanvas()
        { Ready();Assert.IsFalse(game.State.CanvasLocked);Assert.AreEqual(1,AtelierFlowRules.Stage(game.State));Assert.AreEqual(0,game.State.AtelierCanvas.Current.Count); }
        [Test] public void TwoStrokesCannotFinishAndThreeCanDiscardRemainder()
        {
            Ready();Assert.IsTrue(Stroke(17).Accepted);Assert.IsTrue(Stroke(17).Accepted);
            Assert.IsFalse(game.FinishAtelierLayer(game.SessionEpoch,game.State.AtelierCanvas.Revision).Accepted);
            Assert.IsTrue(Stroke(17).Accepted);
            Assert.IsTrue(game.FinishAtelierLayer(game.SessionEpoch,game.State.AtelierCanvas.Revision).Accepted);
            var one=game.State.AtelierCanvas.Layers[0];
            Assert.AreEqual(49,one.DiscardedUnits);Assert.AreEqual(100,one.CommittedUnits+one.DiscardedUnits);
            Assert.AreEqual(2,AtelierFlowRules.Stage(game.State));Assert.IsFalse(game.State.CanvasLocked);
            Assert.AreEqual(0,game.State.AtelierCanvas.Current.Count);
        }
        [TestCase(12)] [TestCase(24)] [TestCase(18)]
        public void BoundaryAndMiddleChoicesKeepLastStrokeLegal(int choice)
        {
            Ready();var layer=game.State.AtelierCanvas.Current;
            while(layer.Count<6)
            {
                int selected=Math.Max(layer.MinCost,Math.Min(layer.MaxCost,choice));
                Assert.That(selected,Is.InRange(12,24));Assert.IsTrue(Stroke(selected).Accepted);
            }
            Assert.AreEqual(0,layer.RemainingPaint);Assert.IsFalse(game.State.HasFact(AtelierFlowRules.Finished(1)));
            Assert.IsFalse(Stroke(12).Accepted);
        }
        [Test] public void UndoRefundsExactStrokeAndCannotUndoSealedLayer()
        {
            Ready();Stroke(24);Assert.IsTrue(game.UndoAtelierStroke(game.SessionEpoch,game.State.AtelierCanvas.Revision).Accepted);
            Assert.AreEqual(100,game.State.AtelierCanvas.Current.RemainingPaint);
            Stroke(17);Stroke(17);Stroke(17);game.FinishAtelierLayer(game.SessionEpoch,game.State.AtelierCanvas.Revision);
            Assert.IsFalse(game.UndoAtelierStroke(game.SessionEpoch,game.State.AtelierCanvas.Revision).Accepted);
            Assert.AreEqual(3,game.State.AtelierCanvas.Layers[0].Count);
        }
        [Test] public void OldEpochStaleRevisionAndDuplicateMutationAreRejectedWithoutCharges()
        {
            Ready();int revision=game.State.AtelierCanvas.Revision;Assert.IsTrue(Stroke(17,revision,"first").Accepted);
            Assert.IsTrue(Stroke(17,revision,"first").AlreadyApplied);
            Assert.IsFalse(Stroke(18,revision,"first").Accepted);
            Assert.IsFalse(Stroke(17,revision).Accepted);
            var request=new AtelierStroke("old",1,5000,5000,0,17);
            Assert.IsFalse(game.CommitAtelierStroke("old-epoch",game.State.AtelierCanvas.Revision,request).Accepted);
            Assert.AreEqual(1,game.State.AtelierCanvas.Current.Count);
        }
        [Test] public void NoBrushAndOutOfBoundsInputCannotMutateArtwork()
        {
            Assert.IsFalse(Stroke(17).Accepted);Ready();
            Assert.IsFalse(game.CommitAtelierStroke(game.SessionEpoch,0,new AtelierStroke("outside",1,-1,5000,0,17)).Accepted);
            Assert.AreEqual(0,game.State.AtelierCanvas.Revision);
        }
        private void StageTwo()
        {
            Ready();Stroke(17);Stroke(17);Stroke(17);
            game.FinishAtelierLayer(game.SessionEpoch,game.State.AtelierCanvas.Revision);
        }
        [Test] public void StageTwoSourcesCannotBeInspectedBeforeFirstLayerFinishes()
        {
            foreach (var source in new[] { AtelierFlowRules.Plaster, AtelierFlowRules.HandMirror, AtelierFlowRules.DryBrush, AtelierFlowRules.MirrorShards })
            {
                Assert.IsFalse(game.IsInspectable(source));
                game.Inspect(source);
                Assert.IsFalse(game.State.Knows(source));
            }
        }
        [Test] public void MirrorObservationRequiresOwnSourceAndNeverAwardsPlaster()
        {
            StageTwo();
            game.PerformAction(AtelierFlowRules.HandMirror,"mirror_compare");
            Assert.IsFalse(game.State.Knows(AtelierFlowRules.C("flip")));
            game.Inspect(AtelierFlowRules.HandMirror);
            Assert.IsFalse(game.State.Knows(AtelierFlowRules.C("flip")));
            game.PerformAction(AtelierFlowRules.HandMirror,"mirror_compare");
            Assert.IsTrue(game.State.Knows(AtelierFlowRules.C("flip")));
            Assert.IsFalse(game.State.Knows(AtelierFlowRules.Plaster));
            Assert.IsEmpty(game.PerformAction(AtelierFlowRules.HandMirror,"mirror_compare"));
        }
        [Test] public void SecondBrushRequiresFourPersonalSourcesAndPreservesFirstLayer()
        {
            StageTwo();
            var first = game.State.AtelierCanvas.Layers[0].Strokes.ToArray();
            game.Inspect(AtelierFlowRules.Plaster);game.Inspect(AtelierFlowRules.HandMirror);
            game.PerformAction(AtelierFlowRules.HandMirror,"mirror_compare");
            game.Inspect(AtelierFlowRules.DryBrush);
            game.Combine(new[]{AtelierFlowRules.C("black"),AtelierFlowRules.C("sharp")});
            Assert.IsFalse(game.State.Knows(new ClueId("atelier.flow.rough")));
            game.Inspect(AtelierFlowRules.MirrorShards);
            game.Combine(new[]{AtelierFlowRules.C("fixed"),AtelierFlowRules.C("flip")});
            game.Combine(new[]{AtelierFlowRules.C("black"),AtelierFlowRules.C("sharp")});
            game.Combine(new[]{new ClueId("atelier.flow.twisted"),new ClueId("atelier.flow.rough")});
            Assert.IsTrue(game.State.Knows(AtelierFlowRules.Brush(2)));
            for (int index = 0; index < 5; index++) Assert.IsTrue(Stroke(20).Accepted);
            Assert.AreEqual(0,game.State.AtelierCanvas.Current.RemainingPaint);
            Assert.IsFalse(game.State.HasFact(AtelierFlowRules.Finished(2)));
            Assert.IsTrue(game.FinishAtelierLayer(game.SessionEpoch,game.State.AtelierCanvas.Revision).Accepted);
            Assert.AreEqual(3,AtelierFlowRules.Stage(game.State));
            CollectionAssert.AreEqual(first,game.State.AtelierCanvas.Layers[0].Strokes);
            Assert.IsFalse(game.State.Knows(AtelierFlowRules.Brush(3)));
            Assert.IsFalse(game.State.CanvasLocked);
        }
        [Test] public void ActionsCannotAwardAnObjectAsAnObservation()
        {
            var id=new ObjectId("test.source");var clue=new ClueId(id.Value);
            Assert.Throws<ArgumentException>(()=>new RuleSet(new[]{new ClueDefinition(clue,"来源",ClueCategory.Object)},
                new[]{new DiscoveryRule("source",id,"来源","",Array.Empty<FactId>(),new[]{clue})},Array.Empty<SemanticRecipe>(),Array.Empty<ReactionRule>(),
                sceneActions:new[]{new SceneActionRule("bad",id,Array.Empty<FactId>(),new[]{AtelierFlowRules.Washed},"","",observations:new[]{clue})}));
        }
    }
}
