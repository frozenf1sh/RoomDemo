// 文件：AtelierFlowNarration.cs
// 职责：按原稿采用文本播放首批旁白，在完整句边界处理更高优先级的新阶段。
// 边界：只更新文字与播放生命周期；不发放事实，开场输入锁由场景订阅完成事件管理。
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Unity.ArtRebuild
{
    /// <summary>首批使用现有字幕节点；UI同学后续替换布局，触发条件仍由会话决定。</summary>
    public sealed class AtelierFlowNarration : MonoBehaviour
    {
        /// <summary>原稿采用文本与优先级集中定义，不散落在各个物件Presenter。</summary>
        private sealed class Line
        {
            public readonly string Key, Text;
            public readonly int Priority;
            public Line(string key, int priority, string text)
            {
                Key = key;
                Priority = priority;
                Text = text;
            }
        }

        public Text Label;
        public CanvasGroup Opacity;
        /// <summary>整组原稿台词实际显示完毕后通知；声音与正式字幕布局由UI后续替换。</summary>
        public event System.Action<string> LineCompleted;
        private readonly Queue<Line> queue = new Queue<Line>();
        private readonly HashSet<string> spoken = new HashSet<string>();
        private Coroutine playback;
        /// <summary>正在播放的稳定键，供运行取证和后续字幕接入读取。</summary>
        public string ActiveKey { get; private set; } = "";
        private static readonly Dictionary<string, Line> Lines = new Dictionary<string, Line> {
            { "G_KNIFE_DRY",new Line("G_KNIFE_DRY",4,"先刮开这块干颜料。") },
            { "G_KNIFE_HOLD",new Line("G_KNIFE_HOLD",4,"文森特拿起了刮刀，他想干什么？") },
            { "G_ROOM_GONE",new Line("G_ROOM_GONE",9,"……文森特，你在干什么？") },
            { "G_KNIFE_WET",new Line("G_KNIFE_WET",4,"底下还没干透。用刀把这块湿颜料推开试试。") },
            { "G_WINDOW_NEAR",new Line("G_WINDOW_NEAR",4,"看看近处那扇亮着灯的窗。") },
            { "G_WINDOW_FAR",new Line("G_WINDOW_FAR",4,"再看看远处的窗灯。") },
            { "G_WINDOW_BOTH",new Line("G_WINDOW_BOTH",4,"对了，这一带一直不大热闹，离城市很远，也没有星星。但有时候夜里亮起的窗灯也可以是星星吧？") },
            { "G_LAMP",new Line("G_LAMP",5,"夜深了。看看左边的灯，把它点亮吧。") },
            { "G_MATCH",new Line("G_MATCH",5,"右下角有一盒火柴，先拿一根。") },
            { "G_REST",new Line("G_REST",6,"你真的该休息了，快画完吧。") },
            { "G_ROOF_WARN",new Line("G_ROOF_WARN",7,"别再刮了。回去把画画完。") },
            { "G_ROOM_ANGRY",new Line("G_ROOM_ANGRY",8,"够了！别再碰这个房间。回到画布前，把画画完！") },
            { "V_ROOF",new Line("V_ROOF",0,"文森特：天花板看起来有点像干颜料……") },
            { "V_WET",new Line("V_WET",0,"文森特：湿颜料掉在地上了，看起来可以用刀推开……") },
            { "V_REFUSE",new Line("V_REFUSE",0,"文森特：我不想再画那幅画了。") },
            { "V_CLOUD",new Line("V_CLOUD",0,"文森特：画点云吧。点一下天空，落一笔。") },
            { "V_STAR",new Line("V_STAR",0,"文森特：先添一点星光。") },
            { "N01", new Line("N01", 1, "你好，文森特，你还好吗？你已经很久没有呼唤我了。视线里空无一物，不是吗？好吧，我能来帮帮你。试着回想起一些物件——一些你从儿时就熟悉的东西。看到了吗？它们就在那里。不要熟视无睹。拿起它们。回想起它们。获得启发。") },
            { "N02", new Line("N02", 2, "有点想法了？太好了。 我建议你只是把它画下来，只是这样就好。") },
            { "N03", new Line("N03", 3, "感觉好些了，还是感觉不确定？那些都不重要了。放松一些了吧，思绪更自由了吧……也许凭借着这样的状态，你可以看见更多事物。但看见更多，就意味着更加自由吗？") },
            { "N04", new Line("N04", 4, "好极了。 我亲眼见证你最有热情的时光。最为狂热，最为锋利，最善于挑战。 热情能够尽量地延伸是一件好事。只是，它真的存在意义吗？") },
            { "N05", new Line("N05", 5, "这一切本是没有意义的。 而这一切本是你的。 你的笔触是你，你的颜色是你，我也是你。 不断地在虚空中坠落，不断地想要抓住些什么。 连挣扎和呼喊都要记录，都要归结成热情。 这就是你，以及所有艺术家所要踏入的同一条河流……") },
            { "N06", new Line("N06",6,"……星之河。 在夜的最终与虚无的最终，只有你自己能够重新点亮大地。") },
            { "N07", new Line("N07",7,"也只有你自己能够让自己不再困顿与迷茫。 将这片启示涂满世界的每个角落。") }
        };

        /// <summary>主角想法与功能文字同表维护，显示在独立世界字幕节点。</summary>
        public static string TextFor(string key)=>Lines.TryGetValue(key,out var line)?line.Text:"";
        /// <summary>只接受已定义台词；新进度替换过时的等待项，当前句播完后再切换。</summary>
        /// <param name="key">09号文档中的稳定台词键。</param>
        public void Play(string key)
        {
            if (!Lines.TryGetValue(key, out var line) || !spoken.Add(key)) return;
            // 正常进度只会前进；较快玩家不必等完整开场组和旧教学组才听到阶段反馈。
            if (queue.Count > 0 && queue.Peek().Priority < line.Priority) queue.Clear();
            queue.Enqueue(line);
            if (playback == null) playback = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Opacity.alpha = 1;
            while (queue.Count > 0)
            {
                var line = queue.Dequeue();
                ActiveKey = line.Key;
                bool completed = true;
                foreach (string sentence in Regex.Split(line.Text, @"(?<=[。！？])"))
                {
                    if (string.IsNullOrWhiteSpace(sentence)) continue;
                    Label.text = "";
                    foreach (char letter in sentence.Trim())
                    {
                        Label.text += letter;
                        yield return new WaitForSeconds(.045f);
                    }
                    yield return new WaitForSeconds(1.6f);
                    // 不在逗号、称呼或半句话中硬切，事实已经推进时也不继续播旧引导。
                    if (queue.Count > 0 && queue.Peek().Priority > line.Priority)
                    {
                        completed = false;
                        break;
                    }
                }
                if (completed) LineCompleted?.Invoke(line.Key);
            }
            Label.text = "";
            Opacity.alpha = 0;
            ActiveKey = "";
            playback = null;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            queue.Clear();
            playback = null;
            ActiveKey = "";
            if (Label != null) Label.text = "";
            if (Opacity != null) Opacity.alpha = 0;
        }
    }
}
