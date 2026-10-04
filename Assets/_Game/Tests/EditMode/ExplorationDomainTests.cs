// =============================================================================
// 文件：ExplorationDomainTests.cs
// 职责：验证普通点击与细节发现严格区分，并完整形成生长概念。
// 边界：观察命令来自通过页许可的检视输入；领域不读取 Collider 或页动画。
// =============================================================================
using System.Linq;
using Game.Application;
using NUnit.Framework;
namespace Game.Domain.Tests
{
    public sealed class ExplorationDomainTests
    {
        [Test] public void ClickingPlantAndBookCannotDirectlyGrantGrowthOrObservations()
        {
            var game = new GameSession(ExplorationRules.Create()); game.Inspect(DemoRules.Plant); game.Inspect(DemoRules.Book);
            Assert.That(game.State.Knows(DemoRules.Growth) || game.State.Knows(ExplorationRules.Sprout) || game.State.Knows(ExplorationRules.TowardLight), Is.False);
            Assert.That(game.State.KnownClues.Count, Is.EqualTo(2));
        }
        [Test] public void TwoPersonallyObservedDetailsFormGrowthThenInterpretPlant()
        {
            var game = new GameSession(ExplorationRules.Create());
            Assert.That(game.ObserveDetail(DemoRules.Plant, "sprout").Single(), Is.TypeOf<InspectionFailed>());
            game.Inspect(DemoRules.Plant); game.ObserveDetail(DemoRules.Plant, "sprout");
            game.Inspect(DemoRules.Book); game.ObserveDetail(DemoRules.Book, "toward_light");
            game.Combine(new[] { ExplorationRules.TowardLight, ExplorationRules.Sprout });
            Assert.That(game.State.Knows(DemoRules.Growth), Is.True); Assert.That(game.State.Facts, Is.Empty);
            game.Combine(new[] { DemoRules.Clue("plant"), DemoRules.Clue("growth") });
            Assert.That(game.State.HasFact(ExplorationRules.PlantStartsGrowing), Is.True);
            Assert.That(game.State.HasFact(DemoRules.BookFallen), Is.False);
        }
        [Test] public void RepeatedDetailDoesNotDuplicateClueAndCannotGrantAnotherObject()
        {
            var game = new GameSession(ExplorationRules.Create()); game.Inspect(DemoRules.Plant); game.ObserveDetail(DemoRules.Plant, "sprout");
            Assert.That(game.ObserveDetail(DemoRules.Plant, "sprout").OfType<ClueDiscovered>(), Is.Empty);
            game.Inspect(DemoRules.Book); game.ObserveDetail(DemoRules.Book, "toward_light");
            Assert.That(game.State.Knows(DemoRules.Window) || game.State.Knows(DemoRules.Painting), Is.False);
            Assert.That(game.State.KnownClues.Count, Is.EqualTo(4));
        }
        /// <summary>所有里程碑由相同内容表恢复，历史与输出一致，最终样板也有明确事实。</summary>
        [Test] public void AllExplorationPresetsRestoreWithConsistentCluesFactsAndHistory()
        {
            foreach (var preset in ExplorationPresets.All)
            {
                var game = GameSession.FromPreset(ExplorationRules.Create(), preset);
                Assert.That(game.State.DayIndex, Is.EqualTo(preset.DayIndex));
                Assert.That(preset.Known.All(game.State.Knows), Is.True);
                Assert.That(preset.Facts.All(game.State.HasFact), Is.True);
                Assert.That(preset.CompletedRecipes.All(game.State.HasCompleted), Is.True);
            }
        }
        /// <summary>世界样板只声明身份；添加它不授予物体、不执行配方或延迟演出。</summary>
        [Test] public void DeclaredWorldFactDoesNotInventCluesRecipesOrDeferredEffects()
        {
            var game = new GameSession(ExplorationRules.Create());
            var events = game.DebugAddFact(ExplorationRules.WorldStarry);
            Assert.That(events.Single(), Is.TypeOf<FactAdded>());
            Assert.That(game.State.Facts.Count, Is.EqualTo(1));
            Assert.That(game.State.KnownClues, Is.Empty);
            Assert.That(game.State.CompletedRecipes, Is.Empty);
            Assert.That(game.State.PendingDeferredEffects, Is.Empty);
        }
    }
}
