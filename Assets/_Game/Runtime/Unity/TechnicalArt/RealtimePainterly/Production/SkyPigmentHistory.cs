// 文件：SkyPigmentHistory.cs
// 职责：复用原 GPU 平流，让两份有限寿命的颜料历史平滑交接。
// 边界：只拥有第二份模拟；原模拟仍由场景拥有，不改变杯水或领域事实。
using UnityEngine;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>首轮保留原 Hero 的完整运动，之后在历史老化前交接；不倒放或冻结颜料。</summary>
    internal sealed class SkyPigmentHistory
    {
        private readonly GpuPigment primary;
        private GpuPigment active, incoming;
        private GameObject owned;
        private bool incomingStarted;
        internal int Handoffs { get; private set; }
        internal float Blend { get; private set; }
        internal RenderTexture Current => active != null ? active.Pigment : null;
        internal RenderTexture Next => incoming != null ? incoming.Pigment : Current;
        internal float SampleTime => incomingStarted ? Mathf.Lerp(active.VisualTime, incoming.VisualTime, Blend) : active.VisualTime;
        internal float OldestTime => active != null ? active.VisualTime : 0;

        internal SkyPigmentHistory(GpuPigment source)
        {
            primary = active = source;
        }

        private void CreateIncoming()
        {
            if (owned != null) return;
            owned = new GameObject("星夜_有限历史交接") { hideFlags = HideFlags.HideAndDontSave };
            owned.SetActive(false);
            owned.transform.SetParent(primary.transform, false);
            incoming = owned.AddComponent<GpuPigment>();
            incoming.SimulationShader = primary.SimulationShader;
            incoming.Width = primary.Width;
            incoming.Height = primary.Height;
            incoming.Mode = 0;
            incoming.Flow = 1;
            incoming.AutomaticTime = false;
            owned.SetActive(true);
        }

        /// <summary>推进实际颜料时间，限制单帧补偿，避免卡顿后一次提交大量 GPU 工作。</summary>
        internal void Advance(float seconds, bool bounded, float lifetime, float blendSeconds)
        {
            float dt = Mathf.Clamp(seconds, 0, .1333f);
            if (dt <= 0 || primary == null || primary.Pigment == null) return;
            lifetime = Mathf.Max(16, lifetime);
            blendSeconds = Mathf.Clamp(blendSeconds, .5f, lifetime * .45f);
            active.Flow = 1;
            active.Advance(dt);
            if (!bounded) { Blend = 0; return; }
            CreateIncoming();
            if (!incomingStarted && active.VisualTime >= lifetime * .5f)
            {
                // 另一份历史在完全不可见时初始化；显现时已经积累了真实平流。
                incoming.Flow = 1;
                incoming.ResetPigment();
                incomingStarted = true;
            }
            else if (incomingStarted)
            {
                incoming.Flow = 1;
                incoming.Advance(dt);
            }
            Blend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(lifetime - blendSeconds, lifetime, active.VisualTime));
            if (active.VisualTime < lifetime) return;
            // 权重到一以后交换角色，旧历史清空时贡献为零，不会出现回绕闪帧。
            (active, incoming) = (incoming, active);
            incoming.ResetPigment();
            incomingStarted = true;
            Blend = 0;
            Handoffs++;
        }

        internal void Reset()
        {
            active = primary;
            if (primary != null) { primary.Flow = 1; primary.ResetPigment(); }
            if (owned != null)
            {
                incoming = owned.GetComponent<GpuPigment>();
                incoming.ResetPigment();
            }
            incomingStarted = false;
            Blend = 0;
            Handoffs = 0;
        }

        internal void Dispose()
        {
            if (owned != null) Object.Destroy(owned);
            owned = null;
            incoming = null;
        }
    }
}
