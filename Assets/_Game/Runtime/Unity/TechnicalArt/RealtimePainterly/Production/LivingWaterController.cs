// 文件：LivingWaterController.cs
// 职责：把已通过的杯水、圆截面落水与反馈湿膜整理为事件驱动组件。
// 边界：保留原液面 Shader、颜料平流与流束形状；不依赖十秒脚本，不判断游戏事实。
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>水的表现状态；停止保留杯中颜料与桌面痕迹，重置才清除历史。</summary>
    public enum LivingWaterState { Still, Swirl, Overflow, Stopped }

    /// <summary>普通组件入口，可独立指定流速、注入与溢出，允许中断和重播。</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class LivingWaterController : MonoBehaviour
    {
        [Header("预设内部资源")]
        public GpuPigment Cup, Wet;
        public Renderer Water, Stream;
        public Transform LipAnchor, LandingAnchor;
        public Transform[] Drops = new Transform[0];
        [Header("表现参数")]
        [Range(0,1)] public float FlowStrength = 1;
        [Range(0,1)] public float PigmentInjection = 1;
        [Min(0)] public float TransitionSeconds = 2;
        public bool Paused;
        private readonly VisualTransition flow = new VisualTransition();
        private readonly VisualTransition injection = new VisualTransition();
        private readonly VisualTransition overflow = new VisualTransition();
        private Mesh streamMesh;
        private Mesh originalMesh;
        private Vector3[] vertices;
        private MaterialPropertyBlock properties;
        private float clock, motionTime, streamAge;
        private float[] births, nextBirths;
        private const int Along = 40, Across = 16;

        /// <summary>最后请求的表现状态；不作为世界事实来源。</summary>
        public LivingWaterState State { get; private set; }
        /// <summary>当前实际旋流强度。</summary>
        public float Flow => flow.Value;
        /// <summary>当前实际溢出量。</summary>
        public float OverflowAmount => overflow.Value;
        /// <summary>当前颜料注入量。</summary>
        public float Injection => injection.Value;

        /// <summary>资源是否已绑定；预设已完成全部内部引用。</summary>
        public bool IsConfigured => Cup != null && Wet != null && Water != null && Stream != null &&
            LipAnchor != null && LandingAnchor != null && Drops != null && Stream.GetComponent<MeshFilter>() != null;

        private void Awake()
        {
            if (Cup != null) Cup.AutomaticTime = false;
            if (Wet != null) Wet.AutomaticTime = false;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!IsConfigured)
            {
                Debug.LogWarning("杯水资源尚未绑定，请使用 LivingWaterCup.prefab 或配置组件引用。", this);
                return;
            }
            properties = new MaterialPropertyBlock();
            Cup.AutomaticTime = false;
            Wet.AutomaticTime = false;
            if (Wet.Feed == null) Wet.Feed = Cup;
            CreateStream();
            births = new float[Drops.Length];
            nextBirths = new float[Drops.Length];
            ResetEffect();
        }

        private void CreateStream()
        {
            streamMesh = new Mesh { name = "生产杯水_运行时流束", hideFlags = HideFlags.HideAndDontSave };
            streamMesh.MarkDynamic();
            vertices = new Vector3[(Along + 1) * (Across + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Along * Across * 6];
            int index = 0;
            for (int a = 0; a <= Along; a++) for (int b = 0; b <= Across; b++)
            {
                int n = a * (Across + 1) + b;
                uv[n] = new Vector2((float)b / Across, (float)a / Along);
                if (a == Along || b == Across) continue;
                triangles[index++] = n; triangles[index++] = n + 1; triangles[index++] = n + Across + 1;
                triangles[index++] = n + 1; triangles[index++] = n + Across + 2; triangles[index++] = n + Across + 1;
            }
            streamMesh.vertices = vertices;
            streamMesh.uv = uv;
            streamMesh.triangles = triangles;
            var filter = Stream.GetComponent<MeshFilter>();
            originalMesh = filter.sharedMesh;
            filter.sharedMesh = streamMesh;
        }

        private void Update() { if (!Paused) Advance(Time.deltaTime); }

        /// <summary>事件请求新的水状态；中断旧渐变，停止保留已经生成的湿痕。</summary>
        /// <param name="state">静止、旋流、溢出或停止。</param>
        /// <param name="seconds">过渡秒数；零用于立即恢复视觉状态。</param>
        public void SetState(LivingWaterState state, float seconds)
        {
            State = state;
            bool active = state == LivingWaterState.Swirl || state == LivingWaterState.Overflow;
            flow.To(active ? FlowStrength : 0, seconds);
            injection.To(active ? PigmentInjection : 0, seconds);
            overflow.To(state == LivingWaterState.Overflow ? 1 : 0, seconds);
            Advance(0);
        }

        /// <summary>UnityEvent 入口：只旋流与注入，不自动溢出。</summary>
        public void Swirl() => SetState(LivingWaterState.Swirl, TransitionSeconds);
        /// <summary>UnityEvent 入口：从当前状态进入溢出。</summary>
        public void StartOverflow() => SetState(LivingWaterState.Overflow, TransitionSeconds);
        /// <summary>UnityEvent 入口：缓慢停流；已有液滴落完，颜料痕迹保留。</summary>
        public void Stop() => SetState(LivingWaterState.Stopped, TransitionSeconds);
        /// <summary>直接控制旋流。不会自动启动溢出或读取游戏门槛。</summary>
        public void SetFlow(float value) { flow.Reset(value); Advance(0); }
        /// <summary>直接控制颜料注入；设零不清除已经存在的色丝。</summary>
        public void SetPigmentInjection(float value) { injection.Reset(value); Advance(0); }
        /// <summary>直接控制溢出量；液体从杯口出发，不反向缩回杯中。</summary>
        public void SetOverflow(float value) { overflow.Reset(value); Advance(0); }
        /// <summary>暂停冻结全部历史与液滴；游戏暂停也会通过 Time.deltaTime=0 自动停止。</summary>
        public void SetPaused(bool value) => Paused = value;

        /// <summary>取消旧过渡并清除本组件拥有的历史，用于新游戏或重播。</summary>
        public void ResetEffect()
        {
            State = LivingWaterState.Still;
            flow.Reset(0); injection.Reset(0); overflow.Reset(0);
            clock = motionTime = streamAge = 0;
            if (Cup != null) Cup.ResetPigment();
            if (Wet != null) Wet.ResetPigment();
            if (births != null)
                for (int i = 0; i < births.Length; i++) { births[i] = -10; nextBirths[i] = .12f * i; Drops[i].gameObject.SetActive(false); }
            if (properties != null) Advance(0);
        }

        /// <summary>Sequence 手动推进时将 Paused 设为 true，避免 Update 重复推进。</summary>
        public void Advance(float seconds)
        {
            if (properties == null) return;
            float dt = Mathf.Max(0, seconds);
            flow.Tick(dt); injection.Tick(dt); overflow.Tick(dt);
            clock += dt;
            motionTime += dt * flow.Value;
            Cup.Flow = flow.Value;
            Cup.Injection = injection.Value;
            if (flow.Value > .0001f || injection.Value > .0001f) Cup.Advance(dt);

            // 杯口到桌面的前沿只向下推进。停流减小透明度与截面，不把水倒放回杯。
            if (overflow.Value > .0001f) streamAge += dt;
            else streamAge = 0;
            float front = Mathf.SmoothStep(0, 1, Mathf.Clamp01(streamAge / .65f));
            Water.GetPropertyBlock(properties);
            properties.SetFloat("_Flow", flow.Value);
            properties.SetFloat("_Surge", overflow.Value);
            properties.SetFloat("_CupPhase", motionTime);
            Water.SetPropertyBlock(properties);
            DrawStream(front);

            bool falling = DrawDrops(front);
            Wet.Injection = front > .995f ? overflow.Value * .86f : 0;
            if (overflow.Value > .0001f || falling) Wet.Advance(dt);
        }

        private void DrawStream(float front)
        {
            Stream.enabled = overflow.Value > .0001f;
            if (!Stream.enabled || streamMesh == null) return;
            var lip = transform.InverseTransformPoint(LipAnchor.position);
            var landing = transform.InverseTransformPoint(LandingAnchor.position);
            float scale = Mathf.Sqrt(Mathf.Max(.002f, overflow.Value));
            for (int a = 0; a <= Along; a++)
            {
                float t = (float)a / Along * front;
                Vector3 centre = Vector3.Lerp(lip, landing, t);
                centre.y = Mathf.Lerp(lip.y, landing.y, t * t);
                centre.x += Mathf.Sin(t * Mathf.PI) * .045f;
                Vector3 tangent = new Vector3(landing.x - lip.x + .045f * Mathf.PI * Mathf.Cos(t * Mathf.PI),
                    (landing.y - lip.y) * 2 * Mathf.Max(.08f, t), landing.z - lip.z).normalized;
                Vector3 axis = Vector3.Cross(tangent, Vector3.forward).normalized;
                Vector3 other = Vector3.Cross(tangent, axis).normalized;
                float radius = Mathf.Lerp(.043f, .021f, t) * (1 + .16f * Mathf.Sin(t * 29 - motionTime * 5)) * scale;
                for (int b = 0; b <= Across; b++)
                {
                    float angle = (float)b / Across * Mathf.PI * 2;
                    Vector3 point = centre + (axis * Mathf.Cos(angle) + other * Mathf.Sin(angle)) * radius;
                    point.z += Mathf.Sin(t * 21 - motionTime * 4) * .006f * (1 - t);
                    // 网格存储在自己的局部空间，预设平移、转向与等比缩放仍保持连接。
                    vertices[a * (Across + 1) + b] = Stream.transform.InverseTransformPoint(transform.TransformPoint(point));
                }
            }
            streamMesh.vertices = vertices;
            streamMesh.RecalculateNormals();
            streamMesh.RecalculateBounds();
            Stream.GetPropertyBlock(properties);
            properties.SetTexture("_Pigment", Cup.Pigment);
            properties.SetFloat("_Flow", flow.Value);
            properties.SetFloat("_CupPhase", Cup.VisualTime);
            properties.SetFloat("_Sheet", 1);
            properties.SetFloat("_Opacity", .64f * overflow.Value);
            Stream.SetPropertyBlock(properties);
        }

        private bool DrawDrops(float front)
        {
            bool falling = false;
            Vector3 lip = transform.InverseTransformPoint(LipAnchor.position);
            for (int i = 0; i < Drops.Length; i++)
            {
                // 停止后不再重置出生时间，已有三滴各自完成落地，不重复刷粒子。
                if (overflow.Value > .025f && front > .6f && clock >= nextBirths[i])
                {
                    births[i] = clock;
                    nextBirths[i] = clock + 1.12f + i * .035f;
                }
                float age = clock - births[i];
                bool alive = age >= 0 && age < .69f;
                Drops[i].gameObject.SetActive(alive);
                if (!alive) continue;
                falling = true;
                float t = age / .69f;
                Vector3 p = lip + new Vector3(.13f + t * .39f, .16f * t - 1.32f * t * t, .035f + Mathf.Sin(i * 2.7f) * .06f);
                Drops[i].position = transform.TransformPoint(p);
                float size = .028f + Mathf.Sin(i * 3) * .007f;
                Drops[i].localScale = new Vector3(size, size * (1 + 2 * t), size);
                Drops[i].GetComponent<Renderer>().SetPropertyBlock(properties);
            }
            return falling;
        }

        private void OnDisable()
        {
            if (Stream != null)
            {
                var filter = Stream.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh == streamMesh) filter.sharedMesh = originalMesh;
            }
            if (streamMesh != null) Destroy(streamMesh);
            if (Drops != null) foreach (var drop in Drops) if (drop != null) drop.gameObject.SetActive(false);
        }
    }
}
