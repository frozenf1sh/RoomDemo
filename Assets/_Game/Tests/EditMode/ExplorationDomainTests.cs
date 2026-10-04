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
    }
}
