// 文件：AtelierStageThreeTests.cs
// 职责：验证两处窗灯的亲点、灯火与夜城配方、干湿观察和材料用途。
// 边界：只验证领域因果，不代表视觉质量或真人鼠标试玩。
using System;
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    /// <summary>通过正常命令建立阶段前置，供本轮相关规则测试复用。</summary>
    internal static class AtelierFlowTestPath
    {
        public static void Pair(GameSession g,string a,string b)=>g.Combine(new[] {AtelierFlowRules.C(a),AtelierFlowRules.C(b)});
        public static void Form(GameSession g,string a,string b)=>g.Combine(new[] {new ClueId("atelier.flow."+a),new ClueId("atelier.flow."+b)});
        public static void Paint(GameSession g)
        {
            g.Inspect(AtelierRules.Painting);
            for(int i=0;i<3;i++)
                Assert.IsTrue(g.CommitAtelierStroke(g.SessionEpoch,g.State.AtelierCanvas.Revision,
                    new AtelierStroke(Guid.NewGuid().ToString(),g.State.AtelierCanvas.Current.Id,5000,5000,0,20)).Accepted);
        }
        public static void Finish(GameSession g)=>Assert.IsTrue(g.FinishAtelierLayer(g.SessionEpoch,g.State.AtelierCanvas.Revision).Accepted);
        public static void Third(GameSession g)
        {
            g.Inspect(StudioRules.Cup);g.Inspect(AtelierRules.Brush);g.PerformAction(StudioRules.Cup,"wash_release_pigment");
            g.Inspect(AtelierFlowRules.Crayon);Pair(g,"transparent","blue");Pair(g,"flow","free");Form(g,"ice_blue","swirl");Paint(g);Finish(g);
            foreach(var source in new[] {AtelierFlowRules.Plaster,AtelierFlowRules.HandMirror,AtelierFlowRules.DryBrush,AtelierFlowRules.MirrorShards})g.Inspect(source);
            g.PerformAction(AtelierFlowRules.HandMirror,"mirror_compare");Pair(g,"fixed","flip");Pair(g,"black","sharp");Form(g,"twisted","rough");Paint(g);Finish(g);
        }
        public static void City(GameSession g,bool farFirst=false)
        {
            g.Inspect(StudioRules.Window);
            g.Inspect(farFirst?AtelierFlowRules.FarWindowLight:AtelierFlowRules.NearWindowLight);
            g.Inspect(farFirst?AtelierFlowRules.NearWindowLight:AtelierFlowRules.FarWindowLight);
            g.Combine(new[] {new ClueId(AtelierFlowRules.NearWindowLight.Value),new ClueId(AtelierFlowRules.FarWindowLight.Value)});
            g.Combine(new[] {AtelierFlowRules.C("window_lights"),new ClueId(StudioRules.Window.Value)});
        }
        public static void Knife(GameSession g)
        {
            g.Inspect(AtelierFlowRules.DryPalette);g.Inspect(AtelierFlowRules.PaletteKnife);
            g.PerformAction(AtelierFlowRules.DryPalette,"palette_scrape");g.PerformAction(AtelierFlowRules.DryPalette,"palette_spread");
        }
        public static void Fourth(GameSession g){Third(g);City(g);Knife(g);Paint(g);Finish(g);}
        public static void Light(GameSession g)
        {
            g.Inspect(StudioRules.LampBody);g.Inspect(AtelierRules.Matches);
            g.PerformAction(AtelierRules.Matches,"strike_match");g.PerformAction(StudioRules.LampBody,"light_wick");
        }
    }
    /// <summary>新内容不再依赖旧的比较按钮、肉身与当下。</summary>
    public sealed class AtelierStageThreeTests
    {
        private GameSession game;
        [SetUp]public void Setup()=>game=new GameSession(AtelierFlowRules.CreateOpening());
        [Test]public void SourcesRemainClosedBeforeSecondLayer()
        {
            foreach(var id in new[] {StudioRules.Window,AtelierFlowRules.NearWindowLight,AtelierFlowRules.FarWindowLight,AtelierFlowRules.DryPalette})
            {game.Inspect(id);Assert.IsFalse(game.State.Knows(id));}
        }
        [TestCase(false)][TestCase(true)]public void BothPersonalLightsFormCityInEitherObservationOrder(bool farFirst)
        {
            AtelierFlowTestPath.Third(game);AtelierFlowTestPath.City(game,farFirst);
            Assert.IsTrue(game.State.Knows(AtelierFlowRules.Brush(3)));
            Assert.IsFalse(game.State.CanUseClue(AtelierFlowRules.C("window_lights")));
            Assert.IsTrue(game.State.Knows(AtelierFlowRules.NearWindowLight));
            Assert.IsTrue(game.State.Knows(AtelierFlowRules.FarWindowLight));
            Assert.IsFalse(game.Rules.Clues.Any(x=>x.DisplayName=="肉身"||x.DisplayName=="当下"));
        }
        [Test]public void WindowAndOneLightDoNotAwardTheOtherSource()
        {
            AtelierFlowTestPath.Third(game);game.Inspect(StudioRules.Window);game.Inspect(AtelierFlowRules.NearWindowLight);
            Assert.IsFalse(game.State.Knows(AtelierFlowRules.FarWindowLight));
            game.Combine(new[] {new ClueId(AtelierFlowRules.NearWindowLight.Value),new ClueId(AtelierFlowRules.FarWindowLight.Value)});
            Assert.IsFalse(game.State.Knows(AtelierFlowRules.C("window_lights")));
        }
        [Test]public void ContactAwardsDryThenWetWithoutAStudyButton()
        {
            AtelierFlowTestPath.Third(game);game.Inspect(AtelierFlowRules.DryPalette);game.Inspect(AtelierFlowRules.PaletteKnife);
            Assert.IsFalse(game.State.Knows(AtelierFlowRules.C("dry_paint")));
            game.PerformAction(AtelierFlowRules.DryPalette,"palette_spread");Assert.IsFalse(game.State.HasFact(AtelierFlowRules.WetLearned));
            game.PerformAction(AtelierFlowRules.DryPalette,"palette_scrape");game.PerformAction(AtelierFlowRules.DryPalette,"palette_spread");
            Assert.IsTrue(game.State.Knows(AtelierFlowRules.C("dry_paint")));Assert.IsTrue(game.State.Knows(AtelierFlowRules.C("wet_paint")));
            Assert.IsFalse(game.State.Knows(StudioRules.Room));Assert.IsFalse(game.State.Knows(StudioRules.Ceiling));
        }
        [Test]public void FinishChecksBothExperiencesAndThreeStrokes()
        {
            AtelierFlowTestPath.Third(game);AtelierFlowTestPath.City(game);AtelierFlowTestPath.Paint(game);
            Assert.IsFalse(game.FinishAtelierLayer(game.SessionEpoch,game.State.AtelierCanvas.Revision).Accepted);
            AtelierFlowTestPath.Knife(game);AtelierFlowTestPath.Finish(game);
            Assert.IsTrue(game.State.HasFact(AtelierFlowRules.Finished(3)));
        }
        [Test]public void MainMaterialProjectionHidesUnusedSourcesButRetainsFutureCanvas()
        {
            AtelierFlowTestPath.Third(game);
            Assert.IsFalse(game.AtelierNotebookMaterials.Contains(new ClueId(StudioRules.Cup.Value)));
            Assert.IsTrue(game.AtelierNotebookMaterials.Contains(new ClueId(AtelierRules.Painting.Value)));
            Assert.IsTrue(game.AtelierNotebookMaterials.Contains(AtelierFlowRules.Brush(1)));
            AtelierFlowTestPath.City(game);
            Assert.IsFalse(game.AtelierNotebookMaterials.Contains(new ClueId(AtelierFlowRules.NearWindowLight.Value)));
            Assert.IsTrue(game.State.Knows(AtelierFlowRules.NearWindowLight));
        }
    }
}
