// =============================================================================
// 文件：NarrativeTests.cs
// 职责：验证两条路线从同一观察开始可达、收束门槛和旧解释外溢。
// 边界：只证明领域依赖与命令结果，不证明旁白情绪或真人理解。
// =============================================================================
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    /// <summary>对当前内容的少量完整路线回归，不模拟 Unity 动画。</summary>
    public sealed class NarrativeTests
    {
        private static void Growth(GameSession s)
        {
            s.Inspect(DemoRules.Plant);
            s.ObserveDetail(DemoRules.Plant, "sprout");
            s.Inspect(DemoRules.Book);
            s.ObserveDetail(DemoRules.Book, "toward_light");
            s.Inspect(DemoRules.Painting);
            Pair(s, "sprout", "toward_light");
        }
        private static void Pair(GameSession s, string a, string b) => s.Combine(new[] { DemoRules.Clue(a), DemoRules.Clue(b) });
        private static void Night(GameSession s)
        {
            s.AdvanceDay();
            s.Inspect(MainlineRules.Ceiling);
            s.ObserveDetail(MainlineRules.Ceiling, "night");
            s.Inspect(DemoRules.Window);
            s.ObserveDetail(DemoRules.Window, "boundless");
            Pair(s, "night", "boundless");
        }
        [Test] public void AllCanvasRouteCompletesWithoutTransformingReality()
        {
            var s = new GameSession(NarrativeRules.Create());
            Growth(s);
            Pair(s, "growth", "painting");
            s.AdvanceDay();
            s.ObserveDetail(DemoRules.Book, "book_far");
            Pair(s, "far", "painting");
            s.Inspect(DemoRules.Paper);
            Pair(s, "sea", "painting");
            Assert.That(s.State.CanvasLocked, Is.True);
            // 锁定只发生在三次核心解释之后；不是第一次服从就惩罚玩家。
            Pair(s, "growth", "plant");
            Assert.That(s.State.HasFact(ExplorationRules.PlantStartsGrowing), Is.False);
            Night(s);
            Pair(s, "starry", "painting");
            Assert.That(s.State.HasFact(NarrativeRules.BanalComplete), Is.True);
            Assert.That(s.State.CompliantCount, Is.EqualTo(4));
            foreach (var fact in new[] { DemoRules.PlantGrown, DemoRules.BookFallen, DemoRules.WindowFar, DemoRules.WaterPresent, MainlineRules.CeilingStarry, MainlineRules.WorldForest })
                Assert.That(s.State.HasFact(fact), Is.False, fact.Value);
        }
        [Test] public void RealityRouteKeepsOldMeaningsAndRevealsRoomLate()
        {
            var s = new GameSession(NarrativeRules.Create());
            Growth(s);
            s.Inspect(MainlineRules.Room);
            Assert.That(s.State.Knows(DemoRules.Clue("room")), Is.False);
            Pair(s, "growth", "plant");
            s.AdvanceDay();
            s.ObserveDetail(DemoRules.Book, "book_far");
            s.Inspect(DemoRules.Window);
            Pair(s, "far", "window");
            s.Inspect(DemoRules.Paper);
            Pair(s, "sea", "painting");
            Assert.That(s.State.HasFact(DemoRules.PlantAquatic), Is.True);
            Assert.That(s.State.HasFact(DemoRules.WindowOcean), Is.True);
            Night(s);
            Pair(s, "starry", "ceiling");
            s.Inspect(MainlineRules.Room);
            Assert.That(s.State.Knows(DemoRules.Clue("room")), Is.True);
            s.ObserveDetail(MainlineRules.Room, "wind_sound");
            s.ObserveDetail(DemoRules.Window, "tree_shadow");
            Pair(s, "tree_shadow", "wind_sound");
            Pair(s, "forest", "room");
            Assert.That(s.State.HasFact(MainlineRules.Complete), Is.True);
            Assert.That(s.State.HasFact(NarrativeRules.BanalComplete), Is.False);
        }
        /// <summary>恢复闭包必须先知道历史倾向，不能在混合路线中误生成普通结局。</summary>
        [Test] public void RestoreUsesNarrativeHistoryBeforeReactionClosure()
        {
            var rules = NarrativeRules.Create();
            var preset = new DebugPreset("mixed", new[] { DemoRules.Clue("growth"),DemoRules.Clue("plant"),DemoRules.Clue("painting"),DemoRules.Clue("far"),DemoRules.Clue("sea"),MainlineRules.Starry },
                new[] { ExplorationRules.PlantStartsGrowing,NarrativeRules.CanvasGrowth,NarrativeRules.CanvasFar,DemoRules.PaintingSea,NarrativeRules.CanvasStarry },
                new[] { "recipe.plant.growth","recipe.painting.growth","recipe.painting.far","recipe.painting.sea","recipe.painting.starry" }, "混合路线");
            var s = GameSession.FromPreset(rules,preset);
            Assert.That(s.State.HasDeviated, Is.True);
            Assert.That(s.State.HasFact(DemoRules.WaterPresent), Is.True);
            Assert.That(s.State.HasFact(NarrativeRules.BanalComplete), Is.False);
        }
        [Test] public void PriorDeviationPreventsLaterCanvasLockAndDuplicateCounts()
        {
            var s = new GameSession(NarrativeRules.Create());
            Growth(s);
            Pair(s, "growth", "plant");
            Pair(s, "growth", "painting");
            Pair(s, "growth", "painting");
            s.AdvanceDay();
            s.ObserveDetail(DemoRules.Book, "book_far");
            Pair(s, "far", "painting");
            s.Inspect(DemoRules.Paper);
            Pair(s, "sea", "painting");
            // 连续三次服从也不能抹掉此前已成立的偏离；重复配方不刷计数。
            Assert.That(s.State.CanvasLocked, Is.False);
            Assert.That(s.State.CompliantCount, Is.EqualTo(3));
            Assert.That(s.State.DeviationCount, Is.EqualTo(1));
            Assert.That(s.State.HasFact(DemoRules.WaterPresent), Is.True);
        }
    }
}
