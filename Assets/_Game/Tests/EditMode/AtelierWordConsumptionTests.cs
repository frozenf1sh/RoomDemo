// 文件：AtelierWordConsumptionTests.cs
// 职责：验证四阶段双词容量、成功消费灰词、失败无损与发现历史保留。
// 边界：只检查领域及应用命令，不代表真人理解度、界面观感或鼠标试玩。
using System;
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>消费只影响可用材料，不删除来源知识，也不由UI自行记账。</summary>
    public sealed class AtelierWordConsumptionTests
    {
        private GameSession game;
        private static ClueId C(string id) => AtelierFlowRules.C(id);
        [SetUp] public void Start()
        {
            game = new GameSession(AtelierFlowRules.CreateOpening());
            game.Inspect(StudioRules.Cup);
            game.Inspect(AtelierRules.Brush);
            game.PerformAction(StudioRules.Cup,"wash_release_pigment");
            game.Inspect(AtelierFlowRules.Crayon);
        }

        [Test] public void SuccessConsumesOnlyDeclaredGrayInputsAndKeepsHistory()
        {
            var events = game.Combine(new[] { C("transparent"),C("blue") });
            var success = events.OfType<CombinationSucceeded>().Single();
            CollectionAssert.AreEquivalent(new[] { C("transparent"),C("blue") },success.ConsumedClues);
            Assert.IsTrue(game.State.Knows(C("transparent")));
            Assert.IsFalse(game.State.CanUseClue(C("transparent")));
            Assert.IsFalse(game.State.CanUseClue(C("blue")));
            Assert.IsTrue(game.State.CanUseClue(C("free")));
            Assert.IsTrue(game.State.CanUseClue(C("flow")));
            Assert.IsTrue(game.State.CanUseClue(new ClueId(StudioRules.Cup.Value)));
            Assert.IsTrue(game.State.CanUseClue(new ClueId("atelier.flow.ice_blue")));
            Assert.IsTrue(game.State.HasCompleted("atelier.flow.recipe.ice_blue"));
        }

        [Test] public void ThirdInputAndWrongPairCannotConsumeAnything()
        {
            Assert.AreEqual(2,game.Rules.MaximumCombinationInputs);
            Assert.IsTrue(game.Combine(new[] { C("transparent"),C("blue"),C("free") }).OfType<CombinationFailed>().Any());
            Assert.IsTrue(game.Combine(new[] { C("transparent"),C("free") }).OfType<CombinationFailed>().Any());
            Assert.IsEmpty(game.State.ConsumedClues);
            Assert.IsTrue(game.State.CanUseClue(C("transparent")));
            Assert.IsEmpty(game.State.CompletedRecipes);
            Assert.IsFalse(game.State.Knows(new ClueId("atelier.flow.ice_blue")));
        }

        [Test] public void ReinspectionAndRepeatedSubmissionCannotRestoreConsumedWords()
        {
            game.Combine(new[] { C("transparent"),C("blue") });
            game.Inspect(StudioRules.Cup);
            game.PerformAction(StudioRules.Cup,"wash_release_pigment");
            Assert.IsFalse(game.State.CanUseClue(C("transparent")));
            Assert.IsFalse(game.State.CanUseClue(C("blue")));
            Assert.IsNull(game.PreviewCombination(new[] { C("transparent"),C("blue") }));
            Assert.IsTrue(game.Combine(new[] { C("transparent"),C("blue") }).OfType<CombinationFailed>().Any());
            Assert.AreEqual(1,game.State.CompletedRecipes.Count);
            Assert.AreEqual(2,game.State.ConsumedClues.Count);
        }

        [Test] public void SuccessSubscribersObserveConsumptionAndOutputTogether()
        {
            bool received = false;
            game.EventRaised += value => {
                if (!(value is CombinationSucceeded)) return;
                received = true;
                Assert.IsFalse(game.State.CanUseClue(C("transparent")));
                Assert.IsFalse(game.State.CanUseClue(C("blue")));
                Assert.IsTrue(game.State.CanUseClue(new ClueId("atelier.flow.ice_blue")));
            };
            game.Combine(new[] { C("transparent"),C("blue") });
            Assert.IsTrue(received);
        }

        [Test] public void UsedBlueConceptsRetireButBrushAndKnowledgeRemain()
        {
            var ice = new ClueId("atelier.flow.ice_blue");
            var swirl = new ClueId("atelier.flow.swirl");
            game.Combine(new[] { C("transparent"),C("blue") });
            game.Combine(new[] { C("flow"),C("free") });
            game.Combine(new[] { ice,swirl });
            Assert.IsFalse(game.State.CanUseClue(ice));
            Assert.IsTrue(game.State.Knows(ice));
            Assert.IsFalse(game.State.CanUseClue(swirl));
            Assert.IsTrue(game.State.CanUseClue(AtelierFlowRules.Brush(1)));
            Assert.AreEqual(6,game.State.ConsumedClues.Count);
        }

        [Test] public void ConsumedDefinitionsAreCopiedAndCannotReferToAnotherInput()
        {
            var inputs = new[] { C("transparent"),C("blue") };
            var consumed = (ClueId[])inputs.Clone();
            var rule = new SemanticRecipe("test.consume",inputs,Array.Empty<FactId>(),new[] { new ClueId("test.result") },
                Array.Empty<FactId>(),consumedInputs:consumed);
            consumed[0] = C("flow");
            Assert.AreEqual(C("transparent"),rule.ConsumedInputs[0]);
            Assert.Throws<ArgumentException>(() => new SemanticRecipe("test.invalid",inputs,Array.Empty<FactId>(),
                Array.Empty<FactId>(),consumedInputs:new[] { C("flow") }));
        }

        [Test] public void NewSessionDoesNotInheritThePreviousConsumption()
        {
            game.Combine(new[] { C("transparent"),C("blue") });
            var fresh = new GameSession(AtelierFlowRules.CreateOpening());
            fresh.Inspect(StudioRules.Cup);
            Assert.IsFalse(fresh.State.Knows(C("transparent")));
            fresh.Inspect(AtelierRules.Brush);
            fresh.PerformAction(StudioRules.Cup,"wash_release_pigment");
            Assert.IsTrue(fresh.State.CanUseClue(C("transparent")));
            Assert.IsEmpty(fresh.State.ConsumedClues);
        }

        [Test] public void EveryGrayWordHasRecipeOrRoofContactPurposeAndPhysicalSourcesDoNotAwardItOnClick()
        {
            var gray = game.Rules.Clues.Where(x=>x.Category==ClueCategory.Observation).Select(x=>x.Id).ToArray();
            Assert.AreEqual(14,gray.Length);
            Assert.IsTrue(gray.Where(id=>id!=C("roof_night")).All(id=>game.Rules.Recipes.Any(recipe=>recipe.Inputs.Contains(id))));
            // 屋孔的当场观察用于第二次继续揭底，接受后消费，不是新增穷举配方。
            foreach (var source in new[] { StudioRules.Cup,AtelierFlowRules.HandMirror })
                Assert.IsFalse(game.Rules.Discoveries.Single(x=>x.Source==source).Grants.Any(gray.Contains));
        }
    }
}
