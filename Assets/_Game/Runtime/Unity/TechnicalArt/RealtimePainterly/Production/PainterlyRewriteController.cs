// 文件：PainterlyRewriteController.cs
// 职责：为已通过的笔势擦开添加可中断渐变、暂停与恢复入口。
// 边界：仅驱动裁剪材质，不修改 Collider、游戏门槛或后方内容。
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>与普通 UnityEvent 和 Sequence 相同的调用方式。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PainterlyOpening))]
    public sealed class PainterlyRewriteController : MonoBehaviour
    {
        [Min(0)] public float TransitionSeconds = 6;
        public bool Paused;
        private PainterlyOpening opening;
        private readonly VisualTransition amount = new VisualTransition();
        /// <summary>当前沿既有笔势擦开的比例；不代表领域中的房间状态。</summary>
        public float Rewrite => amount.Value;

        private void OnEnable()
        {
            opening = GetComponent<PainterlyOpening>();
            amount.Reset(opening.Rewrite);
        }

        /// <summary>暂停自动演出；直接设置接口仍可用于事实恢复。</summary>
        public void SetPaused(bool value) => Paused = value;

        private void Update() { if (!Paused) Advance(Time.deltaTime); }

        /// <summary>沿既有笔势逐渐擦开或恢复；中断旧渐变。</summary>
        /// <param name="value">0～1 的裁剪比例；仅改变显示面。</param>
        /// <param name="seconds">过渡秒数；零时立即应用，包括暂停状态。</param>
        public void RewriteTo(float value, float seconds) { amount.To(value, seconds); Apply(); }
        /// <summary>立即恢复指定画面，取消旧渐变；不会移除碰撞。</summary>
        public void SetRewrite(float value) { amount.Reset(value); Apply(); }
        /// <summary>UnityEvent 入口：擦开到完整的带状区域。</summary>
        public void Open() => RewriteTo(1, TransitionSeconds);
        /// <summary>UnityEvent 入口：恢复完整原表面。</summary>
        public void Close() => RewriteTo(0, TransitionSeconds);
        /// <summary>取消演出并返回未擦开状态。</summary>
        public void ResetEffect() => SetRewrite(0);
        /// <summary>手动推进时将 Paused 设为 true，避免自动时钟重复推进。</summary>
        public void Advance(float seconds) { amount.Tick(seconds); Apply(); }
        private void Apply() { if (opening != null) opening.SetRewrite(amount.Value); }
    }
}
