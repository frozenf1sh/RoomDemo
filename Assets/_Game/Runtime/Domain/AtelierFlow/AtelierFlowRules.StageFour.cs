// 文件：AtelierFlowRules.StageFour.cs
// 职责：定义末阶段点灯、暖星和屋面以后亲手调查的有限内容。
// 边界：不按时间授予屋顶、天空或房间；两条终局由专用画布与工具命令互斥提交。
using System;
using System.Collections.Generic;
namespace Game.Domain
{
    /// <summary>末阶段只增加一条笔刷配方，隐藏路线依靠现场动作。</summary>
    public static partial class AtelierFlowRules
    {
        public static readonly FactId RoofProbed = F("roof_probed");
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
            Source(StudioRules.Ceiling,"屋面","……原来这里也可以被触碰。",new[] { Finished(3),StudioRules.LampTurned });
            Source(StudioRules.Sky,"天空","",new[] { AtelierRules.RoofErased });
            Source(AtelierRules.SharedMark,"接连的笔迹","",new[] { AtelierRules.RoofErased });
            Source(StudioRules.Room,"房间","",new[] { AtelierRules.RoofErased,AtelierRules.SharedMarkSeen });
            actions.Add(new SceneActionRule("strike_match",AtelierRules.Matches,stage,new[] { AtelierRules.MatchStruck },
                "火柴划着了。","先取一根火柴。",blocked));
            actions.Add(new SceneActionRule("light_wick",StudioRules.LampBody,new[] { Finished(3),AtelierRules.MatchStruck },
                new[] { StudioRules.LampTurned },"灯芯亮起来了。","先划燃火柴，再把火柴头送到灯芯边。",blocked,
                new[] { C("warm"),C("bright") },new[] { AtelierRules.Matches }));
            actions.Add(new SceneActionRule("study_shared_mark",AtelierRules.SharedMark,new[] { AtelierRules.RoofErased },
                new[] { AtelierRules.SharedMarkSeen },"","先亲眼看看开口后的天空。",blocked,
                additionalSources:new[] { StudioRules.Sky }));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.brush.4",new[] { C("warm"),C("bright") },
                new[] { Finished(3),StudioRules.LampTurned },new[] { Brush(4) },Array.Empty<FactId>(),
                consumedInputs:new[] { C("warm"),C("bright") }));
        }
    }
}
