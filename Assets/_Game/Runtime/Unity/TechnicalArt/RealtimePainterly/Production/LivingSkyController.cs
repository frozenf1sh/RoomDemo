// 文件：LivingSkyController.cs
// 职责：控制原版颜料星夜的显现、真实平流与有限历史交接。
// 边界：只消费表现指令，不读取世界事实；固定画幅由 FixedViewSky 负责显示。
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>原版颜料星夜的表现控制；固定画幅与旧球面载体共用同一套显现和运动 API。</summary>
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
        [Header("颜料历史")]
        public bool BoundedHistory = true;
        [Min(16)] public float HistorySeconds = 21;
        [Min(.5f)] public float HistoryBlendSeconds = 6;
        public bool FixedView;
        [Range(0,1)] public float InitialLight = 1;
        /// <summary>正式画室分开呈现星光与云层笔势；独立 TA 预设仍保留完整笔触。</summary>
        public bool SeparateLightAndFlow;
        /// <summary>四阶段天空把云的显色与实际流动解耦，旧预设仍沿用原协议。</summary>
        public bool IndependentCloudPaint;
        private readonly VisualTransition cloud=new VisualTransition();
        public float CloudPaint => cloud.Value;
        /// <summary>只改变云的绘画化，不推进GPU颜料的时间。</summary>
        public void CloudTo(float value,float seconds){cloud.To(value,seconds);Apply();}
        private readonly VisualTransition light = new VisualTransition();
        /// <summary>独立控制暖星与颜料色彩，保留点亮解释。</summary>
        public void LightTo(float value, float seconds) { light.To(value, seconds); Apply(); }
        private readonly VisualTransition coverage = new VisualTransition();
        private readonly VisualTransition flow = new VisualTransition();
        private MaterialPropertyBlock properties;
        private SkyPigmentHistory history;
        /// <summary>已完成的历史交接次数，仅供视觉检查。</summary>
        public int HistoryHandoffs => history != null ? history.Handoffs : 0;
        /// <summary>当前最老可见历史的时间；不会随游戏会话无限增加。</summary>
        public float HistoryAge => history != null ? history.OldestTime : 0;
        /// <summary>重置序号，供画幅摄像机丢弃上一轮 TAA 历史。</summary>
        public int ResetGeneration { get; private set; }

        /// <summary>当前天空覆盖；0 为普通天空，1 为完整星夜。</summary>
        public float Coverage => coverage.Value;
        /// <summary>当前颜料流速；0 时停止历史、笔势和交接。</summary>
        public float Flow => flow.Value;

        /// <summary>独立预设的颜料与天空面是否已经绑定。</summary>
        public bool IsConfigured => Pigment != null && Surface != null;

        private void Awake() { if (Pigment != null) Pigment.AutomaticTime = false; }
        private void OnEnable()
        {
            properties = new MaterialPropertyBlock();
            coverage.Reset(InitialCoverage);
            flow.Reset(InitialFlow);
            light.Reset(InitialLight);
            cloud.Reset(IndependentCloudPaint?0:1);
            history?.Dispose();
            history = Application.isPlaying && Pigment != null ? new SkyPigmentHistory(Pigment) : null;
            if (Pigment != null)
            {
                Pigment.AutomaticTime = false;
                // 初始化刷毛仍取单位流场，显现强度与运动时钟不会改变初始颜料。
                Pigment.Flow = 1;
            }
            BindCamera(FollowCamera != null ? FollowCamera : Camera.main);
            Apply();
        }
        private void Update() { if (!Paused) Advance(Time.deltaTime); }
        private void LateUpdate()
        {
            // 世界旋转保持不变；平移球心消除摄像机走动时的天空视差。
            if (!FixedView && FollowCamera != null && Surface != null) Surface.transform.position = FollowCamera.transform.position;
        }

        /// <summary>明确绑定主摄像机；不依靠正式场景查找、标签或其他 Presenter。</summary>
        public void BindCamera(Camera camera)
        {
            FollowCamera = camera;
            if (Protection != null) Protection.OnlyCamera = camera;
        }

        /// <summary>平滑改变覆盖率，保留程序化初始颜料；可被新调用中断。</summary>
        /// <param name="value">0～1 的显现比例；固定画幅淡入，旧球面展开角覆盖。</param>
        /// <param name="seconds">过渡秒数；零立即应用。</param>
        public void RevealTo(float value, float seconds) { coverage.To(value, seconds); Apply(); }
        /// <summary>立即设置覆盖，供事实恢复或 Sequence 精确采样。</summary>
        public void SetCoverage(float value) { coverage.Reset(value); Apply(); }
        /// <summary>平滑改变真实颜料与笔势的时间速度，零停止全部运动。</summary>
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
        /// <summary>UnityEvent 入口：开始原版的颜料云和笔势运动。</summary>
        public void StartFlow() => FlowTo(1, TransitionSeconds);
        /// <summary>UnityEvent 入口：扩展为完整天空。</summary>
        public void ExpandFull() => RevealTo(1, TransitionSeconds);
        /// <summary>UnityEvent 入口：淡出至普通天空，不销毁初始颜料。</summary>
        public void Hide() => RevealTo(0, TransitionSeconds);
        /// <summary>暂停或继续，冻结覆盖、流速与实际 GPU 历史。</summary>
        public void SetPaused(bool paused) => Paused = paused;
        /// <summary>取消渐变并回到预设初态；用于重开，不授予任何事实。</summary>
        public void ResetEffect()
        {
            ResetGeneration++;
            coverage.Reset(InitialCoverage);
            flow.Reset(InitialFlow);
            light.Reset(InitialLight);
            cloud.Reset(IndependentCloudPaint?0:1);
            if (history != null) history.Reset();
            else if (Pigment != null)
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
            light.Tick(seconds);
            cloud.Tick(seconds);
            if (flow.Value > .0001f && coverage.Value > .0001f)
            {
                history?.Advance(Mathf.Min(.1333f, Mathf.Max(0, seconds)) * flow.Value,
                    BoundedHistory, HistorySeconds, HistoryBlendSeconds);
            }
            Apply();
        }

        private void Apply()
        {
            if (Surface == null || properties == null) return;
            Surface.GetPropertyBlock(properties);
            // 显示取向与当前两份实际颜料共用时间；Flow 只在推进层缩放，不重复乘速。
            properties.SetFloat("_Flow", 1);
            if (history != null && history.Current != null)
            {
                properties.SetTexture("_Pigment", history.Current);
                properties.SetTexture("_PigmentNext", history.Next);
                properties.SetFloat("_SkyHistoryBlend", history.Blend);
                properties.SetFloat("_VisualTime", history.SampleTime);
            }
            properties.SetFloat("_StarGain", light.Value);
            properties.SetFloat("_CloudPaint", IndependentCloudPaint?cloud.Value:SeparateLightAndFlow?Mathf.Clamp01(flow.Value/.6f):1);
            properties.SetFloat("_Coverage", coverage.Value);
            properties.SetFloat("_Opacity", Mathf.Clamp01(Opacity));
            properties.SetFloat("_Feather", Feather);
            properties.SetFloat("_SkyYaw", SkyYawDegrees * Mathf.Deg2Rad);
            properties.SetFloat("_SkyElevation", SkyElevationDegrees * Mathf.Deg2Rad);
            properties.SetFloat("_SkySpan", (360f / Mathf.Max(1, PanoramaRepeats)) * Mathf.Deg2Rad);
            properties.SetVector("_RevealDirection", RevealDirection.normalized);
            Surface.SetPropertyBlock(properties);
        }

        private void OnDisable()
        {
            // 第二份历史由本控制器拥有；场景原模拟的生命周期仍归其自身。
            history?.Dispose();
            history = null;
        }
    }
}
