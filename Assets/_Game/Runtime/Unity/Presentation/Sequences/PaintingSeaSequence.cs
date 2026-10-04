// =============================================================================
// 文件：PaintingSeaSequence.cs
// 职责：按画 → 水 → 植物 → 窗外 → 房间的顺序导演这一个涌现段落。
// 边界：只读取已经成立的事实，延迟视觉和声音；绝不延迟、添加或撤销领域事实。
// =============================================================================
using System.Collections;
using Game.Application;
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>单个具体演出段落，使用协程，不建立通用电影或规则调度框架。</summary>
    public sealed class PaintingSeaSequence : MonoBehaviour
    {
        [SerializeField] private PaintingPresenter painting;
        [SerializeField] private WaterPresenter water;
        [SerializeField] private PlantPresenter plant;
        [SerializeField] private WindowPresenter window;
        [SerializeField] private EndingPresenter ending;
        [SerializeField, Min(.1f)] private float anomalyAt = 1.2f;
        [SerializeField, Min(.1f)] private float seaAt = 2.2f;
        [SerializeField, Min(.1f)] private float waterAt = 4.8f;
        [SerializeField, Min(.1f)] private float plantAt = 7.0f;
        [SerializeField, Min(.1f)] private float windowAt = 10.2f;
        [SerializeField, Min(.1f)] private float atmosphereAt = 13.5f;
        [SerializeField, Min(.1f)] private float finishAt = 17.0f;
        private GameSession session;
        private WorldPresenter[] presenters;
        private Coroutine playing;
        private bool finalShown, preview;
        private float startedAt, elapsed;
        /// <summary>只表示协程正在展现，并不代表世界规则尚未计算。</summary>
        public bool IsPlaying { get; private set; }
        /// <summary>已成立的海画尚未展现完成，用于避免 UI 提前剧透结果。</summary>
        public bool IsBenchmarkPending => session != null && session.State.HasFact(DemoRules.PaintingSea) && !finalShown;
        /// <summary>当前节拍的中文名称，供表现调试显示。</summary>
        public string CurrentBeat { get; private set; } = "静止";
        /// <summary>以非缩放时间记录进度；暂停游戏时间不会挂起调试取消操作。</summary>
        public float Elapsed => IsPlaying ? Time.unscaledTime - startedAt : elapsed;
        /// <summary>完整节拍的实际设计时长，测试不另写一份魔法等待时间。</summary>
        public float Duration => finishAt;
        /// <summary>显式注入五个具体节拍，Presenter 彼此没有引用。</summary>
        public void Configure(PaintingPresenter art, WaterPresenter drops, PlantPresenter leaves, WindowPresenter exterior, EndingPresenter ambience)
        { painting = art; water = drops; plant = leaves; window = exterior; ending = ambience; }
        /// <summary>更换会话前取消旧演出和监听；预设恢复不会再播一遍。</summary>
        public void Bind(GameSession game, WorldPresenter[] views)
        {
            Cancel();
            if (session != null) session.EventRaised -= OnEvent;
            session = game; presenters = views; preview = false;
            finalShown = session.State.HasFact(DemoRules.PaintingSea);
            session.EventRaised += OnEvent;
            CurrentBeat = finalShown ? "即时恢复" : "静止";
        }
        /// <summary>过滤这段演出管理的事实；其他事实仍立即交给对应 Presenter。</summary>
        public bool ShouldPresentImmediately(FactId fact)
        {
            if (fact == DemoRules.PaintingSea) return false;
            // 没有海画演出时，独立调试事实仍可直接表现，随后由启动层即时恢复。
            return !IsPlaying || (fact != DemoRules.WaterPresent && fact != DemoRules.PlantAquatic && fact != DemoRules.WindowOcean && fact != DemoRules.Complete);
        }
        /// <summary>只监听海画事实；完整反应闭包已经在此之前由领域计算完毕。</summary>
        private void OnEvent(DomainEvent e)
        {
            if (e is FactAdded fact && fact.Fact == DemoRules.PaintingSea) PlayAnimated();
        }
        /// <summary>播放已经成立的海画解释，不在导演中补充缺少的前置状态。</summary>
        public void PlayAnimated()
        {
            if ((IsPlaying && !preview) || session == null || !session.State.HasFact(DemoRules.PaintingSea)) return;
            // 若玩家在调试预览中提交真实解释，取消预览并转交正式演出。
            if (preview) ResetPresentation();
            preview = false; finalShown = false; Begin();
        }
        /// <summary>开始协程前设置播放标志，确保同一轮后续事实不会立即表现。</summary>
        private void Begin()
        {
            startedAt = Time.unscaledTime; elapsed = 0; IsPlaying = true;
            CurrentBeat = "笔记关系形成";
            playing = StartCoroutine(Run());
        }
        /// <summary>相对开始时间等待，避免每次等待累积帧误差。</summary>
        private IEnumerator Until(float seconds)
        {
            while (Time.unscaledTime - startedAt < seconds) yield return null;
        }
        /// <summary>仅在规定节拍读已经成立的事实，并设置具体表现目标。</summary>
        private IEnumerator Run()
        {
            yield return Until(anomalyAt);
            CurrentBeat = "颜料微动"; painting.PlayAnomaly(); ending.PlaySeaLighting();
            yield return Until(seaAt);
            CurrentBeat = "画中海形成"; painting.PlaySeaTransition();
            yield return Until(waterAt);
            if (preview || session.State.HasFact(DemoRules.WaterPresent)) { CurrentBeat = "第一滴水"; water.PlayWaterReveal(); }
            yield return Until(plantAt);
            if (preview || session.State.HasFact(DemoRules.PlantAquatic)) { CurrentBeat = "植物自行水生化"; plant.PlayAquaticTransition(); }
            yield return Until(windowAt);
            if (preview || session.State.HasFact(DemoRules.WindowOcean)) { CurrentBeat = "远方变成海平线"; window.PlayOceanTransition(); }
            yield return Until(atmosphereAt);
            if (preview || session.State.HasFact(DemoRules.Complete)) { CurrentBeat = "房间氛围转变"; ending.PlayAtmosphere(); }
            yield return Until(finishAt);
            elapsed = Time.unscaledTime - startedAt; IsPlaying = false; finalShown = !preview; playing = null;
            CurrentBeat = preview ? "预览结束（领域未改变）" : "余韵";
        }
        /// <summary>只停止演出；调用方随后从当前或新会话恢复视觉。</summary>
        public void Cancel()
        {
            if (playing != null) StopCoroutine(playing);
            playing = null; IsPlaying = false; elapsed = 0;
        }
        /// <summary>跳过所有等待，直接应用当前领域事实；不新增任何事实。</summary>
        public void ApplyFinalStateImmediately()
        {
            Cancel();
            if (session == null) return;
            foreach (var view in presenters) view.ApplyState(session.State, false);
            finalShown = session.State.HasFact(DemoRules.PaintingSea); CurrentBeat = "即时恢复";
        }
        /// <summary>以下入口明确是表现预览；先建立可读的演出前视觉，不写 GameState。</summary>
        public void ResetPresentation()
        {
            Cancel(); preview = true;
            painting.SetFinalVisual(false); water.SetFinalVisual(false); plant.SetVisual(true, false, false);
            window.SetFinalVisual(true, false); ending.SetPresentation(true, false, false);
            CurrentBeat = "预览待命（领域未改变）";
        }
        /// <summary>调试完整演出，不执行配方也不授予碎片。</summary>
        public void PlayBenchmarkPreview() { ResetPresentation(); Begin(); }
        /// <summary>单独预览画。</summary>
        public void PlayPaintingPreview() { ResetPresentation(); painting.PlaySeaTransition(); ending.PlaySeaLighting(); CurrentBeat = "海画预览"; }
        /// <summary>单独预览水。</summary>
        public void PlayWaterPreview() { ResetPresentation(); painting.SetFinalVisual(true); water.PlayWaterReveal(); CurrentBeat = "滴水预览"; }
        /// <summary>单独预览植物性质变化。</summary>
        public void PlayPlantPreview() { ResetPresentation(); painting.SetFinalVisual(true); water.SetFinalVisual(true); plant.PlayAquaticTransition(); CurrentBeat = "水生植物预览"; }
        /// <summary>单独预览窗外连续变海。</summary>
        public void PlayWindowPreview() { ResetPresentation(); painting.SetFinalVisual(true); water.SetFinalVisual(true); plant.SetVisual(true, true, false); window.PlayOceanTransition(); CurrentBeat = "海窗预览"; }
        /// <summary>只查看最终视觉；与恢复真实领域状态的入口明确分开。</summary>
        public void ApplyFinalPreview()
        {
            Cancel(); preview = true; painting.SetFinalVisual(true); water.SetFinalVisual(true);
            plant.SetVisual(true, true, false); window.SetFinalVisual(true, true); ending.SetPresentation(true, true, true);
            CurrentBeat = "最终效果预览（领域未改变）";
        }
        /// <summary>销毁或换场景时取消协程，并释放会话事件监听。</summary>
        private void OnDestroy() { Cancel(); if (session != null) session.EventRaised -= OnEvent; }
    }
}
