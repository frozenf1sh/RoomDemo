// =============================================================================
// 文件：StudioNarration.cs
// 职责：集中维护画作教程、观察说明与越界后的短旁白。
// 边界：文字只描述输入或回应现象，不授予物体和事实。
// =============================================================================
using System.Linq;
using Game.Domain;
namespace Game.Unity
{
    /// <summary>旁白坚持完成画作；现实路线只由场景现象引出。</summary>
    public static class StudioNarration
    {
        /// <summary>纸页下方的观察说明，不作为旁白播放。</summary>
        public static string Description(ClueId clue, RuleSet rules)
        {
            if (clue == StudioRules.Flow) return "静止的水纹开始流动。";
            if (clue == StudioRules.Light) return "熄灭的地方有了光。";
            if (clue == StudioRules.Open) return "遮挡移开，露出后面的空间。";
            var item = rules.Discoveries.FirstOrDefault(x => x.Grants.Contains(clue));
            if (item != null) return item.Description;
            var observation = rules.Observations.FirstOrDefault(x => x.Grants.Contains(clue));
            return observation != null ? observation.Description : "";
        }
        /// <summary>独立世界字幕仅报告短小物理现象。</summary>
        public static string WorldCaption(FactId fact)
        {
            if (fact == StudioRules.PaintPeeled) return "窗框边的旧漆剥落了。";
            if (fact == StudioRules.CurtainReleased) return "窗帘拉开了。";
            return null;
        }
        /// <summary>教程由实际操作选择这一条短句；确认文字不会推进步骤。</summary>
        public static NarrationDefinition Tutorial(string key)
        {
            string text;
            switch (key)
            {
                case "intro": text="这幅画该交稿了。[pause=.7]先看看画里的水。"; break;
                case "cup": text="看看洗笔杯里的水。"; break;
                case "pigment": text="再看看水里的颜料。"; break;
                case "notebook": text="按 N，打开笔记本。"; break;
                case "read": text="点一下‘杯里的水’。"; break;
                case "drag": text="把‘杯里的水’和‘散开的颜料’拖到右页。"; break;
                case "submit": text="试着解释一下。"; break;
                case "close_flow": text="很好。先合上笔记本。"; break;
                case "water_again": text="点一下画里的水。"; break;
                case "paint_water": text="把‘流动’和‘画里的水’放到一起。"; break;
                case "close_water": text="先合上笔记本，看看水面。"; break;
                case "lamp": text="水面有了。接下来，把画里的灯画亮。[pause=.5]看看台灯。"; break;
                case "switch": text="按下灯座上的开关。"; break;
                case "light": text="先放着。把灯芯亮起前后的样子记在一起。"; break;
                case "picture_lamp": text="现在，点一下画里的灯。"; break;
                case "paint_lamp": text="用‘点亮’完成画里的灯。"; break;
                case "close_lamp": text="合上笔记本，看看那盏灯。"; break;
                case "curtain": text="还差那扇窗。拉开窗帘，看看外面。"; break;
                case "open": text="把窗帘拉开前后的样子放在一起。"; break;
                case "picture_window": text="点一下画里的窗。"; break;
                default: text="把画里的窗也打开。"; break;
            }
            var line=Line("tutorial."+key,NarrationTrigger.Start,"",text,0,10);
            line.InterruptPolicy=NarrationInterruptPolicy.ReplaceAll;
            return line;
        }
        /// <summary>越界已经发生后才表达阻力，不提前透露现实配方。</summary>
        public static NarrationDefinition[] Create() => new[] {
            First("water_flow"), First("water_light"), First("shade_light"), First("window_open"),
            Line("paint_late",NarrationTrigger.Fact,StudioRules.PictureOpen.Value,"画已经完成了。停笔吧。",1,30,1),
            Line("return",NarrationTrigger.Fact,StudioRules.WaterCarriesLight.Value,"回到画上来。",2,30,1),
            Line("skylight",NarrationTrigger.Fact,StudioRules.SkylightOpen.Value,"我们要交的是那幅画。",1,30,1),
            Line("sky",NarrationTrigger.Fact,StudioRules.FullSky.Value,"这已经不是原来的构图了。",2,40,1),
            Line("boundary",NarrationTrigger.Fact,StudioRules.BoundaryConnected.Value,"别动墙面。",3,45,1),
            Line("ordinary",NarrationTrigger.Fact,StudioRules.BanalComplete.Value,"好了，可以交了。",1,60),
            Line("room",NarrationTrigger.Fact,StudioRules.RoomOpened.Value,"……[pause=1]房间不能这样打开。",2,55),
            Line("final",NarrationTrigger.Fact,StudioRules.WorldComplete.Value,"……[pause=1]",1,60)
        };
        private static NarrationDefinition First(string recipe) => new NarrationDefinition("studio.first_boundary",NarrationTrigger.Recipe,"studio.meaning."+recipe,"……[pause=.9]那不是画布。",1.5f,50)
        { MinimumDeviations=1, MaximumDeviations=1, CharacterInterval=.065f, InterruptPolicy=NarrationInterruptPolicy.ReplaceAll };
        private static NarrationDefinition Line(string id,NarrationTrigger trigger,string key,string text,float delay,int priority,int minimum=0)
            => new NarrationDefinition("studio."+id,trigger,key,text,delay,priority)
            { MinimumDeviations=minimum, CharacterInterval=.055f, InterruptPolicy=(id=="ordinary" || id=="final")?NarrationInterruptPolicy.ReplaceAll:NarrationInterruptPolicy.Queue };
    }
}
