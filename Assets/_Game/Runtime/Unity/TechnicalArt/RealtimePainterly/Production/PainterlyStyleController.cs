// 文件：PainterlyStyleController.cs
// 职责：将四级共享风格转换为摄像机上的平滑表现权重。
// 边界：仅驱动 PainterlyCamera，不判断世界阶段，不修改领域事实或项目管线。
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>同一算法的四级强度；不是四种画风。</summary>
    public enum PainterlyStage { Reality, Touched, Dream, Final }

    /// <summary>供 Presenter、Sequence 和 UnityEvent 调用的全局风格入口。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PainterlyCamera))]
    public sealed class PainterlyStyleController : MonoBehaviour
    {
        [Header("四级共享风格")]
        public PainterlyStyleProfile Reality, Touched, Dream, Final;
        [Min(0)] public float TransitionSeconds = 3;
        public bool ApplyRealityOnEnable = true;
        public bool Paused;
        private PainterlyCamera view;
        private readonly VisualTransition intensity = new VisualTransition();

        /// <summary>当前已应用强度；只用于表现调度或诊断。</summary>
        public float PainterlyIntensity => intensity.Value;

        private void OnEnable()
        {
            view = GetComponent<PainterlyCamera>();
            intensity.Reset(ApplyRealityOnEnable ? Amount(PainterlyStage.Reality) : view.PainterlyIntensity);
            view.SetPainterlyIntensity(intensity.Value);
        }

        /// <summary>暂停自动演出；直接设置接口仍可用于事实恢复。</summary>
        public void SetPaused(bool value) => Paused = value;

        private void Update()
        {
            if (!Paused) Advance(Time.deltaTime);
        }

        private float Amount(PainterlyStage stage)
        {
            var profile = stage == PainterlyStage.Reality ? Reality : stage == PainterlyStage.Touched ? Touched : stage == PainterlyStage.Dream ? Dream : Final;
            return profile != null ? profile.PainterlyIntensity : stage == PainterlyStage.Reality ? .06f : stage == PainterlyStage.Touched ? .27f : stage == PainterlyStage.Dream ? .62f : 1;
        }

        /// <summary>切换风格。新请求中断旧渐变，并从当前画面继续；秒数为零时立即恢复。</summary>
        /// <param name="stage">现实、触碰、梦境或完整绘画的表现档位。</param>
        /// <param name="seconds">渐变秒数，负值按零处理。</param>
        public void TransitionTo(PainterlyStage stage, float seconds)
        {
            intensity.To(Amount(stage), seconds);
            if (view != null) view.SetPainterlyIntensity(intensity.Value);
        }

        /// <summary>立即设置表现强度；取消先前风格渐变。</summary>
        public void SetPainterlyIntensity(float value)
        {
            intensity.Reset(value);
            if (view != null) view.SetPainterlyIntensity(intensity.Value);
        }

        /// <summary>UnityEvent 入口：按 Inspector 秒数返回轻现实。</summary>
        public void ShowReality() => TransitionTo(PainterlyStage.Reality, TransitionSeconds);
        /// <summary>UnityEvent 入口：按 Inspector 秒数进入触碰。</summary>
        public void ShowTouched() => TransitionTo(PainterlyStage.Touched, TransitionSeconds);
        /// <summary>UnityEvent 入口：按 Inspector 秒数进入梦境。</summary>
        public void ShowDream() => TransitionTo(PainterlyStage.Dream, TransitionSeconds);
        /// <summary>UnityEvent 入口：按 Inspector 秒数进入完整绘画。</summary>
        public void ShowFinal() => TransitionTo(PainterlyStage.Final, TransitionSeconds);

        /// <summary>Sequence 手动推进；调用时将 Paused 设为 true，避免 Update 重复推进。</summary>
        public void Advance(float seconds)
        {
            if (view != null) view.SetPainterlyIntensity(intensity.Tick(seconds));
        }
    }
}
