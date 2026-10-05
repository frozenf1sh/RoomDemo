// =============================================================================
// 文件：NarrationDefinition.cs
// 职责：定义短旁白的触发、打字、停顿、声音和打断策略。
// 边界：这是表现数据，不能授予线索、改变路线计数或添加事实。
// =============================================================================
using System;
namespace Game.Unity
{
    /// <summary>播放请求如何处理已有旁白；世界字幕使用另一显示节点。</summary>
    public enum NarrationInterruptPolicy { Queue, ReplaceLowerPriority, ReplaceAll }
    /// <summary>来自正式会话的少量事件身份，不引入剧情分支图。</summary>
    public enum NarrationTrigger { Start, Recipe, Fact, Clue }
    /// <summary>策划可以在文本中用 [pause=秒数] 指定安静停顿。</summary>
    [Serializable]
    public sealed class NarrationDefinition
    {
        /// <summary>用于一次性去重的稳定旁白身份。</summary>
        public string Id;
        /// <summary>简体中文短句，可包含不显示的 [pause=秒数] 标记。</summary>
        public string Text;
        /// <summary>配方、事实或线索的稳定 ID；开场为空。</summary>
        public string TriggerId;
        /// <summary>中央声音表中的轻打字声 Cue，资源不散落在播放器。</summary>
        public string AudioBeep = "narration_type";
        /// <summary>消费哪一种已有会话事件；没有剧情分支树。</summary>
        public NarrationTrigger Trigger;
        /// <summary>逐字间隔秒数；用户设置很小时仍保持有限节奏。</summary>
        public float CharacterInterval = .085f;
        /// <summary>句子播放完后的停留秒数。</summary>
        public float Pause = 1.8f;
        /// <summary>收到事件到进入队列的表现等待，不延迟领域事实。</summary>
        public float Delay;
        /// <summary>短队列中的播放优先级，不是配方优先级。</summary>
        public int Priority;
        /// <summary>可播放时已成立的偏离次数范围，仅作内容过滤。</summary>
        public int MinimumDeviations, MaximumDeviations = 100;
        /// <summary>同一局只播放一次，重开时清除。</summary>
        public bool OnceOnly = true;
        /// <summary>只在领域已收束画布路线时播放，不在旁白里决定锁定。</summary>
        public bool RequireLockIn;
        /// <summary>如何处理当前短句与排队短句。</summary>
        public NarrationInterruptPolicy InterruptPolicy = NarrationInterruptPolicy.Queue;
        /// <summary>构造短句内容；所有时序与触发条件都可以在此附近修改。</summary>
        public NarrationDefinition(string id, NarrationTrigger trigger, string key, string text, float delay = 0, int priority = 0)
        {
            Id = id;
            Trigger = trigger;
            TriggerId = key;
            Text = text;
            Delay = delay;
            Priority = priority;
        }
    }
    /// <summary>克制的旁白内容表。没有路线名、善恶评价或对玩家的嘲笑。</summary>
    public static class NarrationContent
    {
        /// <summary>旁白说“应该怎样”，世界字幕仍然说“发生了什么”。</summary>
        public static NarrationDefinition[] Create()
        {
            // 中间的自定义停顿既不写入世界状态，也不需要修改播放器代码。
            return new[] {
                new NarrationDefinition("intro", NarrationTrigger.Start, "", "展览还有三天。[pause=1.1]先完成那幅画吧。", 4),
                new NarrationDefinition("growth_idea", NarrationTrigger.Recipe, "recipe.insight.growth", "很好。[pause=0.6]把它留给画布。", 2),
                new NarrationDefinition("growth_outside", NarrationTrigger.Recipe, "recipe.plant.growth", "……[pause=1.0]那不是画布。", 4.5f, 1),
                new NarrationDefinition("growth_inside", NarrationTrigger.Recipe, "recipe.painting.growth", "这样就好。", 4.2f),
                new NarrationDefinition("distance", NarrationTrigger.Clue, "far", "背景还需要一点距离。", 1.6f),
                new NarrationDefinition("window_outside", NarrationTrigger.Recipe, "recipe.window.far", "你不需要改变窗户。[pause=0.8]把画完成就好。", 4.3f, 1),
                new NarrationDefinition("sea_idea", NarrationTrigger.Clue, "sea", "再添一点海。", 1.8f),
                new NarrationDefinition("sea_inside", NarrationTrigger.Recipe, "recipe.painting.sea", "对。", .9f),
                new NarrationDefinition("sea_leak", NarrationTrigger.Fact, "water.present", "……[pause=1.0]等等。", 5.8f, 2),
                new NarrationDefinition("lock_in", NarrationTrigger.Recipe, "recipe.painting.sea", "很好。[pause=0.8]已经足够了。[pause=1.0]把最后一部分补完吧。", 3.8f, 2) { RequireLockIn = true, MaximumDeviations = 0 },
                new NarrationDefinition("star_idea", NarrationTrigger.Recipe, "recipe.insight.starry", "只差天空了。", 2),
                new NarrationDefinition("roof_outside", NarrationTrigger.Recipe, "recipe.ceiling.starry", "……[pause=1.2]你在做什么？[pause=1.0]屋顶不是画布。", 4.5f, 3) { InterruptPolicy = NarrationInterruptPolicy.ReplaceLowerPriority },
                new NarrationDefinition("room_seen", NarrationTrigger.Clue, "room", "房间不是——", 1.2f, 4) { InterruptPolicy = NarrationInterruptPolicy.ReplaceAll, Pause = 1.2f },
                new NarrationDefinition("completed", NarrationTrigger.Fact, "ending.canvas_complete", "很好。[pause=2.0]这样就完成了。", 3.5f, 5) { InterruptPolicy = NarrationInterruptPolicy.ReplaceAll }
            };
        }
    }
}
