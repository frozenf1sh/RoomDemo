// 文件：AtelierFlowRules.StageFour.cs
// 职责：声明点灯、屋顶和房间的表层解释、天空画布解释。
// 边界：解释只改变事实，实际刮开、推开和天空两次点击必须由玩家另行提交。
using System;
using System.Collections.Generic;
namespace Game.Domain
{
    /// <summary>末阶段的有限内容身份，天空参数不进入领域状态。</summary>
    public static partial class AtelierFlowRules
    {
        public static readonly FactId RoofProbed = F("roof_probed");
        public static readonly FactId RoofAsPaint = F("roof_as_paint"), RoomAsPaint = F("room_as_paint");
        public static readonly FactId SkyAsCanvas = F("sky_as_canvas"), SkyCloudPainted = F("sky_cloud_painted");
        public static readonly FactId SkyStarsPainted = F("sky_stars_painted"), SkyFinished = F("sky_finished");
        private static void AppendStageFour(List<ClueDefinition> clues,List<DiscoveryRule> objects,
            List<SceneActionRule> actions,List<SemanticRecipe> recipes)
        {
            var stage=new[] { Finished(3) };
            var blocked=new[] { AtelierRules.CanvasDelivered,AtelierRules.RoomErased };
            foreach(var pair in new[] { ("warm","温暖"),("bright","明亮"),("roof_night","屋面下的夜色") })
                clues.Add(new ClueDefinition(C(pair.Item1),pair.Item2,ClueCategory.Observation));
            void Source(ObjectId id,string name,string text,FactId[] requires)
            {
                var own=new ClueId(id.Value);
                clues.Add(new ClueDefinition(own,name,ClueCategory.Object,quietDiscovery:id==StudioRules.Room));
                objects.Add(new DiscoveryRule("atelier.flow.inspect."+id.Value,id,name,text,requires,new[] { own }));
            }
            Source(StudioRules.LampBody,"左侧油灯","灯芯还没点着，旁边放着一盒火柴。",stage);
            Source(AtelierRules.Matches,"火柴","火柴盒侧面是砂纸。",stage);
            Source(StudioRules.Ceiling,"屋顶","文森特：这层东西看起来像干颜料。",new[] { Finished(3),StudioRules.LampTurned });
            Source(StudioRules.Sky,"天空","文森特：天空，好干净，看起来像画布……",new[] { AtelierRules.RoomErased });
            Source(AtelierRules.SharedMark,"接连的笔迹","",new[] {SkyFinished});
            Source(StudioRules.Room,"房间","文森特：湿颜料沿着地面连到了墙边。",new[] { AtelierRules.RoofErased });
            actions.Add(new SceneActionRule("strike_match",AtelierRules.Matches,stage,new[] { AtelierRules.MatchStruck },
                "火柴划着了。","先取一根火柴。",blocked));
            actions.Add(new SceneActionRule("light_wick",StudioRules.LampBody,new[] { Finished(3),AtelierRules.MatchStruck },
                new[] { StudioRules.LampTurned },"灯芯亮起来了。","先划燃火柴，再把火柴头送到灯芯边。",blocked,
                new[] { C("warm"),C("bright") },new[] { AtelierRules.Matches }));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.brush.4",new[] { C("warm"),C("bright") },
                new[] { Finished(3),StudioRules.LampTurned },new[] { Brush(4) },Array.Empty<FactId>(),
                consumedInputs:new[] { C("warm"),C("bright") }));
            // 世界解释不产出物体卡；物体身份必须已经由玩家亲点认识。
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.roof_paint",new[] {C("dry_paint"),new ClueId(StudioRules.Ceiling.Value)},
                new[] {StudioRules.LampTurned},new[] {RoofAsPaint},consumedInputs:new[] {C("dry_paint")}));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.room_paint",new[] {C("wet_paint"),new ClueId(StudioRules.Room.Value)},
                new[] {AtelierRules.RoofErased},new[] {RoomAsPaint},consumedInputs:new[] {C("wet_paint")}));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.sky_canvas",new[] {new ClueId(StudioRules.Sky.Value),new ClueId(AtelierRules.Painting.Value)},
                new[] {AtelierRules.RoomErased},new[] {SkyAsCanvas}));
        }
    }
}
