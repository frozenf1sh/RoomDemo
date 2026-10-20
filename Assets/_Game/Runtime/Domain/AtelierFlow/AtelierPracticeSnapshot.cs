// 文件：AtelierPracticeSnapshot.cs
// 职责：封装第四阶段点灯前的内存练习快照。
// 边界：只复制真实前三层与当时的知识，不序列化到磁盘，不允许UI改写集合。
using System;
namespace Game.Domain
{
    /// <summary>可重复恢复的不可变入口，后续本局操作不会污染它。</summary>
    public sealed class AtelierPracticeSnapshot
    {
        private readonly GameState frozen;
        private readonly RuleSet rules;
        private AtelierPracticeSnapshot(GameState state,RuleSet content) { rules=content;frozen=state.CopyAtelierPractice(content); }
        /// <summary>只有真实第三层已收笔、第四层未动笔、灯未点燃时允许建立。</summary>
        public static AtelierPracticeSnapshot Capture(GameState state,RuleSet content)
        {
            if(!state.HasFact(AtelierFlowRules.Finished(3))||state.HasFact(StudioRules.LampTurned)
                ||state.AtelierCanvas.Layers[3].Count!=0||state.CanvasLocked||state.HasPendingDeferredEffects)
                throw new InvalidOperationException("练习快照只能建立在第四阶段点灯前。");
            return new AtelierPracticeSnapshot(state,content);
        }
        /// <summary>构造新的领域状态，保留前三层，第四层、点灯、屋面及结局均处于入口状态。</summary>
        public GameState Restore(RuleSet content)
        {
            if(!ReferenceEquals(content,rules)) throw new InvalidOperationException("练习快照的内容版本不一致。");
            return frozen.CopyAtelierPractice(content);
        }
        /// <summary>恢复时使用捕获时相同的不可变内容表。</summary>
        public RuleSet Rules => rules;
    }
}
