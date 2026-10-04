// =============================================================================
// 文件：InspectionModelTests.cs
// 职责：验证检视状态许可与观察不能隔着封面取得。
// 边界：纯 C# 测试不覆盖网格、音效听感或鼠标发现的自然程度。
// =============================================================================
using Game.Application;
using NUnit.Framework;
namespace Game.Domain.Tests
{
    public sealed class InspectionModelTests
    {
        [Test] public void BookMustOpenAndTurnBeforeReadingQuote()
        {
            var model = new InspectionModel(InspectionContent.For("book"));
            // 封面关闭时点击短语必须失败，不改变状态。
            Assert.That(model.TryAct("quote", out var blocked), Is.False); Assert.That(blocked, Is.Null);
            Assert.That(model.TryAct("cover", out _), Is.True); Assert.That(model.State, Is.EqualTo("page1"));
            Assert.That(model.CanAct("quote"), Is.False);
            Assert.That(model.TryAct("next1", out _), Is.True);
            Assert.That(model.TryAct("quote", out var observed), Is.True); Assert.That(observed.ObservationId, Is.EqualTo("toward_light"));
            Assert.That(model.TryAct("next2", out _), Is.True); Assert.That(model.CanAct("quote"), Is.False);
            Assert.That(model.TryAct("previous3", out _), Is.True); Assert.That(model.State, Is.EqualTo("page2"));
        }
        [Test] public void CubeExercisesSameStateCoreAndNewInspectionStartsClosed()
        {
            var definition = InspectionContent.For("test_cube"); var model = new InspectionModel(definition);
            Assert.That(model.TryAct("close", out _), Is.False);
            Assert.That(model.TryAct("open", out _), Is.True); Assert.That(model.State, Is.EqualTo("open"));
            Assert.That(model.TryAct("close", out _), Is.True);
            Assert.That(new InspectionModel(definition).State, Is.EqualTo("closed"));
        }
    }
}
