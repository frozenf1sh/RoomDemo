// =============================================================================
// 文件：NarrationSystem.cs
// 职责：消费内容事件，播放极简短句、停顿标记、轻打字声和容量有限的旁白队列。
// 边界：纯表现消费者；计数、收束和结局由领域决定，播放器不反向发命令。
// =============================================================================
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Unity
{
    /// <summary>单声部短旁白。重开会取消待播请求并重置一次性记录。</summary>
    public sealed class NarrationSystem : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private CanvasGroup opacity;
        [SerializeField] private ContentFeedback sounds;
        [SerializeField] private NarrationDefinition[] definitions;
        private GameSession session;
        private readonly List<NarrationDefinition> queue = new List<NarrationDefinition>();
        private readonly HashSet<string> seen = new HashSet<string>();
        private Coroutine playing;
        private int currentPriority = -1;
        /// <summary>当前只是旁白是否可见，不作为游戏条件。</summary>
        public bool IsShowing => playing != null;
        /// <summary>显式绑定文本、声音 Cue 与内容表，不查找全局服务。</summary>
        public void Configure(Text text, CanvasGroup group, ContentFeedback cues, NarrationDefinition[] content)
        { label = text; opacity = group; sounds = cues; definitions = content; }
        /// <summary>切换会话时取消旧字幕和所有延时；只在正式新游戏播放开场。</summary>
        public void Bind(GameSession game)
        {
            if (session != null) session.EventRaised -= OnEvent;
            StopAllCoroutines();
            playing = null;
            currentPriority = -1;
            seen.Clear();
            queue.Clear();
            label.text = "";
            opacity.alpha = 0;
            session = game;
            session.EventRaised += OnEvent;
            if (game.State.CompletedRecipes.Count == 0 && game.State.Facts.Count == 0)
                Trigger(NarrationTrigger.Start, "");
        }
        private void OnEvent(DomainEvent value)
        {
            if (value is CombinationSucceeded recipe) Trigger(NarrationTrigger.Recipe, recipe.RecipeId);
            if (value is FactAdded fact) Trigger(NarrationTrigger.Fact, fact.Fact.Value);
            if (value is ClueDiscovered clue) Trigger(NarrationTrigger.Clue, clue.Clue.Value);
        }
        private void Trigger(NarrationTrigger trigger, string id)
        {
            foreach (var definition in definitions.Where(x => x.Trigger == trigger && x.TriggerId == id))
            {
                var state = session.State;
                if (state.DeviationCount < definition.MinimumDeviations || state.DeviationCount > definition.MaximumDeviations
                    || (definition.RequireLockIn && !state.CanvasLocked)) continue;
                if (definition.OnceOnly && !seen.Add(definition.Id)) continue;
                StartCoroutine(Delayed(definition));
            }
        }
        private IEnumerator Delayed(NarrationDefinition definition)
        {
            yield return new WaitForSecondsRealtime(definition.Delay);
            Enqueue(definition);
        }
        /// <summary>按优先级插入短请求；最多八条，避免旁白挤满安静观察时间。</summary>
        public void Enqueue(NarrationDefinition definition)
        {
            bool interrupt = definition.InterruptPolicy == NarrationInterruptPolicy.ReplaceAll
                || (definition.InterruptPolicy == NarrationInterruptPolicy.ReplaceLowerPriority && definition.Priority > currentPriority);
            if (interrupt)
            {
                if (playing != null) StopCoroutine(playing);
                playing = null;
                queue.Clear();
            }
            if (queue.Count >= 8) queue.RemoveAt(queue.Count - 1);
            int at = queue.FindIndex(x => x.Priority < definition.Priority);
            queue.Insert(at < 0 ? queue.Count : at, definition);
            if (playing == null) playing = StartCoroutine(Play());
        }
        private IEnumerator Play()
        {
            while (queue.Count > 0)
            {
                var definition = queue[0];
                queue.RemoveAt(0);
                currentPriority = definition.Priority;
                label.text = "";
                opacity.alpha = 1;
                int audible = 0;
                for (int i = 0; i < definition.Text.Length; i++)
                {
                    // 停顿标记不显示，使用固定文化解析小数，不受开发机语言影响。
                    if (definition.Text[i] == '[' && definition.Text.Substring(i).StartsWith("[pause="))
                    {
                        int end = definition.Text.IndexOf(']', i);
                        if (end >= 0 && float.TryParse(definition.Text.Substring(i + 7, end - i - 7), NumberStyles.Float, CultureInfo.InvariantCulture, out float pause))
                        { i = end; yield return new WaitForSecondsRealtime(Mathf.Clamp(pause, 0, 10)); continue; }
                    }
                    char character = definition.Text[i];
                    label.text += character;
                    if (!char.IsWhiteSpace(character) && !char.IsPunctuation(character) && ++audible % 2 == 0)
                        sounds.PlayCue(definition.AudioBeep);
                    yield return new WaitForSecondsRealtime(Mathf.Max(.02f, definition.CharacterInterval));
                }
                yield return new WaitForSecondsRealtime(definition.Pause);
                for (float t = 0; t < .7f; t += Time.unscaledDeltaTime)
                { opacity.alpha = 1 - t / .7f; yield return null; }
                opacity.alpha = 0;
                label.text = "";
            }
            currentPriority = -1;
            playing = null;
        }
        private void OnDestroy()
        {
            // 会话可能比组件更长寿，必须解除事件引用；协程由组件销毁自动取消。
            if (session != null) session.EventRaised -= OnEvent;
        }
    }
}
