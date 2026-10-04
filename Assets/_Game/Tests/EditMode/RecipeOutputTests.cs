// =============================================================================
// 文件：RecipeOutputTests.cs
// 职责：验证统一配方能够产生线索、事实或两者，不依赖具体谜题分支。
// 边界：只测试纯 C# 会话；不以领域通过代替检视画面和真人理解度验收。
// =============================================================================
using System;
using System.Linq;
using Game.Application;
using NUnit.Framework;

namespace Game.Domain.Tests
{
    /// <summary>第四阶段的配方输出契约与旧事实配方兼容验证。</summary>
    public sealed class RecipeOutputTests
    {
        private static readonly ClueId Sprout = new ClueId("sprout"), Light = new ClueId("toward_light"), Growth = new ClueId("growth");
        private static readonly FactId Inspired = new FactId("test.inspired");

        /// <summary>同一配方数据可以只输出线索，或同时输出世界事实。</summary>
        private static RuleSet Rules(bool mixed = false) => new RuleSet(
            new[] { new ClueDefinition(Sprout, "新芽", ClueCategory.Observation), new ClueDefinition(Light, "向光", ClueCategory.Observation), new ClueDefinition(Growth, "生长", ClueCategory.Concept) },
            Array.Empty<DiscoveryRule>(),
            new[] { new SemanticRecipe("recipe.insight.growth", new[] { Sprout, Light }, Array.Empty<FactId>(), new[] { Growth }, mixed ? new[] { Inspired } : Array.Empty<FactId>()) },
            Array.Empty<ReactionRule>());

        [Test]
        public void ClueOnlyRecipeFormsConceptWithoutWorldFact()
        {
            // 使用预设只提供输入，确保目标概念确实由组合产生。
            var session = GameSession.FromPreset(Rules(), new DebugPreset("input", new[] { Sprout, Light }, Array.Empty<FactId>(), Array.Empty<string>()));
            var events = session.Combine(new[] { Light, Sprout });
            Assert.That(session.State.Knows(Growth), Is.True);
            Assert.That(events.OfType<ClueDiscovered>().Single().Clue, Is.EqualTo(Growth));
            Assert.That(events.OfType<CombinationSucceeded>().Count(), Is.EqualTo(1));
            Assert.That(session.State.Facts, Is.Empty);
        }

        [Test]
        public void MixedOutputAddsClueAndFactOnce()
        {
            var session = GameSession.FromPreset(Rules(true), new DebugPreset("input", new[] { Sprout, Light }, Array.Empty<FactId>(), Array.Empty<string>()));
            var events = session.Combine(new[] { Sprout, Light });
            Assert.That(session.State.Knows(Growth) && session.State.HasFact(Inspired), Is.True);
            Assert.That(events.OfType<ClueDiscovered>().Count(), Is.EqualTo(1));
            Assert.That(events.OfType<FactAdded>().Count(), Is.EqualTo(1));
            Assert.That(session.Combine(new[] { Light, Sprout }).Single(), Is.TypeOf<CombinationFailed>());
        }

        [Test]
        public void UndefinedOutputClueIsRejectedBeforePlaying()
        {
            Assert.Throws<ArgumentException>(() => new RuleSet(
                new[] { new ClueDefinition(Sprout, "新芽", ClueCategory.Observation), new ClueDefinition(Light, "向光", ClueCategory.Observation) }, Array.Empty<DiscoveryRule>(),
                new[] { new SemanticRecipe("bad", new[] { Sprout, Light }, Array.Empty<FactId>(), new[] { Growth }, Array.Empty<FactId>()) }, Array.Empty<ReactionRule>()));
        }

        [Test]
        public void RestoredHistoryMustIncludeItsProducedClue()
        {
            // 概念形成的历史也必须与已知线索一致，不能只校验事实输出。
            Assert.Throws<ArgumentException>(() => GameSession.FromPreset(Rules(), new DebugPreset("bad", new[] { Sprout, Light }, Array.Empty<FactId>(), new[] { "recipe.insight.growth" })));
            Assert.That(GameSession.FromPreset(Rules(), new DebugPreset("good", new[] { Sprout, Light, Growth }, Array.Empty<FactId>(), new[] { "recipe.insight.growth" })).State.CompletedRecipes.Count, Is.EqualTo(1));
        }

        [Test]
        public void OutputCollectionsAreDefensiveCopiesAndOldConstructorStillProducesFacts()
        {
            var output = new[] { Growth };
            var recipe = new SemanticRecipe("copy", new[] { Sprout, Light }, Array.Empty<FactId>(), output, new[] { Inspired });
            output[0] = Sprout;
            Assert.That(recipe.ProducesClues.Single(), Is.EqualTo(Growth));
            var legacy = new SemanticRecipe("legacy", new[] { Sprout, Light }, Array.Empty<FactId>(), new[] { Inspired });
            Assert.That(legacy.ProducesClues, Is.Empty);
            Assert.That(legacy.ProducesFacts.Single(), Is.EqualTo(Inspired));
        }
        /// <summary>输出概念合法，但不能用新配方代替玩家认识一个场景物体。</summary>
        [Test] public void RecipeCannotGrantAnObjectClue()
        {
            var painting = new ClueId("painting");
            Assert.Throws<ArgumentException>(() => new RuleSet(new[] {
                new ClueDefinition(Sprout, "新芽", ClueCategory.Observation), new ClueDefinition(Light, "向光", ClueCategory.Observation), new ClueDefinition(painting, "画", ClueCategory.Object) },
                Array.Empty<DiscoveryRule>(), new[] { new SemanticRecipe("bad.object", new[] { Sprout, Light }, Array.Empty<FactId>(), new[] { painting }, Array.Empty<FactId>()) }, Array.Empty<ReactionRule>()));
        }
        /// <summary>发现表也拒绝跨物体授予，保护后续策划数据不重现旧问题。</summary>
        [Test] public void DiscoveryCannotNameAnotherObject()
        {
            var window = new ClueId("window");
            Assert.Throws<ArgumentException>(() => new RuleSet(new[] { new ClueDefinition(window, "窗", ClueCategory.Object) },
                new[] { new DiscoveryRule("bad.source", new ObjectId("book"), "书", "书", Array.Empty<FactId>(), new[] { window }) },
                Array.Empty<SemanticRecipe>(), Array.Empty<ReactionRule>()));
        }
    }
}
