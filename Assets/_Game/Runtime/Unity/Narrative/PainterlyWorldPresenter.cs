// =============================================================================
// 文件：PainterlyWorldPresenter.cs
// 职责：让已发生的现实解释逐渐染成统一笔触语言，并管理少量有方向的空气光点。
// 边界：风格强度只是表现参数，绝不写入领域或决定剧情；重开归还全局 Shader 参数。
// =============================================================================
using Game.Application;
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>一个具体世界的绘画化消费者；不增加主题框架或新的谜题。</summary>
    public sealed class PainterlyWorldPresenter : MonoBehaviour
    {
        [SerializeField] private ParticleSystem flowingLights;
        private GameSession session;
        private float intensity, target, start, startedAt, duration;
        private static readonly int Painterly = Shader.PropertyToID("_PainterlyIntensity");
        /// <summary>全局材质强度只供视觉调试，不能当作事实查询。</summary>
        public float Intensity => intensity;
        /// <summary>绑定稀疏光点，房间初始保持安静。</summary>
        public void Configure(ParticleSystem lights) { flowingLights = lights; }
        /// <summary>替换会话时取消旧渐变；预设直接读取最终事实。</summary>
        public void Bind(GameSession game)
        {
            if (session != null) session.EventRaised -= OnEvent;
            session = game;
            session.EventRaised += OnEvent;
            Restore();
        }
        private float ReadIntensity()
        {
            // 这是颜色脚本，不产生世界规则；全部事实已经由领域成立。
            var state=session.State;
            if(state.HasFact(MainlineRules.WorldForest)) return 1;
            if(state.HasFact(MainlineRules.CeilingStarry)) return .75f;
            if(state.HasFact(DemoRules.WaterPresent)) return .48f;
            return state.HasDeviated ? .20f : 0;
        }
        private void OnEvent(DomainEvent value)
        {
            if (!(value is FactAdded) && !(value is CombinationSucceeded)) return;
            float next=ReadIntensity();
            if(Mathf.Approximately(target,next)) return;
            start=intensity; target=next; startedAt=Time.unscaledTime;
            duration=session.State.HasFact(MainlineRules.WorldForest) ? 18 : 6;
        }
        /// <summary>恢复事实对应的风格；没有再次闪烁或形成概念的声音。</summary>
        public void Restore()
        {
            intensity=target=ReadIntensity();
            start=target; startedAt=Time.unscaledTime; duration=0;
            if(flowingLights!=null) flowingLights.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            Apply();
        }
        private void Update()
        {
            if(session==null) return;
            float progress=duration<=0 ? 1 : Mathf.Clamp01((Time.unscaledTime-startedAt)/duration);
            intensity=Mathf.Lerp(start,target,Mathf.SmoothStep(0,1,progress));
            Apply();
        }
        private void Apply()
        {
            Shader.SetGlobalFloat(Painterly,intensity);
            // 光点只在屋顶已打开后缓慢进入，不形成 RPG 奖励爆炸。
            if(flowingLights!=null)
            {
                var emission=flowingLights.emission;
                emission.rateOverTime=Mathf.Max(0,(intensity-.50f)*3);
                if(intensity>.50f && !flowingLights.isPlaying) flowingLights.Play();
            }
        }
        private void OnDisable() { Shader.SetGlobalFloat(Painterly,0); }
        private void OnDestroy()
        {
            if(session!=null) session.EventRaised-=OnEvent;
            Shader.SetGlobalFloat(Painterly,0);
        }
    }
}
