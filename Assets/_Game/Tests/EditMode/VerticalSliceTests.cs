// =============================================================================
// 文件：VerticalSliceTests.cs
// 职责：验证海画链、旧解释交叉、规则顺序无关与循环终止。
// 边界：断言最终闭包及玩家命令数量，不依赖表现动画。
// =============================================================================
using System;
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    /// <summary>验证海画链、旧解释交叉、规则顺序无关与循环终止。</summary>
    public sealed class VerticalSliceTests
    {
        /// <summary>通过新 Combine API 提交测试中的两个碎片。</summary>
        private static void Pair(GameSession s, string a, string b) => s.Combine(new[] { DemoRules.Clue(a), DemoRules.Clue(b) });
        /// <summary>执行植物、书、窗户路径，到达海之前的正式状态。</summary>
        private static void BeforeSea(GameSession s)
        {
            s.Inspect(DemoRules.Plant); Pair(s, "plant", "growth"); s.Inspect(DemoRules.Book); Pair(s, "far", "window");
        }
        /// <summary>只需三次玩家组合即可自动达到完整涌现。</summary>
        [Test] public void CompletePlaythroughNeedsOnlyThreePlayerRecipes()
        {
            var s = new GameSession(DemoRules.Create()); BeforeSea(s);
            // 以下断言检查可观察结果；测试不依赖场景或规则实现的内部字段。
            Assert.That(s.State.HasFact(DemoRules.PaperRevealed)); Assert.That(s.State.HasFact(DemoRules.WaterPresent), Is.False);
            s.Inspect(DemoRules.Paper); Assert.That(s.State.Knows(DemoRules.Sea)); Assert.That(s.State.Knows(DemoRules.Painting));
            var events = s.Combine(new[] { DemoRules.Clue("painting"), DemoRules.Clue("sea") });
            Assert.That(events.OfType<CombinationSucceeded>().Single().RecipeId, Is.EqualTo("recipe.painting.sea"));
            Assert.That(events.OfType<ReactionTriggered>().Select(x => x.Id), Is.EquivalentTo(new[] { "painting-leaks-water", "plant-becomes-aquatic", "window-becomes-ocean", "room-remembers" }));
            Assert.That(s.State.Facts.Count, Is.EqualTo(11)); Assert.That(s.State.HasFact(DemoRules.Complete));
            Assert.That(s.State.CompletedRecipes.Count, Is.EqualTo(3)); Assert.That(s.State.FiredReactions.Count, Is.EqualTo(8));
        }
        /// <summary>提前知道画后观察纸张只新增海。</summary>
        [Test] public void PreviouslyKnownPaintingIsNotRediscoveredByPaper()
        {
            var s = new GameSession(DemoRules.Create()); s.Inspect(DemoRules.Painting); BeforeSea(s);
            Assert.That(s.Inspect(DemoRules.Paper).OfType<ClueDiscovered>().Select(x => x.Clue), Is.EqualTo(new[] { DemoRules.Clue("sea") }));
        }
        /// <summary>海不会伪造未建立的旧解释，旧解释稍后建立仍可发生交叉。</summary>
        [Test] public void SeaDoesNotInventAbsentOlderInterpretations()
        {
            var d = DemoRules.Create(); var grant = new DiscoveryRule("test", DemoRules.Paper, "测试纸张", "", Array.Empty<FactId>(), d.Clues.Select(x => x.Id).ToArray());
            var s = new GameSession(new RuleSet(d.Clues.ToArray(), new[] { grant }, d.Recipes.ToArray(), d.Reactions.ToArray()));
            s.Inspect(DemoRules.Paper); Pair(s, "sea", "painting");
            Assert.That(s.State.HasFact(DemoRules.WaterPresent)); Assert.That(s.State.HasFact(DemoRules.PlantAquatic), Is.False); Assert.That(s.State.HasFact(DemoRules.WindowOcean), Is.False);
            Pair(s, "plant", "growth"); Assert.That(s.State.HasFact(DemoRules.PlantAquatic)); Assert.That(s.State.HasFact(DemoRules.Complete), Is.False);
            Pair(s, "far", "window"); Assert.That(s.State.HasFact(DemoRules.Complete));
        }
        /// <summary>打乱反应数据顺序后最终闭包不变。</summary>
        [Test] public void ShuffledReactionDefinitionsHaveIdenticalClosure()
        {
            var d = DemoRules.Create(); var normal = new GameSession(d);
            var shuffled = new GameSession(new RuleSet(d.Clues.ToArray(), d.Discoveries.ToArray(), d.Recipes.ToArray(), d.Reactions.OrderBy(x => x.Id).Reverse().ToArray()));
            foreach (var s in new[] { normal, shuffled }) { BeforeSea(s); s.Inspect(DemoRules.Paper); Pair(s, "painting", "sea"); }
            Assert.That(shuffled.State.Facts, Is.EquivalentTo(normal.State.Facts)); Assert.That(shuffled.State.FiredReactions, Is.EquivalentTo(normal.State.FiredReactions));
        }
        /// <summary>两个互相产生事实的反应不会无限循环。</summary>
        [Test] public void TwoNodeCycleTerminatesWithUniqueFactsAndReactions()
        {
            var d = DemoRules.Create(); var a = new FactId("a"); var b = new FactId("b");
            var recipe = new SemanticRecipe("test", new[] { DemoRules.Clue("plant"), DemoRules.Clue("growth") }, Array.Empty<FactId>(), new[] { a });
            var s = new GameSession(new RuleSet(d.Clues.ToArray(), d.Discoveries.ToArray(), new[] { recipe }, new[] { new ReactionRule("b-a", new[] { b }, new[] { a }), new ReactionRule("a-b", new[] { a }, new[] { b }) }));
            s.Inspect(DemoRules.Plant); var e = s.Combine(recipe.Inputs);
            Assert.That(s.State.Facts.Count, Is.EqualTo(2)); Assert.That(e.OfType<FactAdded>().Count(), Is.EqualTo(2)); Assert.That(s.State.FiredReactions.Count, Is.EqualTo(2));
        }
    }
}
