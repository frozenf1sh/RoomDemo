// 文件：LivingSkyController.cs
// 职责：控制已通过星夜的局部显现、流动、扩展与暂停，并保持世界方向固定。
// 边界：不换主 Skybox，不改摄像机，不读取世界事实；只管理自己的 GPU 颜料与遮罩。
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>世界朝向的天空球；移动镜头只平移球心，不旋转颜料坐标。</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class LivingSkyController : MonoBehaviour
    {
        [Header("预设内部资源")]
        public GpuPigment Pigment;
        public Renderer Surface;
        public PainterlyInfluence Protection;
        [Header("摄像机与世界方向")]
        public Camera FollowCamera;
        public Vector3 RevealDirection = new Vector3(0, .4f, 1);
        public float SkyYawDegrees;
        public float SkyElevationDegrees;
        [Range(1,8)] public int PanoramaRepeats = 4;
        [Header("表现参数")]
        [Range(0,1)] public float InitialCoverage;
        [Range(0,1)] public float InitialFlow;
        [Range(0,1)] public float Opacity = 1;
        [Range(.01f,.3f)] public float Feather = .07f;
        [Min(0)] public float TransitionSeconds = 5;
        public bool Paused;
        private readonly VisualTransition coverage = new VisualTransition();
        private readonly VisualTransition flow = new VisualTransition();
        private MaterialPropertyBlock properties;

        /// <summary>当前天空覆盖；0 为普通天空，1 为完整星夜。</summary>
        public float Coverage => coverage.Value;
        /// <summary>当前流速；0 时颜料与笔势真正冻结。</summary>
        public float Flow => flow.Value;

        /// <summary>独立预设的颜料与天空面是否已经绑定。</summary>
        public bool IsConfigured => Pigment != null && Surface != null;

        private void Awake() { if (Pigment != null) Pigment.AutomaticTime = false; }
        private void OnEnable()
        {
            properties = new MaterialPropertyBlock();
            coverage.Reset(InitialCoverage);
            flow.Reset(InitialFlow);
            if (Pigment != null) Pigment.AutomaticTime = false;
            BindCamera(FollowCamera != null ? FollowCamera : Camera.main);
            Apply();
        }
        private void Update() { if (!Paused) Advance(Time.deltaTime); }
        private void LateUpdate()
        {
            // 世界旋转保持不变；平移球心消除摄像机走动时的天空视差。
            if (FollowCamera != null && Surface != null) Surface.transform.position = FollowCamera.transform.position;
        }

        /// <summary>明确绑定主摄像机；不依靠正式场景查找、标签或其他 Presenter。</summary>
        public void BindCamera(Camera camera)
        {
            FollowCamera = camera;
            if (Protection != null) Protection.OnlyCamera = camera;
        }

        /// <summary>平滑改变覆盖率，保留当前颜料历史；可被新调用中断。</summary>
        /// <param name="value">0～1 的角覆盖比例，1 为全天空。</param>
        /// <param name="seconds">过渡秒数；零立即应用。</param>
        public void RevealTo(float value, float seconds) { coverage.To(value, seconds); Apply(); }
        /// <summary>立即设置覆盖，供事实恢复或 Sequence 精确采样。</summary>
        public void SetCoverage(float value) { coverage.Reset(value); Apply(); }
        /// <summary>平滑改变实际平流速度；零流速冻结历史。</summary>
        public void FlowTo(float value, float seconds) { flow.To(value, seconds); Apply(); }
        /// <summary>立即设置流速并取消旧速度渐变。</summary>
        public void SetFlow(float value) { flow.Reset(value); Apply(); }
        /// <summary>立即设置天空显现权重；不重建模拟。</summary>
        public void SetOpacity(float value) { Opacity = Mathf.Clamp01(value); Apply(); }
        /// <summary>设置天空局部出现的世界方向，不追随相机转向。</summary>
        public void SetRevealDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude > .00001f) RevealDirection = direction.normalized;
            Apply();
        }
        /// <summary>UnityEvent 入口：在指定方向局部出现，仍保持静止。</summary>
        public void ShowLocal() => RevealTo(.16f, TransitionSeconds);
        /// <summary>UnityEvent 入口：开始已存在颜料的慢流。</summary>
        public void StartFlow() => FlowTo(1, TransitionSeconds);
        /// <summary>UnityEvent 入口：扩展为完整天空。</summary>
        public void ExpandFull() => RevealTo(1, TransitionSeconds);
        /// <summary>UnityEvent 入口：淡出至普通天空，不销毁历史。</summary>
        public void Hide() => RevealTo(0, TransitionSeconds);
        /// <summary>暂停或继续，冻结覆盖渐变、流速渐变和 GPU 历史。</summary>
        public void SetPaused(bool paused) => Paused = paused;
        /// <summary>取消渐变并回到预设初态；用于重开，不授予任何事实。</summary>
        public void ResetEffect()
        {
            coverage.Reset(InitialCoverage);
            flow.Reset(InitialFlow);
            if (Pigment != null)
            {
                // 清空时使用已通过的单位取向，避免上一轮的慢流值改变初始刷毛方向。
                Pigment.Flow = 1;
                Pigment.ResetPigment();
            }
            Apply();
        }

        /// <summary>由 Sequence 手动推进时先设 Paused=true；不重复推进自动时钟。</summary>
        public void Advance(float seconds)
        {
            coverage.Tick(seconds);
            flow.Tick(seconds);
            if (Pigment != null && flow.Value > .0001f && coverage.Value > .0001f)
            {
                Pigment.Flow = flow.Value;
                Pigment.Advance(Mathf.Max(0, seconds));
            }
            Apply();
        }

        private void Apply()
        {
            if (Surface == null || properties == null) return;
            Surface.GetPropertyBlock(properties);
            // 模拟流速可为零；笔触取向仍读取单位流场，避免静止时退成任意对角线。
            properties.SetFloat("_Flow", 1);
            properties.SetFloat("_Coverage", coverage.Value);
            properties.SetFloat("_Opacity", Mathf.Clamp01(Opacity));
            properties.SetFloat("_Feather", Feather);
            properties.SetFloat("_SkyYaw", SkyYawDegrees * Mathf.Deg2Rad);
            properties.SetFloat("_SkyElevation", SkyElevationDegrees * Mathf.Deg2Rad);
            properties.SetFloat("_SkySpan", (360f / Mathf.Max(1, PanoramaRepeats)) * Mathf.Deg2Rad);
            properties.SetVector("_RevealDirection", RevealDirection.normalized);
            Surface.SetPropertyBlock(properties);
        }
    }
}
