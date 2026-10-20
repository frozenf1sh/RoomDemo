// 文件：AtelierFlowRules.cs
// 职责：定义四阶段新流程的稳定身份、前三阶段观察、动作与笔刷配方。
// 边界：旧AtelierRules保持原样；当前开放P00至P17，不用旧静物计数推进新阶段。
using System;
using System.Collections.Generic;
namespace Game.Domain
{
    /// <summary>首批内容表与后续批次共用的稳定协议。</summary>
    public static partial class AtelierFlowRules
    {
        public static readonly ObjectId Plaster = new ObjectId("atelier.flow.plaster");
        public static readonly ObjectId HandMirror = new ObjectId("atelier.flow.hand_mirror");
        public static readonly ObjectId DryBrush = new ObjectId("atelier.flow.dry_brush");
        public static readonly ObjectId MirrorShards = new ObjectId("atelier.flow.mirror_shards");
        public static readonly FactId MirrorCompared = F("mirror_compared");
        public static readonly ObjectId Crayon = new ObjectId("atelier.flow.crayon_picture");
        public static readonly FactId Enabled = F("enabled"), IntroRead = F("intro_read"), Washed = F("washed");
        public static readonly FactId DryLearned = F("knife_dry"), WetLearned = F("knife_wet"), WorldCommitted = F("world_committed");
        /// <summary>新流程事实使用独立前缀，避免触发旧线的三次解释收束。</summary>
        public static FactId F(string id) => new FactId("atelier.flow." + id);
        /// <summary>四个图样的稳定材料ID，类别为概念，不冒充实体画笔。</summary>
        public static ClueId Brush(int layer) => new ClueId("atelier.flow.brush." + layer);
        /// <summary>收层事实只由画布命令产生，视觉显色不能授予。</summary>
        public static FactId Finished(int layer) => F("layer_finished." + layer);
        /// <summary>来源内的性质身份；显示词不用于匹配。</summary>
        public static ClueId C(string id) => new ClueId("atelier.flow.observation." + id);
        /// <summary>已完成第一层以后开放左侧；后续阶段由对应收层事实恢复。</summary>
        public static int Stage(GameState state) => state.HasFact(Finished(3)) ? 4 : state.HasFact(Finished(2)) ? 3 : state.HasFact(Finished(1)) ? 2 : 1;

        /// <summary>建立前三阶段真实闭环；未接入的第四层不会获得凭空笔刷。</summary>
        public static RuleSet CreateOpening()
        {
            var clues = new List<ClueDefinition>();
            var objects = new List<DiscoveryRule>();
            var actions = new List<SceneActionRule>();
            var recipes = new List<SemanticRecipe>();
            var empty = Array.Empty<FactId>();
            void Observe(string id, string name) => clues.Add(new ClueDefinition(C(id), name, ClueCategory.Observation));
            // 只收录当前流程真正用于配方的灰色观察，描述性用语留在原稿介绍中。
            Observe("transparent", "透明"); Observe("blue", "蓝色");
            Observe("flow", "流动"); Observe("free", "奔放");
            Observe("fixed", "固定"); Observe("flip", "翻转");
            Observe("black", "黑色"); Observe("sharp", "锋利");
            var twisted = new ClueId("atelier.flow.twisted");
            var rough = new ClueId("atelier.flow.rough");
            clues.Add(new ClueDefinition(twisted, "扭曲", ClueCategory.Concept));
            clues.Add(new ClueDefinition(rough, "粗犷", ClueCategory.Concept));
            var ice = new ClueId("atelier.flow.ice_blue");
            var swirl = new ClueId("atelier.flow.swirl");
            clues.Add(new ClueDefinition(ice, "冰蓝", ClueCategory.Concept));
            clues.Add(new ClueDefinition(swirl, "卷涡", ClueCategory.Concept));
            for (int n = 1; n <= 4; n++) clues.Add(new ClueDefinition(Brush(n), n == 1 ? "卷涡笔刷" : n == 2 ? "柏影笔刷" : n == 3 ? "夜城笔刷" : "暖星笔刷", ClueCategory.Concept));
            void Object(ObjectId id, string label, string text, params ClueId[] observations)
            {
                var own = new ClueId(id.Value);
                clues.Add(new ClueDefinition(own, label, ClueCategory.Object));
                var grants = new List<ClueId> { own }; grants.AddRange(observations);
                objects.Add(new DiscoveryRule("atelier.flow.inspect." + id.Value, id, label, text,
                    id == Plaster || id == HandMirror || id == DryBrush || id == MirrorShards ? new[] { Finished(1) } : empty, grants));
            }
            // 便条是可选动机说明；开场UI输入锁不伪装成“已读便条”的领域事实。
            Object(AtelierRules.Manuscript, "催稿便条", "快交稿了，我还是一点想法都没有。");
            Object(StudioRules.Cup, "洗笔杯", "杯子上靠着一个沾着蓝色颜料的笔，洗一下比较好");
            Object(AtelierRules.Brush, "洗笔画笔", "把笔尖浸到水里洗一下吧");
            Object(Crayon, "蜡笔画", "真怀念那时候奔放又杂乱的笔触。", C("free"));
            Object(AtelierRules.Painting, "画布", "");
            Object(Plaster, "石膏像", "很枯燥。是的，但不去钻研它们，就无法表达出更多的事物。是这样没错……", C("fixed"));
            Object(HandMirror, "手镜", "这个小玩意儿是怎么买到的来着……哦，当时研究自画像入了迷。还能转啊转的。自我也有两面性……呵呵。那时候的我肯定会这么说。");
            Object(DryBrush, "黑色干画笔", "黑色。对，闭塞和笃定，这是我无论如何都想达到的事情。", C("black"));
            Object(MirrorShards, "碎镜片", "去他的自我。但是碎成一片片了，一样的还是自我。当心，别在拼凑自我的时候划伤了自己。", C("sharp"));
            actions.Add(new SceneActionRule("mirror_compare", HandMirror, new[] { Finished(1) }, new[] { MirrorCompared },
                "再规矩的形，翻过来就不压我了。", "先亲点手镜，再转动镜面。", observations: new[] { C("flip") }));
            actions.Add(new SceneActionRule("read_note", AtelierRules.Manuscript, empty, new[] { IntroRead }, "", "先看看便条。"));
            actions.Add(new SceneActionRule("wash_release_pigment", StudioRules.Cup, empty, new[] { Washed },
                "到处都是蓝色。我刚刚在干什么来着……？", "先亲点杯水和洗笔画笔。",
                observations: new[] { C("transparent"), C("blue"), C("flow") }, additionalSources: new[] { AtelierRules.Brush }));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.ice_blue", new[] { C("transparent"), C("blue") }, empty, new[] { ice }, empty, consumedInputs: new[] { C("transparent"), C("blue") }));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.swirl", new[] { C("flow"), C("free") }, empty, new[] { swirl }, empty, consumedInputs: new[] { C("flow"), C("free") }));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.brush.1", new[] { ice, swirl }, empty, new[] { Brush(1) }, empty));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.twisted", new[] { C("fixed"), C("flip") }, new[] { Finished(1) }, new[] { twisted }, empty, consumedInputs: new[] { C("fixed"), C("flip") }));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.rough", new[] { C("black"), C("sharp") }, new[] { Finished(1) }, new[] { rough }, empty, consumedInputs: new[] { C("black"), C("sharp") }));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.brush.2", new[] { twisted, rough }, new[] { Finished(1) }, new[] { Brush(2) }, empty));
            AppendStageThree(clues,objects,actions,recipes);
            // 新线明确等待主动完成；配方次数不能触发旧的自动锁画布。
            return new RuleSet(clues.ToArray(), objects.ToArray(), recipes.ToArray(), Array.Empty<ReactionRule>(),
                declaredFacts: new[] { Enabled, MirrorCompared, Finished(1), Finished(2), Finished(3), Finished(4), DryLearned, WetLearned, WorldCommitted,
                    WindowNearSeen, WindowFarSeen, WindowCompared, PaletteStudied },
                sceneActions: actions.ToArray(), canvasLockFact: AtelierRules.CanvasDelivered, maximumCombinationInputs: 2);
        }
    }
}
