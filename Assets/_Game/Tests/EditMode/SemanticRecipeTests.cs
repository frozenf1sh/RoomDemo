// =============================================================================
// 文件：SemanticRecipeTests.cs
// 职责：验证无序精确匹配、重复输入拒绝与三元配方能力。
// 边界：三元镜子配方仅为测试数据，不加入正式玩法。
// =============================================================================
using System;
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    /// <summary>验证无序精确匹配、重复输入拒绝与三元配方能力。</summary>
    public sealed class SemanticRecipeTests
    {
        /// <summary>创建测试用碎片身份，不引入 Unity。</summary>
        private static ClueId C(string value) => new ClueId(value);
        /// <summary>正反输入顺序产生同一事实与一次历史。</summary>
        [TestCase(false)] [TestCase(true)] public void PairIsUnorderedAndRecorded(bool reverse)
        {
            var s = new GameSession(DemoRules.Create()); s.Inspect(DemoRules.Plant);
            var pair = new[] { C("plant"), C("growth") }; if (reverse) Array.Reverse(pair);
            // 以下断言检查可观察结果；测试不依赖场景或规则实现的内部字段。
            Assert.That(s.Combine(pair).First(), Is.TypeOf<CombinationSucceeded>());
            Assert.That(s.State.HasFact(DemoRules.PlantGrown)); Assert.That(s.State.CompletedRecipes.Count, Is.EqualTo(1));
        }
        /// <summary>多余或重复输入不能匹配二元配方。</summary>
        [Test] public void ExtraOrDuplicateInputCannotMatchPair()
        {
            var s = new GameSession(DemoRules.Create()); s.Inspect(DemoRules.Plant); s.Inspect(DemoRules.Window);
            Assert.That(s.Combine(new[] { C("plant"), C("growth"), C("window") }).Single(), Is.TypeOf<CombinationFailed>());
            Assert.That(s.Combine(new[] { C("plant"), C("growth"), C("growth") }).Single(), Is.TypeOf<CombinationFailed>());
            Assert.That(s.State.Facts, Is.Empty); Assert.That(s.State.CompletedRecipes, Is.Empty); Assert.That(s.State.KnownClues.Count, Is.EqualTo(3));
        }
        /// <summary>测试用三元配方无需修改引擎。</summary>
        [Test] public void ThreeInputRecipeNeedsOnlyContentData()
        {
            var clues = new[] { new ClueDefinition(C("mirror"), "镜子", ClueCategory.Object), new ClueDefinition(C("reflection"), "反射", ClueCategory.Concept), new ClueDefinition(C("now"), "此刻", ClueCategory.Modifier) };
            var inputs = clues.Select(x => x.Id).ToArray(); var fact = new FactId("mirror.other_side");
            var rules = new RuleSet(clues, new[] { new DiscoveryRule("mirror", new ObjectId("mirror"), "镜子", "", Array.Empty<FactId>(), inputs) },
                new[] { new SemanticRecipe("mirror.reflection.now", inputs, Array.Empty<FactId>(), new[] { fact }) }, Array.Empty<ReactionRule>());
            var s = new GameSession(rules); s.Inspect(new ObjectId("mirror"));
            Assert.That(s.Combine(inputs.Take(2).ToArray()).Single(), Is.TypeOf<CombinationFailed>());
            s.Combine(inputs.Reverse().ToArray()); Assert.That(s.State.HasFact(fact));
        }
        /// <summary>相同输入的歧义配方在装载时被拒绝。</summary>
        [Test] public void SameInputsCannotSelectTwoRecipes()
        {
            var d = DemoRules.Create(); var first = d.Recipes[0];
            Assert.Throws<ArgumentException>(() => new RuleSet(d.Clues.ToArray(), d.Discoveries.ToArray(), new[] { first,
                new SemanticRecipe("ambiguous", first.Inputs.Reverse().ToArray(), first.Requires, first.Produces) }, d.Reactions.ToArray()));
        }
        /// <summary>重复配方失败，不重复产生事实或消耗碎片。</summary>
        [Test] public void RepeatedRecipeDoesNotEmitFactsOrConsumeClues()
        {
            var s = new GameSession(DemoRules.Create()); s.Inspect(DemoRules.Plant); var inputs = new[] { C("plant"), C("growth") };
            s.Combine(inputs); var known = s.State.KnownClues.ToArray(); var facts = s.State.Facts.ToArray();
            Assert.That(s.Combine(inputs).Single(), Is.TypeOf<CombinationFailed>());
            Assert.That(s.State.KnownClues, Is.EquivalentTo(known)); Assert.That(s.State.Facts, Is.EquivalentTo(facts));
        }
    }
}
