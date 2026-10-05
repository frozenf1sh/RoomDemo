// =============================================================================
// 文件：CanvasRoutePresenter.cs
// 职责：把交给画的意义逐层留在一个静止、有边框的作品中。
// 边界：只读事实；外溢演出仍由原海画导演消费，不控制其他 Presenter。
// =============================================================================
using Game.Application;
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>树、距离、海与夜空在画内完成；同一材质通过属性块保持独立。</summary>
    public sealed class CanvasRoutePresenter : MonoBehaviour
    {
        [SerializeField] private Renderer surface;
        private GameSession session;
        private MaterialPropertyBlock properties;
        private Vector4 layers, target;
        private static readonly int Layers = Shader.PropertyToID("_CanvasLayers");
        /// <summary>只注入画内独立表面，不借用物体身份或碰撞体。</summary>
        public void Configure(Renderer canvas) { surface = canvas; }
        /// <summary>重开取消旧画进度和监听，预设立即恢复已完成层。</summary>
        public void Bind(GameSession game)
        {
            if (session != null) session.EventRaised -= OnEvent;
            session = game;
            properties = properties ?? new MaterialPropertyBlock();
            session.EventRaised += OnEvent;
            Restore();
        }
        /// <summary>已成立的海若越过画框，静止作品层让出表面；这只是视觉所有权。</summary>
        private void OnEvent(DomainEvent value)
        {
            if (!(value is FactAdded)) return;
            ReadTarget();
            surface.enabled = !session.State.HasFact(DemoRules.WaterPresent);
        }
        private void ReadTarget()
        {
            var state = session.State;
            target = new Vector4(state.HasFact(NarrativeRules.CanvasGrowth) ? 1 : 0,
                state.HasFact(NarrativeRules.CanvasFar) ? 1 : 0, state.HasFact(DemoRules.PaintingSea) ? 1 : 0,
                state.HasFact(NarrativeRules.CanvasStarry) ? 1 : 0);
        }
        /// <summary>次日或重建立即设置画内各层，不重播完成动画。</summary>
        public void Restore()
        {
            if (session == null) return;
            ReadTarget();
            layers = target;
            surface.enabled = !session.State.HasFact(DemoRules.WaterPresent);
            Write();
        }
        /// <summary>历史灰盒没有画布路线，归还独立表面。</summary>
        public void Hide() { if (surface != null) surface.enabled = false; }
        private void Update()
        {
            // 这里只让颜料逐层落在画布上，作品中的树和海不参与房间规则。
            if (session == null || properties == null) return;
            layers = Vector4.MoveTowards(layers, target, Time.deltaTime / 2.2f);
            Write();
        }
        private void Write()
        {
            properties.Clear();
            properties.SetVector(Layers, layers);
            surface.SetPropertyBlock(properties);
        }
        private void OnDestroy() { if (session != null) session.EventRaised -= OnEvent; }
    }
}
