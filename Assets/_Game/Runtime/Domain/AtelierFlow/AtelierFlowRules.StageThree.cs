// 文件：AtelierFlowRules.StageThree.cs
// 职责：追加窗景比较、干颜料板与两种刮刀接触的第三阶段有限内容。
// 边界：来源只由亲点认识；细节和工具动作不授予屋顶、房间或完整概念。
using System;
using System.Collections.Generic;
namespace Game.Domain
{
    /// <summary>第三阶段保持与前两阶段相同的内容驱动规则，不扩展通用引擎。</summary>
    public static partial class AtelierFlowRules
    {
        public static readonly ObjectId DryPalette = new ObjectId("atelier.flow.dry_palette");
        public static readonly ObjectId PaletteKnife = new ObjectId("atelier.flow.palette_knife");
        public static readonly FactId WindowNearSeen = F("window_near_seen"), WindowFarSeen = F("window_far_seen");
        public static readonly FactId WindowCompared = F("window_compared"), PaletteStudied = F("palette_studied");

        /// <summary>在同一规则集中追加四个必要灰词、三个配方和实际动作门槛。</summary>
        private static void AppendStageThree(List<ClueDefinition> clues,List<DiscoveryRule> objects,
            List<SceneActionRule> actions,List<SemanticRecipe> recipes)
        {
            var stage = new[] { Finished(2) };
            var none = Array.Empty<FactId>();
            foreach (var pair in new[] { ("reality","现实"),("night","夜色"),("stars","星辰"),("chaos","混乱") })
                clues.Add(new ClueDefinition(C(pair.Item1),pair.Item2,ClueCategory.Observation));
            var body = new ClueId("atelier.flow.body");
            var present = new ClueId("atelier.flow.present");
            clues.Add(new ClueDefinition(body,"肉身",ClueCategory.Concept));
            clues.Add(new ClueDefinition(present,"当下",ClueCategory.Concept));
            void Source(ObjectId id,string name,string text)
            {
                var clue = new ClueId(id.Value);
                clues.Add(new ClueDefinition(clue,name,ClueCategory.Object));
                objects.Add(new DiscoveryRule("atelier.flow.inspect."+id.Value,id,name,text,stage,new[] { clue }));
            }
            Source(StudioRules.Window,"窗景","对了，这一带一直不大热闹，离城市很远，也没有星星。但有时候夜里亮起的窗灯也可以是星星吧？");
            Source(DryPalette,"干颜料板","啊，一团乱糟。它们全都干掉了。我什么都画不出来有一段时间了。");
            Source(PaletteKnife,"调色刮刀","");
            actions.Add(new SceneActionRule("window_near",StudioRules.Window,stage,new[] { WindowNearSeen },
                "近处的屋檐挡住了一部分远处房屋。","先走到窗边看看。",observations:new[] { C("reality") }));
            actions.Add(new SceneActionRule("window_far",StudioRules.Window,stage,new[] { WindowFarSeen },
                "远处有几扇亮着灯的窗。","先走到窗边看看。",observations:new[] { C("night") }));
            actions.Add(new SceneActionRule("window_compare",StudioRules.Window,new[] { Finished(2),WindowNearSeen,WindowFarSeen },new[] { WindowCompared },
                "也许我不应该想那么多。即使现实是那样让人沮丧，星星也存在于大地上。",
                "先看看近处屋檐和远处灯火。",observations:new[] { C("stars") }));
            actions.Add(new SceneActionRule("palette_inspect",DryPalette,stage,new[] { PaletteStudied },
                "很多时候我感觉自己在夜里才是那样感性。我的躯壳就像这样混乱地依附在作画的执念上。",
                "先看看干颜料板。",observations:new[] { C("chaos") }));
            // 两面经验都要求亲点板与刮刀；没有用取工具或播放演出来代替表面接触。
            actions.Add(new SceneActionRule("palette_scrape",DryPalette,new[] { Finished(2),PaletteStudied },new[] { DryLearned },
                "去掉了那些混乱干瘪的混蛋，剩下的是一片空白。","先细看颜料，再拿起板旁的刮刀。",
                additionalSources:new[] { PaletteKnife }));
            actions.Add(new SceneActionRule("palette_spread",DryPalette,new[] { Finished(2),PaletteStudied },new[] { WetLearned },
                "已用刀面推开软色。","先细看颜料，再拿起板旁的刮刀。",additionalSources:new[] { PaletteKnife }));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.body",new[] { C("chaos"),C("night") },stage,new[] { body },none,
                consumedInputs:new[] { C("chaos"),C("night") }));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.present",new[] { C("reality"),C("stars") },stage,new[] { present },none,
                consumedInputs:new[] { C("reality"),C("stars") }));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.brush.3",new[] { body,present },stage,new[] { Brush(3) },none));
        }
    }
}
