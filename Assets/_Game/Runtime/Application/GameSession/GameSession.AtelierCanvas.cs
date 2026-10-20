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
        /// <summary>显式提交最终表面操作；旧世代和旧画面请求均不会写入当前会话。</summary>
        public AtelierCanvasResult ApplyAtelierFinale(string epoch,int revision,AtelierFinaleAction action,
            System.Collections.Generic.IReadOnlyList<AtelierContactSegment> path,int assistedTaps=0)
            => CompleteCanvas(epoch,()=>AtelierFinaleOperations.Apply(State,Rules,revision,action,path,assistedTaps));
        /// <summary>捕获点灯前的真实入口，不补发任何材料或玩家操作。</summary>
        public AtelierPracticeSnapshot CaptureAtelierPractice() => AtelierPracticeSnapshot.Capture(State,Rules);
        private GameSession(AtelierPracticeSnapshot snapshot)
        {
            Rules=snapshot.Rules;
            State=snapshot.Restore(Rules);
            engine=new RuleEngine(Rules);
        }
        /// <summary>生成新世代的练习会话，旧UI和接触回调不能污染恢复后的作品。</summary>
        public static GameSession FromAtelierPractice(AtelierPracticeSnapshot snapshot)
        {
            if(snapshot==null) throw new ArgumentNullException(nameof(snapshot));
            return new GameSession(snapshot);
        }
    }
}
