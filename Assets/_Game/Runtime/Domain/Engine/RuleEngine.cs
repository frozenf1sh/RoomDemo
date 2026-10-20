// =============================================================================
// 文件：RuleEngine.cs
// 职责：执行观察与组合命令，添加事实，并把自动反应求值到稳定状态。
// 边界：纯 C# 领域逻辑；不读取 Transform、材质、Collider、音频或 UI。
// 扩展：新增谜题只追加规则数据，本文件不包含任何具体物体的特殊判断。
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain
{
    /// <summary>有限语义配方与自动反应的同步规则引擎。</summary>
    /// <remarks>成功命令先产生自身事件，再产生反应事件；失败命令不会改变世界。</remarks>
    public sealed class RuleEngine
    {
        // 规则集合在构造时固定；运行期间只修改传入的 GameState。
        private readonly RuleSet rules;

        /// <summary>创建使用指定内容数据的规则引擎。</summary>
        /// <param name="rules">经过唯一性与引用校验的规则集合。</param>
        /// <exception cref="ArgumentNullException">规则集合为空。</exception>
        public RuleEngine(RuleSet rules)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        /// <summary>执行一条外部命令，并返回按发生顺序排列的领域事件。</summary>
        /// <param name="state">唯一的世界事实来源，由应用层持有。</param>
        /// <param name="command">观察、组合，或第一版兼容解释命令。</param>
        /// <returns>只读事件列表；失败时返回失败事件，而非抛出业务异常。</returns>
        /// <exception cref="ArgumentException">命令类型不受支持。</exception>
        public IReadOnlyList<DomainEvent> Execute(GameState state, ICommand command)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var events = new List<DomainEvent>();
            bool accepted;

            // 命令分发只区分输入方式，不能在这里写盆栽、海或窗户的规则。
            if (command is InspectObject inspect) accepted = Inspect(state, inspect, events);
            else if (command is AdvanceDay) accepted = Advance(state, events);
            else if (command is PerformSceneAction action) accepted = Act(state, action, events);
            else if (command is ObserveDetail detail) accepted = Observe(state, detail, events);
            else if (command is Combine combine) accepted = Combine(state, combine.Clues, events);
            else if (command is Interpret interpret) accepted = Interpret(state, interpret, events);
            else throw new ArgumentException("不支持此命令。", nameof(command));

            // 失败的组合不应顺带推进反应；只有被接受的命令才进行闭包求值。
            if (accepted) EvaluateReactions(state, events);
            return events.AsReadOnly();
        }

        /// <summary>调试授予线索；不会完成配方，也不会凭空产生世界事实。</summary>
        /// <param name="state">调试会话状态。</param>
        /// <param name="clues">必须已经存在于内容定义中的碎片。</param>
        /// <returns>仅包含第一次发现的碎片事件。</returns>
        public IReadOnlyList<DomainEvent> DebugGrantClues(GameState state, IReadOnlyList<ClueId> clues)
        {
            // 在任何修改前验证整个输入，避免输入错误留下半完成状态。
            if (clues.Any(c => !rules.Clues.Any(d => d.Id == c)))
                throw new ArgumentException("未知的调试碎片。");
            var events = new List<DomainEvent>();
            GrantClues(state, clues, events);
            return events.AsReadOnly();
        }

        /// <summary>调试添加已定义的事实，并复用正常游戏的反应闭包。</summary>
        /// <param name="state">需要推进的会话状态。</param>
        /// <param name="facts">内容数据中出现过的事实 ID。</param>
        /// <returns>新增事实及其后续反应；不伪造玩家组合历史。</returns>
        public IReadOnlyList<DomainEvent> DebugAddFacts(GameState state, IReadOnlyList<FactId> facts)
        {
            // 允许所有规则引用的事实，不把具体试玩内容写死在引擎中。
            var known = rules.Recipes.SelectMany(x => x.Produces.Concat(x.Requires))
                .Concat(rules.Reactions.SelectMany(x => x.Produces.Concat(x.Requires)))
                .Concat(rules.Logics.Select(x => x.UnlockFact))
                .Concat(rules.Discoveries.SelectMany(x => x.Requires)).Concat(rules.Observations.SelectMany(x => x.Requires))
                .Concat(rules.DeclaredFacts).Concat(rules.SceneActions.SelectMany(x => x.Requires.Concat(x.Produces).Concat(x.Blocks))).ToHashSet();
            if (facts.Any(f => !known.Contains(f))) throw new ArgumentException("未知的调试事实。");
            var events = new List<DomainEvent>();
            // 调试或预设若直接写入灵感事实，也必须恢复其观察前提；否则会形成“有灵感却没有可解释词条”的无效状态。
            foreach (var logic in rules.Logics.Where(x => facts.Contains(x.UnlockFact)))
                GrantClues(state, logic.RequiresClues, events);
            AddFacts(state, facts, events);
            EvaluateReactions(state, events);
            return events.AsReadOnly();
        }

        /// <summary>在全新状态中恢复调试预设，不向表现层发布过渡事件。</summary>
        /// <param name="state">没有已知碎片或事实的新会话状态。</param>
        /// <param name="preset">线索、种子事实和已完成配方的数据快照。</param>
        /// <remarks>应用层应在此操作成功后绑定 Presenter，并立即恢复最终表现。</remarks>
        public void RestorePreset(GameState state, DebugPreset preset)
        {
            if (state.KnownClues.Count > 0 || state.Facts.Count > 0)
                throw new InvalidOperationException("预设只能恢复到全新的会话。");
            if (preset.CompletedRecipes.Any(id => !rules.Recipes.Any(r => r.Id == id)))
                throw new ArgumentException("预设引用了未知配方。");

            // 种子事实走正式闭包，后续事实与反应由规则产生；返回事件在此有意丢弃。
            state.RestoreDay(preset.DayIndex);
            DebugGrantClues(state, preset.Known);
            // 先恢复解释历史再计算闭包，条件反应不能误把有过偏离的预设当成服从路线。
            foreach (var id in preset.CompletedRecipes)
                state.Complete(id, rules.Recipes.Single(r => r.Id == id).Disposition);
            DebugAddFacts(state, preset.Facts);
            foreach (var id in preset.CompletedRecipes)
            {
                var recipe = rules.Recipes.Single(r => r.Id == id);
                // 历史必须与世界一致，不能记录一个尚未产生其输出的配方。
                if (!recipe.ProducesFacts.All(state.HasFact) || !recipe.ProducesClues.All(state.Knows))
                    throw new ArgumentException("预设的已完成配方必须与其产出的线索和事实一致。");
            }
        }

        /// <summary>按发现规则判断可调查性，去重授予碎片并返回观察描述。</summary>
        private bool Inspect(GameState state, InspectObject command, List<DomainEvent> events)
        {
            var definition = rules.Discoveries.FirstOrDefault(x => x.Source == command.Object);
            // 物体可以在场景中可见，但尚未满足调查条件；Collider 不决定业务许可。
            if (definition == null || !rules.CanDiscover(state, definition))
            {
                events.Add(new InspectionFailed(command.Object,
                    definition == null ? "未知物体。" : definition.LockedDescription));
                return false;
            }
            GrantClues(state, definition.Grants, events);
            // 即使没有新发现，重复观察仍然可以再次阅读描述。
            events.Add(new ObjectInspected(command.Object, definition.Description));
            return true;
        }

        /// <summary>检查已认识的来源与世界条件，只有明确的细节点击才授予观察。</summary>
        private bool Observe(GameState state, ObserveDetail command, List<DomainEvent> events)
        {
            var definition = rules.Observations.FirstOrDefault(x => x.Source == command.Source && x.DetailId == command.DetailId);
            if (definition == null || !state.Knows(command.Source) || !definition.Requires.All(state.HasFact))
            {
                events.Add(new InspectionFailed(command.Source, definition == null ? "未知观察细节。" : !state.Knows(command.Source) ? "请先亲自观察这件物体。" : definition.LockedDescription));
                return false;
            }
            // 重复点细节仍可阅读描述，但 HashSet 保证线索与发现提示只新增一次。
            GrantClues(state, definition.Grants, events);
            events.Add(new ObjectInspected(command.Source, definition.Description));
            return true;
        }

        /// <summary>验证亲手操作的真实来源；同批记录事实和当场观察，不完成重要解释。</summary>
        private bool Act(GameState state, PerformSceneAction command, List<DomainEvent> events)
        {
            var rule = rules.SceneActions.FirstOrDefault(x => x.Id == command.ActionId && x.Source == command.Source);
            bool allowed = rule != null && state.Knows(command.Source) && rule.AdditionalSources.All(state.Knows) && rule.Requires.All(state.HasFact)
                && !rule.Blocks.Any(state.HasFact) && !state.CanvasLocked;
            if (!allowed)
            {
                events.Add(new InspectionFailed(command.Source, rule == null ? "这里没有这个操作。" : rule.LockedDescription));
                return false;
            }
            // 重复命令不会追加重复事实，也不会刷新路线计数。
            if (rule.Produces.All(state.HasFact)) return true;
            events.Add(new ObjectInspected(command.Source, rule.Description));
            AddFacts(state, rule.Produces, events);
            // 有效动作和当场可见的结果同批记录，动画不负责延迟发放观察。
            GrantClues(state, rule.ProducedObservations, events);
            return true;
        }

        /// <summary>把旧的概念加物体输入适配为统一碎片组合，保持旧事件协议。</summary>
        private bool Interpret(GameState state, Interpret command, List<DomainEvent> events)
        {
            var modern = new List<DomainEvent>();
            bool accepted = Combine(state,
                new[] { new ClueId(command.Concept.Value), new ClueId(command.Target.Value) }, modern);
            if (!accepted)
                events.Add(new InterpretationFailed(command.Concept, command.Target, ((CombinationFailed)modern[0]).Reason));
            else
            {
                // 替换首个成功事件，其后的事实事件保持原有次序。
                events.Add(new InterpretationSucceeded(command.Concept, command.Target));
                events.AddRange(modern.Skip(1));
            }
            return accepted;
        }

        /// <summary>无序精确匹配一个配方，校验已知输入、前置事实和完成记录。</summary>
        private bool Combine(GameState state, IReadOnlyList<ClueId> clues, List<DomainEvent> events)
        {
            // RuleSet 构造时已拒绝歧义输入，因此最多只能命中一个配方。
            var recipe = rules.Recipes.SingleOrDefault(x => x.Matches(clues));
            string reason = clues.Count > rules.MaximumCombinationInputs ? (rules.MaximumCombinationInputs == 2 ? "组合区一次只能放两个词语。" : "组合区一次最多放三个词语。")
                : clues.Any(x => !state.Knows(x)) ? "请先观察房间，收集这些碎片。"
                : clues.Any(x => !state.CanUseClue(x)) ? "这些词语已经用于形成新的联系。"
                : recipe == null ? "没有形成新的解释。"
                : !recipe.Requires.All(state.HasFact) ? "所需的世界事实尚未成立。"
                : !state.Allows(recipe.Disposition) ? "先把画中最后的天空补完。"
                : state.HasCompleted(recipe.Id) ? "这个解释已经存在于房间中。" : null;
            if (reason != null)
            {
                events.Add(new CombinationFailed(reason));
                return false;
            }

            // 内容显式声明哪些输入会消费；只在成功分支写入，来源知识不会因此丢失。
            state.Complete(recipe.Id, recipe.Disposition);
            foreach (var clue in recipe.ConsumedInputs) state.ConsumeClue(clue);
            events.Add(new CombinationSucceeded(recipe.Id, recipe.ProducesClues, recipe.ConsumedInputs));
            ConsumeRequiredLogics(state, recipe, events);
            // 两类输出都由数据驱动；线索先发布，随后事实进入同一个反应闭包。
            GrantClues(state, recipe.ProducesClues, events);
            AddFacts(state, recipe.ProducesFacts, events);
            return true;
        }

        /// <summary>日序推进后应用到期效果；正常反应随后继续求值到稳定。</summary>
        private bool Advance(GameState state, List<DomainEvent> events)
        {
            if (state.PendingDeferredEffects.Count == 0)
            {
                events.Add(new DayAdvanceFailed("先让一个解释留在房间里，再结束今天。"));
                return false;
            }
            state.AdvanceDay(); events.Add(new DayAdvanced(state.DayIndex));
            // 使用快照后移除，不在枚举可变队列时修改其内容。
            foreach (var effect in state.PendingDeferredEffects.Where(x => x.DueDay <= state.DayIndex).ToArray())
            {
                state.Consume(effect.ReactionId);
                events.Add(new ReactionTriggered(effect.ReactionId));
                AddFacts(state, effect.Produces, events);
            }
            return true;
        }

        /// <summary>全量扫描反应直到稳定；内容定义顺序不影响最终事实闭包。</summary>
        private void EvaluateReactions(GameState state, List<DomainEvent> events)
        {
            bool fired;
            do
            {
                fired = false;
                foreach (var rule in rules.Reactions)
                {
                    // 未满足叙事门槛时不消耗一次性身份，旧海画可以在后来的偏离出现后外溢。
                    if (state.HasFired(rule.Id) || !rule.Requires.All(state.HasFact)
                        || state.DeviationCount < rule.MinimumDeviations || (rule.RequiresNoDeviation && state.HasDeviated)) continue;
                    // 先标记后产出，保证循环 A→B→A 也无法重复触发同一反应。
                    state.Fire(rule.Id);
                    if (rule.DelayDays == 0)
                    {
                        events.Add(new ReactionTriggered(rule.Id));
                        AddFacts(state, rule.Produces, events);
                    }
                    else if (!rule.Produces.All(state.HasFact))
                    {
                        // 不等待几秒，不延后领域的判断：现在登记到指定日序。
                        int dueDay = checked(state.DayIndex + rule.DelayDays);
                        state.Schedule(new PendingDeferredEffect(rule.Id, dueDay, rule.Produces));
                        events.Add(new ReactionScheduled(rule.Id, dueDay));
                    }
                    // 预设已包含全部延迟输出时，只标记反应，不重新安排未来再发生。
                    fired = true;
                }
                // 每个非空轮次至少消耗一个未触发反应，轮次最多受反应总数限制。
                // 即使本轮产生了已有事实，也只再扫描到没有新反应为止。
            } while (fired);
        }

        /// <summary>统一授予线索并保持兼容事件；发现与配方产出都在这里去重。</summary>
        private void GrantClues(GameState state, IEnumerable<ClueId> clues, List<DomainEvent> events)
        {
            foreach (var clue in clues)
            {
                // 同一观察反复点击或多条规则输出同一概念，都只发一次新发现。
                if (!state.Discover(clue)) continue;
                events.Add(new ClueDiscovered(clue));
                var category = rules.Clues.Single(x => x.Id == clue).Category;
                if (category == ClueCategory.Object) events.Add(new ObjectDiscovered(new ObjectId(clue.Value)));
                if (category == ClueCategory.Concept) events.Add(new ConceptDiscovered(new ConceptId(clue.Value)));
            }
            EvaluateLogics(state, events);
        }

        /// <summary>在每次发现后检查灵感；只由已知碎片取得，不能由 UI 或动画提前写入。</summary>
        private void EvaluateLogics(GameState state, List<DomainEvent> events)
        {
            foreach (var logic in rules.Logics)
            {
                // 已使用的灵感不应因之后发现新词条而重新出现；未使用时仍由 HashSet 去重。
                if (state.HasConsumedLogic(logic.Id) || !logic.RequiresClues.All(state.Knows) || !state.AddFact(logic.UnlockFact)) continue;
                events.Add(new LogicDiscovered(logic.Id));
                events.Add(new FactAdded(logic.UnlockFact));
            }
        }

        /// <summary>消耗本次配方显式要求的灵感，普通世界事实不会被此方法移除。</summary>
        private void ConsumeRequiredLogics(GameState state, SemanticRecipe recipe, List<DomainEvent> events)
        {
            foreach (var logic in rules.Logics.Where(x => recipe.Requires.Contains(x.UnlockFact)))
            {
                // 配方许可已在 Combine 的前置校验中确认；两道去重都用于兼容异常预设恢复。
                if (!state.RemoveFact(logic.UnlockFact)) continue;
                state.ConsumeLogic(logic.Id);
                events.Add(new LogicConsumed(logic.Id));
            }
        }

        /// <summary>唯一的批量事实添加出口，只为第一次成立的事实产生事件。</summary>
        private static void AddFacts(GameState state, IEnumerable<FactId> facts, List<DomainEvent> events)
        {
            foreach (var fact in facts)
                if (state.AddFact(fact)) events.Add(new FactAdded(fact));
        }
    }
}
