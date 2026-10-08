// 文件：PainterlyLocalController.cs
// 职责：平滑控制指定 Renderer 或世界空间区域的局部绘画强度与传播大小。
// 边界：复用现有 Influence 合成，不替换对象材质，不写世界事实。
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>指定物体先进入绘画；区域随物体移动，边缘不使用屏幕噪声。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PainterlyInfluence))]
    public sealed class PainterlyLocalController : MonoBehaviour
    {
        [Range(0, 1)] public float TouchedIntensity = .27f;
        [Min(0)] public float TransitionSeconds = 3;
        public Vector3 MaximumRadii = Vector3.one;
        [Range(0, 1)] public float InitialExtent = 1;
        public bool Paused;
        private PainterlyInfluence influence;
        private readonly VisualTransition strength = new VisualTransition();
        private readonly VisualTransition extent = new VisualTransition();

        /// <summary>实际局部权重；全局与局部取最大值，不累加。</summary>
        public float PainterlyIntensity => strength.Value;
        /// <summary>区域半径的当前比例；指定 Renderer 模式不使用这个值。</summary>
        public float Extent => extent.Value;

        private void OnEnable()
        {
            influence = GetComponent<PainterlyInfluence>();
            strength.Reset(influence.PainterlyIntensity);
            extent.Reset(InitialExtent);
        }

        /// <summary>暂停自动演出；直接设置接口仍可用于事实恢复。</summary>
        public void SetPaused(bool value) => Paused = value;

        private void Update() { if (!Paused) Advance(Time.deltaTime); }

        /// <summary>明确绑定接收影响的摄像机，支持相机与效果位于不同的加载场景。</summary>
        public void BindCamera(Camera camera) => GetComponent<PainterlyInfluence>().OnlyCamera = camera;

        /// <summary>指定独立物体；空数组使用世界空间区域。</summary>
        public void SetTargets(Renderer[] targets) => GetComponent<PainterlyInfluence>().Targets = targets;

        /// <summary>平滑提升或退回局部强度；中断旧渐变。</summary>
        /// <param name="value">0～1 的局部强度，不修改全局风格。</param>
        /// <param name="seconds">过渡秒数；零时立即应用，包括暂停状态。</param>
        public void TransitionTo(float value, float seconds) { strength.To(value, seconds); Apply(); }
        /// <summary>立即恢复局部强度；取消旧渐变。</summary>
        public void SetPainterlyIntensity(float value) { strength.Reset(value); Apply(); }
        /// <summary>调整区域大小，按 MaximumRadii 乘 0～1；不改全局风格。</summary>
        public void ExpandTo(float value, float seconds) { extent.To(value, seconds); Apply(); }
        /// <summary>UnityEvent 入口：局部物体先被触碰。</summary>
        public void Touch() => TransitionTo(TouchedIntensity, TransitionSeconds);
        /// <summary>UnityEvent 入口：恢复只受全局档位影响。</summary>
        public void Clear() => TransitionTo(0, TransitionSeconds);

        /// <summary>手动推进时将 Paused 设为 true；对象本身不读取游戏状态。</summary>
        public void Advance(float seconds) { strength.Tick(seconds); extent.Tick(seconds); Apply(); }

        private void Apply()
        {
            if (influence == null) return;
            influence.SetPainterlyIntensity(strength.Value);
            // 空区域仍用最小半径，避免除零；强度为零才表示取消影响。
            influence.Radii = MaximumRadii * Mathf.Max(.001f, extent.Value);
        }
    }
}
