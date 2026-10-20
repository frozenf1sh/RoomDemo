// 文件：AtelierFlowRules.StageThree.cs
// 职责：声明窗灯类比、夜城笔刷以及干湿颜料的实际接触观察。
// 边界：来源只由亲点取得；不使用比较面板或抽象中间词替代玩家观察。
using System;
using System.Collections.Generic;
namespace Game.Domain
{
    /// <summary>第三阶段只追加有限内容，不改变通用配方引擎。</summary>
    public static partial class AtelierFlowRules
    {
        public static readonly ObjectId DryPalette = new ObjectId("atelier.flow.dry_palette");
        public static readonly ObjectId PaletteKnife = new ObjectId("atelier.flow.palette_knife");
        public static readonly ObjectId NearWindowLight = new ObjectId("atelier.flow.window_light.near");
        public static readonly ObjectId FarWindowLight = new ObjectId("atelier.flow.window_light.far");
        public static readonly FactId WindowNearSeen = F("window_near_seen"), WindowFarSeen = F("window_far_seen");
        public static readonly FactId WindowCompared = F("window_compared"), PaletteStudied = F("palette_studied");

        /// <summary>两处亲见窗灯形成灯火，再与窗景形成原有夜城图样。</summary>
        private static void AppendStageThree(List<ClueDefinition> clues,List<DiscoveryRule> objects,
            List<SceneActionRule> actions,List<SemanticRecipe> recipes)
        {
            var stage = new[] { Finished(2) };
            var none = Array.Empty<FactId>();
            foreach (var pair in new[] { ("window_lights","灯火"),("dry_paint","干颜料"),("wet_paint","湿颜料") })
                clues.Add(new ClueDefinition(C(pair.Item1),pair.Item2,ClueCategory.Observation));
            void Source(ObjectId id,string name,string text)
            {
                var clue = new ClueId(id.Value);
                clues.Add(new ClueDefinition(clue,name,ClueCategory.Object));
                objects.Add(new DiscoveryRule("atelier.flow.inspect."+id.Value,id,name,text,stage,new[] { clue }));
            }
            Source(StudioRules.Window,"窗景","");
            Source(NearWindowLight,"近处的窗灯","近处那扇窗还亮着灯。");
            Source(FarWindowLight,"远处的窗灯","远处也有几扇亮着灯的窗。");
            Source(DryPalette,"干颜料板","啊，一团乱糟。它们全都干掉了。我什么都画不出来有一段时间了。");
            Source(PaletteKnife,"调色刮刀","");
            actions.Add(new SceneActionRule("window_near",NearWindowLight,stage,new[] { WindowNearSeen },"","先看看近处的窗灯。"));
            actions.Add(new SceneActionRule("window_far",FarWindowLight,stage,new[] { WindowFarSeen },"","先看看远处的窗灯。"));
            // 看板不授予干湿词，经验与观察在玩家真实操作后同一次成立。
            actions.Add(new SceneActionRule("palette_inspect",DryPalette,stage,new[] { PaletteStudied },"","先看看颜料板。"));
            actions.Add(new SceneActionRule("palette_scrape",DryPalette,stage,new[] { DryLearned },
                "干壳刮开了。底下还有一点没干透的颜料。","先亲点颜料板和刮刀。",
                observations:new[] { C("dry_paint") },additionalSources:new[] { PaletteKnife }));
            actions.Add(new SceneActionRule("palette_spread",DryPalette,new[] { Finished(2),DryLearned },new[] { WetLearned },
                "湿颜料被推开了。","先刮开表面的干壳。",observations:new[] { C("wet_paint") },additionalSources:new[] { PaletteKnife }));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.window_lights",
                new[] {new ClueId(NearWindowLight.Value),new ClueId(FarWindowLight.Value)},stage,new[] {C("window_lights")},none));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.brush.3",new[] {C("window_lights"),new ClueId(StudioRules.Window.Value)},
                stage,new[] {Brush(3)},none,consumedInputs:new[] {C("window_lights")}));
        }
    }
}
