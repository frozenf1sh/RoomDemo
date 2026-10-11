// 文件：AtelierNarration.cs
// 职责：集中维护新画室第一阶段短旁白、停顿与拉回当前任务的分支。
// 边界：文本只反映已经发生的输入和状态，不透露现实配方，不决定世界事实。
using Game.Domain;

namespace Game.Unity.ArtRebuild
{
    /// <summary>沿用旧主流程短句的语气，越界时先让玩家看见结果。</summary>
    public static class AtelierNarration
    {
        /// <summary>从实际触发身份生成本阶段内容；旁白播放器负责时序与中断。</summary>
        public static NarrationDefinition Line(string key, GameState state)
        {
            string text;
            switch (key)
            {
                case "start": text = "桌上留了张便条。先看看吧。"; break;
                case "manuscript": text = "明早就要交了。[pause=.8]那就画眼前这些。杯子，笔，灯，再带一点窗外的夜色。"; break;
                case "brush_needed": text = "先看看杯边那支笔。蓝色还够用。"; break;
                case "sketch_first": text = "对，就从这里开始。"; break;
                case "still_life": text = "东西都在了。[pause=1.0]可这水，看着像块玻璃。[pause=1.0]洗洗笔，换点颜色试试。"; break;
                case "water_seen": text = "笔就搭在杯沿上。拿它蘸一下水。"; break;
                case "spread_seen": text = "看，蓝色散开了。[pause=.8]刚才水面还那么平。"; break;
                case "book_open": text = "把水面前后的样子放在一起，记一下。"; break;
                case "flow_learned": text = state.HasFact(AtelierRules.CupSketched) ? "刚才那圈水纹，把它画进去。" : "先把杯子画上去。水纹等会儿再添。"; break;
                case "water_painted": text = "对，就是这样。[pause=.8]这才像水。"; break;
                case "outside_first":
                    text = "……你在干什么？[pause=1.2]那不是画布。[pause=1.0]";
                    text += !state.HasFact(StudioRules.PictureFlow) ? "先把画里的水画好。" : "回到画上来。";
                    break;
                default: text = "回到画上来。"; break;
            }
            // 这里只生成表现请求；没有播放回调可以添加状态或奖励。
            return new NarrationDefinition("atelier." + key, NarrationTrigger.Start, "", text, priority: key.StartsWith("outside") ? 3 : 0)
            {
                CharacterInterval = .085f, Pause = 2.3f, AudioBeep = "",
                InterruptPolicy = NarrationInterruptPolicy.ReplaceAll
            };
        }
    }
}
