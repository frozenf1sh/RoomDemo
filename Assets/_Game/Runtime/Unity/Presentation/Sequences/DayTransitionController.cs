// =============================================================================
// 文件：DayTransitionController.cs
// 职责：接受玩家的“结束今天”输入，提交日序命令，再以淡出、余音、淡入呈现后果。
// 边界：事实在命令提交时立即计算；黑屏仅延后视觉恢复，不决定延迟规则是否成立。
//       不建立时钟、精力或日历；重开和预设切换必须取消旧过渡。
// =============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Unity
{
    /// <summary>这个房间的跨日输入与过渡，和海画／星空世界演出分别管理。</summary>
    public sealed class DayTransitionController : MonoBehaviour
    {
        [SerializeField] private Button endDayButton;
        [SerializeField] private Text dayLabel, transitionLabel;
        [SerializeField] private GameObject curtain;
        [SerializeField] private CanvasGroup darkness;
        [SerializeField] private AudioSource remainingSound;
        private GameSession session;
        private NotebookView notebook;
        private Action restore;
        private Coroutine running;
        private readonly List<AudioSource> loops = new List<AudioSource>();
        private readonly List<float> volumes = new List<float>();
        public bool IsTransitioning { get; private set; }
        public void Configure(Button button, Text day, Text nextDay, GameObject panel, CanvasGroup fade, AudioSource rustle)
        { endDayButton = button; dayLabel = day; transitionLabel = nextDay; curtain = panel; darkness = fade; remainingSound = rustle; }
        public void Bind(GameSession game, NotebookView book, Action restoreVisuals)
        {
            Cancel(); if (session != null) session.EventRaised -= OnEvent;
            session = game; notebook = book; restore = restoreVisuals; session.EventRaised += OnEvent;
            endDayButton.onClick.RemoveAllListeners(); endDayButton.onClick.AddListener(Begin); Refresh();
        }
        /// <summary>先锁定展示，立即提交领域命令；到黑屏时才应用它已算出的最终视觉。</summary>
        public void Begin()
        {
            if (session == null || !session.CanAdvanceDay || notebook.ExternalInputBlocked || IsTransitioning) return;
            notebook.SetOpen(false); notebook.ExternalInputBlocked = true; IsTransitioning = true;
            // 此标记先于命令事件，启动组件据此过滤即时动画，避免书在淡出前抢先落下。
            session.AdvanceDay();
            foreach (var source in transform.root.GetComponentsInChildren<AudioSource>(true))
                if (source != remainingSound && source.loop) { loops.Add(source); volumes.Add(source.volume); }
            running = StartCoroutine(Transition()); Refresh();
        }
        private IEnumerator Transition()
        {
            curtain.SetActive(true); transitionLabel.text = "";
            float elapsed = 0;
            while (elapsed < .65f)
            {
                elapsed += Time.unscaledDeltaTime; darkness.alpha = Mathf.Clamp01(elapsed / .65f);
                for (int i = 0; i < loops.Count; i++) if (loops[i] != null) loops[i].volume = volumes[i] * (1 - darkness.alpha);
                yield return null;
            }
            darkness.alpha = 1; restore(); remainingSound.Play();
            transitionLabel.text = "第 " + session.State.DayIndex + " 天";
            yield return new WaitForSecondsRealtime(1.3f);
            elapsed = 0;
            while (elapsed < .85f)
            {
                elapsed += Time.unscaledDeltaTime; darkness.alpha = 1 - Mathf.Clamp01(elapsed / .85f);
                for (int i = 0; i < loops.Count; i++) if (loops[i] != null) loops[i].volume = volumes[i] * (1 - darkness.alpha);
                yield return null;
            }
            ResetTransition(); running = null; Refresh();
        }
        /// <summary>取消协程并归还借用音量；不回滚已经成立的次日事实。</summary>
        public void Cancel()
        {
            if (running != null) StopCoroutine(running); running = null; ResetTransition();
        }
        private void ResetTransition()
        {
            IsTransitioning = false; if (notebook != null) notebook.ExternalInputBlocked = false;
            curtain.SetActive(false); darkness.alpha = 0; remainingSound.Stop();
            for (int i = 0; i < loops.Count; i++) if (loops[i] != null) loops[i].volume = volumes[i];
            loops.Clear(); volumes.Clear();
        }
        private void OnEvent(DomainEvent _) { Refresh(); }
        private void Refresh()
        {
            dayLabel.text = "第 " + session.State.DayIndex + " 天";
            endDayButton.gameObject.SetActive(session.CanAdvanceDay && !IsTransitioning);
            endDayButton.interactable = !notebook.ExternalInputBlocked && !notebook.IsOpen;
        }
        private void Update()
        {
            // 只更新输入许可，绝不在 Update 中推进日序或检测世界反应。
            if (session != null && endDayButton.gameObject.activeSelf) endDayButton.interactable = !notebook.ExternalInputBlocked && !notebook.IsOpen;
        }
        private void OnDestroy() { Cancel(); if (session != null) session.EventRaised -= OnEvent; }
    }
}
