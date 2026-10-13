// =============================================================================
// 文件：AtelierRules.cs
// 职责：新夜间画室的观察比较、画作交稿与现实擦除路线内容。
// 边界：只定义有限规则；不依赖镜头、材质、动画时长或旁白是否播完。
// =============================================================================
using System;
using System.Collections.Generic;

namespace Game.Domain
{
    /// <summary>在既有稳定物体身份上装载新画室内容，不恢复归档场景。</summary>
    public static class AtelierRules
    {
        /// <summary>新场景新增的真实来源；原有杯、灯、窗帘和画中区域沿用稳定身份。</summary>
        public static readonly ObjectId Painting = new ObjectId("atelier.painting"),
            WetPaper = new ObjectId("atelier.wet_paper"), Trial = new ObjectId("atelier.trial"),
            SharedMark = new ObjectId("atelier.shared_mark"), Brush = new ObjectId("atelier.brush"),
            Manuscript = new ObjectId("atelier.manuscript"), StillLamp = new ObjectId("atelier.still_lamp"),
            Matches = new ObjectId("atelier.matches");
        /// <summary>记录玩家实际划过火柴的历史，不代表火柴永远燃烧。</summary>
        public static readonly FactId MatchStruck = F("match_struck");
        /// <summary>擦除也必须通过亲手擦拭前后的观察形成。</summary>
        public static readonly ClueId Erase = new ClueId("atelier.erase");
        /// <summary>准备、现实变化与主动交稿各有独立事实。</summary>
        public static readonly FactId CupSketched = F("cup_sketched"), BrushSketched = F("brush_sketched"),
            LampSketched = F("lamp_sketched"), WindowSketched = F("window_sketched"), StillLifePainted = F("still_life_painted");
        /// <summary>后续解释和交稿事实，不由普通静物落笔计数。</summary>
        public static readonly FactId TrialWiped = F("trial_wiped"), PaperWet = F("paper_wet"),
            PaperFlow = F("paper_flow"), FrameErased = F("frame_erased"), RoofErased = F("roof_erased"),
            ReadyToDeliver = F("ready_to_deliver"), CanvasDelivered = F("canvas_delivered"),
            RealityTouched = F("reality_touched"), SharedMarkSeen = F("shared_mark_seen"),
            WholeObserved = F("whole_observed"), RoomErased = F("room_erased");

        /// <summary>构造新内容事实身份，避免与归档路线混用。</summary>
        public static FactId F(string value) => new FactId("atelier." + value);
        /// <summary>观察身份可用于界面展示；不含配方判定。</summary>
        public static ClueId Observation(string value) => new ClueId("atelier.obs." + value);

        /// <summary>创建完整有限内容；表现阶段是否已验收不由领域猜测。</summary>
        public static RuleSet Create()
        {
            var clues = new List<ClueDefinition>();
            var objects = new List<DiscoveryRule>();
            var observations = new List<ObservationDiscovery>();
            var recipes = new List<SemanticRecipe>();
            var reactions = new List<ReactionRule>();
            var actions = new List<SceneActionRule>();
            var logics = new List<LogicDefinition>();
            var none = Array.Empty<FactId>();
            void Object(ObjectId id, string label, string description, FactId[] requires = null, bool quiet = false)
            {
                clues.Add(new ClueDefinition(new ClueId(id.Value), label, ClueCategory.Object, quiet));
                objects.Add(new DiscoveryRule("atelier.inspect." + id.Value, id, label, description,
                    requires ?? none, new[] { new ClueId(id.Value) }, "这里还没有看清。"));
            }
            void Detail(ObjectId id, string key, string label, string text, params FactId[] requires)
            {
                clues.Add(new ClueDefinition(Observation(key), label, ClueCategory.Observation));
                observations.Add(new ObservationDiscovery(id, key, text, requires, new[] { Observation(key) }));
            }
            FactId Logic(string id, string label, ClueId first, ClueId second)
            {
                var fact = F("logic." + id);
                logics.Add(new LogicDefinition("logic." + id, label, fact, new[] { first, second }));
                return fact;
            }
            void Idea(string id, ClueId concept, string label, string before, string after, FactId logic)
            {
                clues.Add(new ClueDefinition(concept, label, ClueCategory.Concept));
                recipes.Add(new SemanticRecipe("atelier.idea." + id, new[] { Observation(before), Observation(after) },
                    new[] { logic }, new[] { concept }, none));
            }
            void Apply(string id, ClueId concept, ObjectId target, FactId fact,
                NarrativeDisposition disposition = NarrativeDisposition.Deviant, FactId[] requires = null)
                => recipes.Add(new SemanticRecipe("atelier.meaning." + id,
                    new[] { concept, new ClueId(target.Value) }, requires ?? none, new[] { fact }, disposition));

            // 纸面、实物和整体的词条都由玩家分别点击，不能被书或反应代发。
            Object(Painting, "画布", "绷在木框上的亚麻画布，等着第一笔。");
            Object(Brush, "画笔", "笔毛上还沾着蓝色，笔杆搭在杯沿上。");
            Object(Manuscript, "桌上的便条", "明早交稿。想不出题目，就画眼前这些。");
            Object(StillLamp, "桌上的灯", "灯光照着桌面，瓷座上的花纹已经磨淡。");
            Object(StudioRules.PictureWater, "画里的水", "杯子已经画上了，水面还是静的。", new[] { CupSketched });
            Object(StudioRules.PictureLamp, "画里的灯", "画里画了灯芯，却还没有点上那一点亮色。", new[] { LampSketched });
            Object(StudioRules.PictureWindow, "画里的窗", "画里还留着刚才遮住窗边的帘子。", new[] { WindowSketched });
            Object(StudioRules.Cup, "杯里的水", "一只洗笔杯。杯口有旧颜料，水还很清。");
            Object(StudioRules.LampBody, "左边的油灯", "灯芯是黑的，玻璃罩搁在一旁。灯碗里还有油。");
            Object(Matches, "火柴", "纸盒侧面有一条磨旧的砂纸，抽屉里还剩几根火柴。");
            Object(StudioRules.Curtain, "窗帘", "帘环挂在铜杆上，拉绳从左边的铜环垂下来。");
            Object(WetPaper, "纸上的湿色", "蓝色的水迹沿着纸纤维洇开。", new[] { PaperWet });
            Object(Trial, "画边的湿色", "木板上蹭了一道湿颜料，旁边搭着擦笔布。");
            Object(StudioRules.Window, "窗框", "灰蓝色的漆压在木纹上，转角处还能看见刷痕。");
            Object(StudioRules.Ceiling, "屋顶", "屋顶中央也覆着同样的灰蓝色笔触，梁木压在它下面。");
            Object(StudioRules.Sky, "天空", "窗外是平常的夜色。远处还有云。");
            Object(SharedMark, "接连的笔迹", "这道颜料越过木头和灰泥，到了布面上也没有断。",
                new[] { RoofErased, StudioRules.FullSky }, true);
            Object(StudioRules.Room, "房间", "从梁木到窗帘，再到桌沿，颜料的边沿连成了同一幅画。",
                new[] { RoofErased, StudioRules.FullSky, SharedMarkSeen, WholeObserved }, true);

            Detail(StudioRules.Cup, "still", "平静的水面", "水面很平，灯的倒影落在杯边。");
            Detail(StudioRules.Cup, "spread", "散开的水纹", "笔尖碰过的地方，蓝色沿着一圈圈水纹散开。", StudioRules.Washed);
            // 前后状态使用完整短语，笔记本中能直接读出“同一盏油灯的变化”。
            Detail(StudioRules.LampBody, "dark", "熄灭的油灯", "油灯的灯芯没有发光。");
            Detail(StudioRules.LampBody, "bright", "点亮的油灯", "火苗停在油灯灯芯顶端，瓷座也亮了一点。", StudioRules.LampTurned);
            Detail(StudioRules.Curtain, "hidden", "遮住的窗帘", "帘褶挤着窗边，挡住了一小片山坡。");
            Detail(StudioRules.Curtain, "revealed", "拉开的窗帘", "帘环收向两边，窗边的山坡多露出了一点。", StudioRules.CurtainReleased);
            Detail(Trial, "wet", "擦前的湿色", "木纹被一层没干的蓝灰颜料盖住。");
            Detail(Trial, "base", "擦后的底色", "布带走了湿颜料，原来的木纹露出来了。", TrialWiped);
            var flowLogic = Logic("flow", "流动的灵感", Observation("still"), Observation("spread"));
            var lightLogic = Logic("light", "点亮的灵感", Observation("dark"), Observation("bright"));
            var openLogic = Logic("open", "打开的灵感", Observation("hidden"), Observation("revealed"));
            var eraseLogic = Logic("erase", "擦除的灵感", Observation("wet"), Observation("base"));
            Idea("flow", StudioRules.Flow, "流动", "still", "spread", flowLogic);
            Idea("light", StudioRules.Light, "点亮", "dark", "bright", lightLogic);
            Idea("open", StudioRules.Open, "打开", "hidden", "revealed", openLogic);
            Idea("erase", Erase, "擦除", "wet", "base", eraseLogic);
            Apply("picture_water", StudioRules.Flow, StudioRules.PictureWater, StudioRules.PictureFlow, NarrativeDisposition.Compliant);
            Apply("picture_lamp", StudioRules.Light, StudioRules.PictureLamp, StudioRules.PictureLit, NarrativeDisposition.Compliant);
            Apply("picture_window", StudioRules.Open, StudioRules.PictureWindow, StudioRules.PictureOpen, NarrativeDisposition.Compliant);
            Apply("water_flow", StudioRules.Flow, StudioRules.Cup, StudioRules.WaterFlow);
            Apply("paper_flow", StudioRules.Flow, WetPaper, PaperFlow, requires: new[] { PaperWet });
            Apply("water_light", StudioRules.Light, StudioRules.Cup, StudioRules.WaterLit);
            Apply("frame_erase", Erase, StudioRules.Window, FrameErased);
            Apply("roof_erase", Erase, StudioRules.Ceiling, RoofErased);
            Apply("sky_light", StudioRules.Light, StudioRules.Sky, StudioRules.SkyLit);
            Apply("sky_flow", StudioRules.Flow, StudioRules.Sky, StudioRules.SkyFlow);
            Apply("room_erase", Erase, StudioRules.Room, RoomErased,
                requires: new[] { RoofErased, StudioRules.FullSky, SharedMarkSeen, WholeObserved });

            // 物品和画笔都须亲自调查；落笔使用同一个有限引擎，保持中性，不提前凑满服从次数。
            void Sketch(string id, ObjectId source, FactId fact)
            {
                var inputs = source == Brush
                    ? new[] { new ClueId(Painting.Value), new ClueId(Brush.Value) }
                    : new[] { new ClueId(Painting.Value), new ClueId(source.Value), new ClueId(Brush.Value) };
                recipes.Add(new SemanticRecipe("atelier.sketch." + id, inputs, none, new[] { fact }));
            }
            Sketch("cup", StudioRules.Cup, CupSketched);
            Sketch("brush", Brush, BrushSketched);
            Sketch("lamp", StillLamp, LampSketched);
            Sketch("window", StudioRules.Window, WindowSketched);
            reactions.Add(new ReactionRule("atelier.reaction.still_life", new[] { CupSketched, BrushSketched, LampSketched, WindowSketched }, new[] { StillLifePainted }));

            // 天空的两种解释可以互换顺序；局部水光、窗框擦除都不是主线钥匙。
            reactions.Add(new ReactionRule("atelier.reaction.full_sky", new[] { StudioRules.SkyLit, StudioRules.SkyFlow }, new[] { StudioRules.FullSky }));
            reactions.Add(new ReactionRule("atelier.reaction.reality_touched", none, new[] { RealityTouched }, minimumDeviations: 1));
            reactions.Add(new ReactionRule("atelier.reaction.ready", new[] { StudioRules.PictureFlow, StudioRules.PictureLit, StudioRules.PictureOpen },
                new[] { ReadyToDeliver }, requiresNoDeviation: true));
            actions.Add(new SceneActionRule("wash", StudioRules.Cup, none, new[] { StudioRules.Washed }, "笔尖的蓝色在水里散开了。", "先看看杯里的水。"));
            actions.Add(new SceneActionRule("strike_match", Matches, none, new[] { MatchStruck }, "火柴头擦过砂纸，亮了起来。", "先拿一根火柴。"));
            actions.Add(new SceneActionRule("light_wick", StudioRules.LampBody, new[] { MatchStruck }, new[] { StudioRules.LampTurned }, "火苗留在灯芯上。", "先把火柴划亮，再送到灯芯边。"));
            actions.Add(new SceneActionRule("release_curtain", StudioRules.Curtain, none, new[] { StudioRules.CurtainReleased }, "帘子沿着铜杆收向两边。", "先看看窗边的拉绳。"));
            actions.Add(new SceneActionRule("wipe_trial", Trial, none, new[] { TrialWiped }, "木纹露出来了。", "布要碰到湿颜料。"));
            actions.Add(new SceneActionRule("observe_whole", SharedMark, new[] { RoofErased, StudioRules.FullSky }, new[] { SharedMarkSeen, WholeObserved }, "这几道笔迹连在一起。", "先看清它经过了哪里。"));
            actions.Add(new SceneActionRule("deliver", Painting, new[] { ReadyToDeliver }, new[] { CanvasDelivered }, "这张画稿定下了，留在画架上晾干，明早交稿。", "画还有几处没完成。", new[] { RealityTouched, RoomErased }));
            return new RuleSet(clues.ToArray(), objects.ToArray(), recipes.ToArray(), reactions.ToArray(), observations.ToArray(),
                sceneActions: actions.ToArray(), canvasLockFact: CanvasDelivered, logics: logics.ToArray());
        }
    }
}
