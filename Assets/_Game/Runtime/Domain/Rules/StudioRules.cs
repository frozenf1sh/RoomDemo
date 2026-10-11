// =============================================================================
// 文件：StudioRules.cs
// 职责：新版画室的有限观察、灵感、准备动作与解释联动。
// 边界：只定义领域内容；所有星夜、颜色、移动与开墙由表现层消费。
// =============================================================================
using System;
using System.Collections.Generic;
namespace Game.Domain
{
    /// <summary>画室第一条完整路线的内容表，复用现有同步反应闭包。</summary>
    public static class StudioRules
    {
        /// <summary>与场景绑定的物体身份，不因中文文案改变。</summary>
        public static readonly ObjectId Cup = O("studio.cup"), Lamp = O("studio.lamp"), Study = O("studio.study"),
            Rest = O("studio.rest"), Curtain = O("studio.curtain"), Ceiling = O("studio.ceiling"), Skylight = O("studio.skylight"),
            TopLine = O("studio.top_line"), Window = O("studio.window"), WindowLine = O("studio.window_line"),
            Door = O("studio.door"), Room = O("studio.room"), PictureWater = O("studio.picture_water"),
            PictureLamp = O("studio.picture_lamp"), PictureWindow = O("studio.picture_window"), LampBody = O("studio.lamp_body"), Sky = O("studio.sky"), Tree = O("studio.tree");
        /// <summary>主概念都来自比较观察，不能直接在点击物体时奖励。</summary>
        public static readonly ClueId Flow = C("studio.flow"), Light = C("studio.light"), Open = C("studio.open"), Extend = C("studio.extend");
        /// <summary>准备操作与最终汇合的事实身份。</summary>
        public static readonly FactId Washed = F("washed"), LampTurned = F("lamp_turned"), CupPlaced = F("cup_placed"),
            RestMoved = F("rest_moved"), LampAimed = F("lamp_aimed"), LightPathClear = F("light_path_clear"),
            FrameLifted = F("frame_lifted"), StrokeContinued = F("stroke_continued"), CurtainReleased = F("curtain_released"),
            WaterFlow = F("water_flow"), WaterLit = F("water_lit"), ShadeLit = F("shade_lit"),
            PictureFlow = F("picture_flow"), PictureLit = F("picture_lit"), PictureOpen = F("picture_open"),
            RoofLit = F("roof_lit"), RoofFlow = F("roof_flow"), SkylightOpen = F("skylight_open"), RoofExtended = F("roof_extended"),
            FullSky = F("full_sky"), WindowOpen = F("window_open"), WindowExtended = F("window_extended"),
            BoundaryConnected = F("boundary_connected"), WholeObserved = F("whole_observed"), RoomOpened = F("room_opened"),
            BanalComplete = F("banal_complete"), WaterCarriesLight = F("water_carries_light"), DoorOpen = F("door_open"),
            RoomFlow = F("room_flow"), RoomLit = F("room_lit"), ShadeDropped = F("shade_dropped"),
            PaintPeeled = F("paint_peeled"), SkyVisible = F("sky_visible"), SkyLit = F("sky_lit"), SkyFlow = F("sky_flow"),
            TreeFlow = F("tree_flow"), TreeLit = F("tree_lit"), WorldComplete = F("world_complete");
        /// <summary>保持物体、碎片和事实的稳定英文协议。</summary>
        public static ObjectId O(string id) => new ObjectId(id);
        /// <summary>构造统一碎片身份。</summary>
        public static ClueId C(string id) => new ClueId(id);
        /// <summary>本关事实采用独立前缀，不碰历史内容。</summary>
        public static FactId F(string id) => new FactId("studio." + id);
        /// <summary>创建完整内容；自然操作和概念解释在同一会话中求值。</summary>
        public static RuleSet Create()
        {
            var clues = new List<ClueDefinition>();
            var discoveries = new List<DiscoveryRule>();
            var observations = new List<ObservationDiscovery>();
            var recipes = new List<SemanticRecipe>();
            var reactions = new List<ReactionRule>();
            var none = Array.Empty<FactId>();
            void Object(ObjectId id, string label, string text, FactId[] requires = null, bool quiet = false)
            {
                clues.Add(new ClueDefinition(C(id.Value), label, ClueCategory.Object, quiet));
                discoveries.Add(new DiscoveryRule("inspect." + id.Value, id, label, text, requires ?? none, new[] { C(id.Value) }, "这里还被挡着。"));
            }
            void Observation(ObjectId source, string id, string label, string text, params FactId[] requires)
            {
                var clue = C("studio.obs." + id);
                clues.Add(new ClueDefinition(clue, label, ClueCategory.Observation));
                observations.Add(new ObservationDiscovery(source, id, text, requires, new[] { clue }));
            }
            void Idea(string id, ClueId concept, string label, string first, string second)
            {
                clues.Add(new ClueDefinition(concept, label, ClueCategory.Concept));
                recipes.Add(new SemanticRecipe("studio.idea." + id, new[] { C("studio.obs." + first), C("studio.obs." + second) }, none, new[] { concept }, none));
            }
            void Meaning(string id, ClueId concept, ObjectId target, FactId fact, NarrativeDisposition disposition = NarrativeDisposition.Deviant)
                => recipes.Add(new SemanticRecipe("studio.meaning." + id, new[] { concept, C(target.Value) }, none, new[] { fact }, disposition));
            void Reaction(string id, FactId[] requires, params FactId[] produces)
                => reactions.Add(new ReactionRule("studio.reaction." + id, requires, produces));

            // 观察只留下物体和现象；玩家随后在同一有限引擎里形成概念。
            Object(PictureWater, "画里的水", "画上的水纹还没有画完。");
            Object(PictureLamp, "画里的灯", "画里的灯没有光。");
            Object(PictureWindow, "画里的窗", "画中的窗扇闭着。");
            Object(Cup, "杯里的水", "颜料正在水里散开。试试把它和‘散开的颜料’放到右页。");
            Object(LampBody, "台灯", "灯芯熄着，开关就在灯座旁。");
            Object(Lamp, "灯罩", "掉下的灯罩上画着几颗暗淡的星星。", new[] { ShadeDropped });
            Object(Curtain, "窗帘", "两片窗帘合拢，拉环垂在一旁。");
            Object(Skylight, "天窗", "剥落的漆露出一圈窗框。", new[] { PaintPeeled });
            Object(Window, "窗", "窗帘后是一扇闭着的窗。", new[] { CurtainReleased });
            Object(Sky, "天空", "几颗星还很暗。云停在半空，像没有干透的颜料。", new[] { RoomOpened });
            Object(Tree, "树", "树枝弯向同一个方向，叶子却一动不动。暗处的叶尖藏着一点金色。", new[] { RoomOpened });
            Object(Room, "房间", "一道颜料边沿绕过屋顶和墙角，围住了整间画室。就像画布的边缘。", new[] { BoundaryConnected }, true);
            Observation(Cup, "spread", "散开的颜料", "水中的蓝色颜料慢慢散开。", Washed);
            Observation(LampBody, "dark", "熄灭的灯芯", "灯芯没有发光。");
            Observation(LampBody, "bright", "点起后的灯芯", "开关按下以后，灯芯亮了。", LampTurned);
            Observation(Curtain, "hidden", "合拢的窗帘", "窗帘遮住了窗外。");
            Observation(Curtain, "revealed", "拉开的窗帘", "拉开窗帘，窗外露了出来。", CurtainReleased);
            clues.Add(new ClueDefinition(Flow, "流动", ClueCategory.Concept));
            recipes.Add(new SemanticRecipe("studio.idea.flow", new[] { C(Cup.Value), C("studio.obs.spread") }, none, new[] { Flow }, none));
            Idea("light", Light, "点亮", "dark", "bright");
            Idea("open", Open, "打开", "hidden", "revealed");
            Meaning("picture_water", Flow, PictureWater, PictureFlow, NarrativeDisposition.Compliant);
            Meaning("picture_lamp", Light, PictureLamp, PictureLit, NarrativeDisposition.Compliant);
            Meaning("picture_window", Open, PictureWindow, PictureOpen, NarrativeDisposition.Compliant);
            Meaning("water_flow", Flow, Cup, WaterFlow);
            Meaning("water_light", Light, Cup, WaterLit);
            Meaning("shade_light", Light, Lamp, ShadeLit);
            Meaning("skylight_open", Open, Skylight, SkylightOpen);
            Meaning("window_open", Open, Window, WindowOpen);
            Meaning("sky_light", Light, Sky, SkyLit);
            Meaning("sky_flow", Flow, Sky, SkyFlow);
            Meaning("tree_flow", Flow, Tree, TreeFlow);
            Meaning("tree_light", Light, Tree, TreeLit);
            Meaning("room_open", Open, Room, RoomOpened);
            Meaning("room_flow", Flow, Room, RoomFlow);
            Meaning("room_light", Light, Room, RoomLit);
            // 两种真实水光解法汇合；动画是否落地绝不决定这些事实。
            Reaction("shade_on_water", new[] { ShadeLit }, WaterLit);
            Reaction("water_carries", new[] { WaterFlow, WaterLit }, WaterCarriesLight, PaintPeeled);
            Reaction("skylight_view", new[] { SkylightOpen }, SkyVisible);
            Reaction("window_view", new[] { WindowOpen }, SkyVisible);
            Reaction("living_sky", new[] { SkyLit, SkyFlow }, FullSky);
            // 天窗揭出完整画室边沿，天空创作留到空间展开之后；侧窗是可选探索。
            Reaction("boundary_spills", new[] { SkylightOpen }, BoundaryConnected);
            Reaction("world_complete", new[] { RoomOpened, FullSky }, WorldComplete);
            reactions.Add(new ReactionRule("studio.reaction.canvas_end", new[] { PictureFlow, PictureLit, PictureOpen }, new[] { BanalComplete }, requiresNoDeviation: true));
            var actions = new[] {
                new SceneActionRule("wash", Cup, none, new[] { Washed }, "颜料在水里散开。", "看看水里的颜料。"),
                new SceneActionRule("turn_lamp", LampBody, none, new[] { LampTurned, ShadeDropped }, "灯亮了。松动的灯罩掉在桌上。", "看看灯座上的开关。"),
                new SceneActionRule("release_curtain", Curtain, none, new[] { CurtainReleased }, "窗帘拉开了。", "看看窗帘的拉环。")
            };
            return new RuleSet(clues.ToArray(), discoveries.ToArray(), recipes.ToArray(), reactions.ToArray(), observations.ToArray(), sceneActions: actions);
        }
    }
}
