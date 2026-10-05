// =============================================================================
// 文件：CanvasEndingSequence.cs
// 职责：连续服从后收窄注意力，并在作品完成后安静地淡出。
// 边界：结局事实由领域决定；此组件只拥有画架灯、叠层和临时输入锁。
// =============================================================================
using System.Collections;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Unity
{
    /// <summary>普通完成没有魔法高潮，也不评价玩家；重开必须归还所有视觉资源。</summary>
    public sealed class CanvasEndingSequence : MonoBehaviour
    {
        [SerializeField] private Light canvasLight;
        [SerializeField] private CanvasGroup fade;
        [SerializeField] private Text endingWord;
        [SerializeField] private NotebookView notebook;
        private GameSession session;
        private float initialLight, emphasis;
        private Coroutine ending;
        /// <summary>仅表示淡出已经展现，领域完成条件由独立事实表达。</summary>
        public bool IsFinished { get; private set; }
        /// <summary>绑定画架灯与极简完成叠层。</summary>
        public void Configure(Light focalLight, CanvasGroup blackout, Text word, NotebookView notes)
        { canvasLight = focalLight; fade = blackout; endingWord = word; notebook = notes; }
        private void Awake() { initialLight = canvasLight.intensity; }
        /// <summary>替换会话时先清理旧淡出与输入锁，不把状态遗留给新游戏。</summary>
        public void Bind(GameSession game)
        {
            Cancel();
            if (session != null) session.EventRaised -= OnEvent;
            session = game;
            session.EventRaised += OnEvent;
            emphasis = game.State.CanvasLocked ? 1 : 0;
            if (game.State.HasFact(NarrativeRules.BanalComplete))
            { fade.alpha = 1; endingWord.text = "完成。"; IsFinished = true; }
        }
        private void OnEvent(DomainEvent value)
        {
            if (value is FactAdded fact && fact.Fact == NarrativeRules.BanalComplete && ending == null)
                ending = StartCoroutine(Finish());
        }
        private void Update()
        {
            if (session == null) return;
            // 收束只加强原来画架的暖光，不突然熄灭整个世界或撤销玩家所得线索。
            emphasis = Mathf.MoveTowards(emphasis, session.State.CanvasLocked ? 1 : 0, Time.deltaTime / 5);
            canvasLight.intensity = initialLight * (1 + .35f * emphasis);
        }
        private IEnumerator Finish()
        {
            // 给玩家看完成的画，等克制的旁白说完；此处延迟的是视觉，不是结局事实。
            yield return new WaitForSecondsRealtime(13);
            notebook.SetOpen(false);
            notebook.ExternalInputBlocked = true;
            for (float t = 0; t < 4; t += Time.unscaledDeltaTime)
            { fade.alpha = Mathf.SmoothStep(0, 1, t / 4); yield return null; }
            fade.alpha = 1;
            endingWord.text = "完成。";
            IsFinished = true;
            ending = null;
        }
        /// <summary>重开或历史回归取消旧协程，释放本组件拥有的临时输入锁。</summary>
        public void Cancel()
        {
            if (ending != null) StopCoroutine(ending);
            ending = null;
            IsFinished = false;
            if (fade != null) fade.alpha = 0;
            if (endingWord != null) endingWord.text = "";
            if (notebook != null) notebook.ExternalInputBlocked = false;
            if (canvasLight != null) canvasLight.intensity = initialLight;
        }
        private void OnDestroy() { Cancel(); if (session != null) session.EventRaised -= OnEvent; }
    }
}
