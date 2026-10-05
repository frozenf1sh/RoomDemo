// =============================================================================
// 文件：OneLineSubtitle.cs
// 职责：在中下方依次淡入一行字幕，短暂停留后淡出。
// 边界：只接收文本，不认识物体、事实或配方，不构建对话系统。
// =============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Unity
{
    /// <summary>容量受限的单行字幕，重开时取消协程并清空待播文本。</summary>
    public sealed class OneLineSubtitle : MonoBehaviour
    {
        [SerializeField] private Text line;
        [SerializeField] private CanvasGroup opacity;
        private readonly Queue<string> queue = new Queue<string>();
        private Coroutine playing;
        public void Configure(Text label, CanvasGroup group) { line=label; opacity=group; }
        /// <summary>排入一条短字幕；只保留最近四条，空文本被忽略。</summary>
        public void Show(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if(queue.Count>=4) queue.Dequeue(); queue.Enqueue(text);
            if(playing==null) playing=StartCoroutine(Play());
        }
        private IEnumerator Play()
        {
            // 每条字幕独立计时，不因后来的事件覆写正在阅读的文字。
            while(queue.Count>0)
            {
                line.text=queue.Dequeue(); float start=Time.unscaledTime;
                while(Time.unscaledTime-start<3.1f)
                {
                    float t=Time.unscaledTime-start;
                    opacity.alpha=Mathf.Min(Mathf.Clamp01(t/.4f),Mathf.Clamp01((3.1f-t)/.6f));
                    yield return null;
                }
            }
            opacity.alpha=0; line.text=""; playing=null;
        }
        /// <summary>取消旧局字幕；领域恢复不会重复播放解释台词。</summary>
        public void Clear()
        { if(playing!=null) StopCoroutine(playing); playing=null; queue.Clear(); opacity.alpha=0; line.text=""; }
    }
}
