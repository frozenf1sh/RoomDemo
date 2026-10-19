// 文件：GameSession.AtelierCanvas.cs
// 职责：为UI和世界输入提供同一组画布用例与会话世代保护。
// 边界：不持有第二份画作；只调用领域账本并在整个命令完成后发布事件。
using System;
using Game.Domain;
namespace Game.Application
{
    /// <summary>新画布的明确入口；旧GameSession命令和规则表保持兼容。</summary>
    public sealed partial class GameSession
    {
        /// <summary>每次重开生成新世代，旧UI回调不能落入新会话。</summary>
        public string SessionEpoch { get; } = Guid.NewGuid().ToString("N");
        private AtelierCanvasResult CompleteCanvas(string epoch, Func<AtelierCanvasResult> command)
        {
            if (epoch != SessionEpoch) return new AtelierCanvasResult(false,"会话已经重新开始。");
            var result = command();
            // 所有扣量、笔触和阶段事实已经完成，再让UI和TA读取同一个最终状态。
            Publish(result.Events);
            return result;
        }
        /// <summary>提交一枚笔印，不按鼠标路径连续盖章。</summary>
        public AtelierCanvasResult CommitAtelierStroke(string epoch, int revision, AtelierStroke stroke)
            => CompleteCanvas(epoch, () => AtelierCanvasOperations.Commit(State,Rules,revision,stroke));
        /// <summary>撤销当前未封存层最后一笔，精确退款。</summary>
        public AtelierCanvasResult UndoAtelierStroke(string epoch, int revision)
            => CompleteCanvas(epoch, () => AtelierCanvasOperations.Undo(State,Rules,revision));
        /// <summary>主动封存当前层，不由颜料耗尽或计时器代按完成。</summary>
        public AtelierCanvasResult FinishAtelierLayer(string epoch, int revision)
            => CompleteCanvas(epoch, () => AtelierCanvasOperations.Finish(State,Rules,revision));
    }
}
