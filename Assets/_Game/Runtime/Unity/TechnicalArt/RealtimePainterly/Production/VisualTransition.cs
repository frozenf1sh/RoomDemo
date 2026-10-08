// 文件：VisualTransition.cs
// 职责：为生产组件保存一个可中断的标量渐变。
// 边界：仅表现值与时间，不调度游戏事件，不建立通用动画框架。
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>新目标从当前值开始，停止与重置不会留下旧协程。</summary>
    internal sealed class VisualTransition
    {
        private float start, target, duration, elapsed;
        internal float Value { get; private set; }
        internal bool Running => elapsed < duration;

        internal void Reset(float value)
        {
            Value = start = target = Mathf.Clamp01(value);
            duration = elapsed = 0;
        }

        internal void To(float value, float seconds)
        {
            start = Value;
            target = Mathf.Clamp01(value);
            duration = Mathf.Max(0, seconds);
            elapsed = 0;
            if (duration == 0) Value = target;
        }

        internal float Tick(float seconds)
        {
            // 每次只推进当前目标；换目标时不重放上一轮的起点。
            elapsed = Mathf.Min(duration, elapsed + Mathf.Max(0, seconds));
            if (duration > 0)
                Value = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, elapsed / duration));
            return Value;
        }
    }
}
