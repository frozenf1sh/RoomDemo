// =============================================================================
// 文件：DebugPresetTests.cs
// 职责：验证预设闭包、组合历史、调试授予与错误输入边界。
// 边界：调试复用正式规则；授予线索不能自动求解，错误不能修改状态。
// =============================================================================
using System;
using System.Linq;
using Game.Application;
using Game.Domain;
using NUnit.Framework;
namespace Game.Tests
{
    /// <summary>验证预设闭包、组合历史、调试授予与错误输入边界。</summary>
    public sealed class DebugPresetTests
    {
        /// <summary>海之前的预设可继续提交正式配方通关。</summary>
        [Test] public void BeforeSeaCanCompleteThroughTheRealRecipe()
        {
            var s = GameSession.FromPreset(DemoRules.Create(), DebugPresets.BeforeSea);
            // 以下断言检查可观察结果；测试不依赖场景或规则实现的内部字段。
            Assert.That(s.State.Facts.Count, Is.EqualTo(6)); Assert.That(s.State.HasFact(DemoRules.PaintingSea), Is.False);
            Assert.That(s.State.CompletedRecipes.Count, Is.EqualTo(2)); Assert.That(s.State.FiredReactions.Count, Is.EqualTo(4));
            s.Combine(new[] { DemoRules.Clue("sea"), DemoRules.Clue("painting") }); Assert.That(s.State.HasFact(DemoRules.Complete));
        }
        /// <summary>涌现预设的事实、反应和组合历史保持一致。</summary>
        [Test] public void EmergencePresetRunsClosureAndAgreesWithRecipeHistory()
        {
            var s = GameSession.FromPreset(DemoRules.Create(), DebugPresets.Emergence);
            Assert.That(s.State.Facts.Count, Is.EqualTo(11)); Assert.That(s.State.CompletedRecipes.Count, Is.EqualTo(3)); Assert.That(s.State.FiredReactions.Count, Is.EqualTo(8));
            Assert.That(s.DebugAddFact(DemoRules.PaintingSea), Is.Empty);
        }
        /// <summary>授予全部碎片不等于完成解谜，重复授予无事件。</summary>
        [Test] public void GrantAllOnlyAddsCluesWithoutSolvingTheRoom()
        {
            var s = new GameSession(DemoRules.Create()); s.DebugGrantAllClues(); Assert.That(s.State.KnownClues.Count, Is.EqualTo(7));
            Assert.That(s.State.Facts, Is.Empty); Assert.That(s.State.CompletedRecipes, Is.Empty); Assert.That(s.DebugGrantAllClues(), Is.Empty);
            Assert.That(s.Combine(new[] { DemoRules.Clue("plant"), DemoRules.Clue("growth"), DemoRules.Clue("sea") }).Single(), Is.TypeOf<CombinationFailed>());
        }
        /// <summary>非法 ID 在修改前被拒绝，合法事实正常推进闭包。</summary>
        [Test] public void AddingFactEvaluatesClosureAndRejectsTyposBeforeMutation()
        {
            var s = new GameSession(DemoRules.Create()); Assert.Throws<ArgumentException>(() => s.DebugAddFact(new FactId("plant.gorwn"))); Assert.That(s.State.Facts, Is.Empty);
            s.DebugAddFact(DemoRules.PlantGrown); Assert.That(s.State.HasFact(DemoRules.BookInspectable)); Assert.That(s.State.FiredReactions.Count, Is.EqualTo(2));
        }
    }
}
