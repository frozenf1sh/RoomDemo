// =============================================================================
// 文件：NarrativeDisposition.cs
// 职责：定义解释的薄叙事倾向和由成功历史派生的旁白阶段。
// 边界：纯数据协议；不含旁白文本、播放时间、画布 Shader 或声音。
// =============================================================================
namespace Game.Domain
{
    /// <summary>中性联想、服从画布指引、偏离画布指引；玩家看不到这些分类。</summary>
    public enum NarrativeDisposition { Neutral, Compliant, Deviant }
    /// <summary>计数派生的内部阶段，没有复杂分支图或随机锁定。</summary>
    public enum NarrativeStage { Normal, SlightlyConfused, Concerned, LosingAuthority, CanvasLockIn }
}
