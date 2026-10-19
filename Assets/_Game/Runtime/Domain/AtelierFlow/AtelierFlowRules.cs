// 文件：AtelierFlowRules.cs
// 职责：定义四阶段新流程的稳定身份、首批观察、动作与卷涡笔刷配方。
// 边界：旧AtelierRules保持原样；首批只开放P00至P07，不用旧静物计数推进新阶段。
using System;
using System.Collections.Generic;
namespace Game.Domain
{
    /// <summary>首批内容表与后续批次共用的稳定协议。</summary>
    public static class AtelierFlowRules
    {
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

        /// <summary>建立第一批真实闭环；未接入的二至四层不会获得凭空笔刷。</summary>
        public static RuleSet CreateOpening()
        {
            var clues = new List<ClueDefinition>();
            var objects = new List<DiscoveryRule>();
            var actions = new List<SceneActionRule>();
            var recipes = new List<SemanticRecipe>();
            var empty = Array.Empty<FactId>();
            void Observe(string id, string name) => clues.Add(new ClueDefinition(C(id), name, ClueCategory.Observation));
            Observe("transparent", "透明"); Observe("mapping", "映射"); Observe("blue", "蓝色");
            Observe("flow", "流动"); Observe("free", "奔放"); Observe("messy", "杂乱");
            var ice = new ClueId("atelier.flow.ice_blue");
            var swirl = new ClueId("atelier.flow.swirl");
            clues.Add(new ClueDefinition(ice, "冰蓝", ClueCategory.Concept));
            clues.Add(new ClueDefinition(swirl, "卷涡", ClueCategory.Concept));
            for (int n = 1; n <= 4; n++) clues.Add(new ClueDefinition(Brush(n), n == 1 ? "卷涡笔刷" : "第" + n + "阶段笔刷", ClueCategory.Concept));
            void Object(ObjectId id, string label, string text, params ClueId[] observations)
            {
                var own = new ClueId(id.Value);
                clues.Add(new ClueDefinition(own, label, ClueCategory.Object));
                var grants = new List<ClueId> { own }; grants.AddRange(observations);
                objects.Add(new DiscoveryRule("atelier.flow.inspect." + id.Value, id, label, text,
                    empty, grants));
            }
            // 便条是可选动机说明；开场UI输入锁不伪装成“已读便条”的领域事实。
            Object(AtelierRules.Manuscript, "催稿便条", "快交稿了，我还是一点想法都没有。");
            Object(StudioRules.Cup, "杯里的水", "一杯水而已。它可以映出你。", C("transparent"), C("mapping"));
            Object(AtelierRules.Brush, "洗笔画笔", "把笔尖放进水里。");
            Object(Crayon, "蜡笔画", "真怀念那时候奔放又杂乱的笔触。", C("free"), C("messy"));
            Object(AtelierRules.Painting, "画布", "");
            actions.Add(new SceneActionRule("read_note", AtelierRules.Manuscript, empty, new[] { IntroRead }, "", "先看看便条。"));
            actions.Add(new SceneActionRule("wash_release_pigment", StudioRules.Cup, empty, new[] { Washed },
                "到处都是蓝色。我刚刚在干什么来着……？", "先亲点杯水和洗笔画笔。",
                observations: new[] { C("blue"), C("flow") }, additionalSources: new[] { AtelierRules.Brush }));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.ice_blue", new[] { C("transparent"), C("blue") }, empty, new[] { ice }, empty));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.swirl", new[] { C("flow"), C("free") }, empty, new[] { swirl }, empty));
            recipes.Add(new SemanticRecipe("atelier.flow.recipe.brush.1", new[] { ice, swirl }, empty, new[] { Brush(1) }, empty));
            // 新线明确等待主动完成；配方次数不能触发旧的自动锁画布。
            return new RuleSet(clues.ToArray(), objects.ToArray(), recipes.ToArray(), Array.Empty<ReactionRule>(),
                declaredFacts: new[] { Enabled, Finished(1), Finished(2), Finished(3), Finished(4), DryLearned, WetLearned, WorldCommitted },
                sceneActions: actions.ToArray(), canvasLockFact: AtelierRules.CanvasDelivered);
        }
    }
}
