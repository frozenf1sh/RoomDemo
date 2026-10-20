// =============================================================================
// 文件：GameSession.cs
// 职责：应用层会话入口，持有领域状态、执行命令并依序发布领域事件。
// 边界：不依赖 Unity；UI 与输入通过此对象进入规则层，不能直接修改状态集合。
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;

namespace Game.Application
{
    /// <summary>一次游戏的显式依赖入口；重开游戏时创建新实例。</summary>
    public sealed partial class GameSession
    {
        // 引擎仅处理规则；会话负责事件发布，使领域测试不需要 Unity 或事件框架。
        private readonly RuleEngine engine;
        /// <summary>本会话的只读内容规则。</summary>
        public RuleSet Rules { get; }
        /// <summary>世界状态的唯一事实来源；外部仅能读取只读快照与查询。</summary>
        public GameState State { get; }
        /// <summary>命令完成后依序发布的领域事件。订阅方负责解除订阅。</summary>
        public event Action<DomainEvent> EventRaised;

        /// <summary>以给定规则创建没有任何发现、事实或历史的新游戏。</summary>
        /// <param name="rules">关卡内容数据；由启动组件显式传入。</param>
        public GameSession(RuleSet rules)
        {
            Rules = rules;
            State = new GameState(rules.Clues, rules.CanvasLockFact);
            engine = new RuleEngine(rules);
        }

        /// <summary>调查场景物体；可否发现内容完全由领域规则决定。</summary>
        /// <param name="id">SceneEntity 提供的稳定物体 ID。</param>
        /// <returns>发现、观察或失败事件。</returns>
        public IReadOnlyList<DomainEvent> Inspect(ObjectId id) => Dispatch(new InspectObject(id));

        /// <summary>来自检视模型许可后的细节点击；不能直接传入任意产出线索。</summary>
        public IReadOnlyList<DomainEvent> ObserveDetail(ObjectId source, string detail) => Dispatch(new ObserveDetail(source, detail));

        /// <summary>玩家对已认识的物体进行明确操作；许可由领域数据判断。</summary>
        public IReadOnlyList<DomainEvent> PerformAction(ObjectId source, string actionId) => Dispatch(new PerformSceneAction(source, actionId));

        /// <summary>第一版二元解释入口，内部适配为统一的碎片组合。</summary>
        /// <param name="concept">旧概念 ID。</param>
        /// <param name="target">旧目标物体 ID。</param>
        /// <returns>兼容解释事件及新增事实、反应事件。</returns>
        public IReadOnlyList<DomainEvent> Interpret(ConceptId concept, ObjectId target) => Dispatch(new Interpret(concept, target));

        /// <summary>组合两个或三个碎片；调用方无需知道配方或反应。</summary>
        /// <param name="clues">本次提交的碎片快照，顺序不影响匹配。</param>
        /// <returns>组合结果与规则闭包的完整事件序列。</returns>
        public IReadOnlyList<DomainEvent> Combine(IReadOnlyList<ClueId> clues) => Dispatch(new Combine(clues));

        /// <summary>只读预览当前可成立的精确配方；画布用结果提示，不复制匹配规则。</summary>
        /// <param name="clues">画布中相互靠近的两个或三个线索。</param>
        /// <returns>尚未完成且前置条件成立的配方，或空引用；不产生事件或状态修改。</returns>
        public SemanticRecipe PreviewCombination(IReadOnlyList<ClueId> clues)
        {
            // 与命令共享同一个匹配方法及可用性查询；最终提交仍由领域再次校验。
            return Rules.AvailableRecipes(State).SingleOrDefault(recipe => recipe.Matches(clues));
        }

        /// <summary>查询物体当前是否可调查，不读取或改变场景 Collider。</summary>
        public bool IsInspectable(ObjectId id) => Rules.IsInspectable(State, id);
        /// <summary>查询有限操作的事实条件，表现层不能复制动作配方。</summary>
        public bool IsActionAvailable(ObjectId source, string actionId)
        {
            var action = Rules.SceneActions.SingleOrDefault(x => x.Id == actionId && x.Source == source);
            return action != null && !State.CanvasLocked && IsInspectable(source)
                && action.AdditionalSources.All(State.Knows) && action.Requires.All(State.HasFact) && !action.Blocks.Any(State.HasFact)
                && !action.Produces.All(State.HasFact);
        }

        /// <summary>有待执行的解释时，玩家可以选择结束今天。</summary>
        public bool CanAdvanceDay => State.HasPendingDeferredEffects;
        /// <summary>玩家明确推进日序；立即计算次日事实，视觉过渡独立进行。</summary>
        public IReadOnlyList<DomainEvent> AdvanceDay() => Dispatch(new AdvanceDay());

        /// <summary>调试授予全部已定义碎片，不自动解谜。</summary>
        public IReadOnlyList<DomainEvent> DebugGrantAllClues() => Publish(engine.DebugGrantClues(State, Rules.Clues.Select(x => x.Id).ToArray()));

        /// <summary>调试授予指定碎片；只供开发快进使用，不改变正常调查许可。</summary>
        /// <param name="clues">必须是当前规则集中已定义的稳定碎片。</param>
        /// <returns>本次实际新增的发现事件；重复碎片由领域去重。</returns>
        public IReadOnlyList<DomainEvent> DebugGrantClues(IEnumerable<ClueId> clues)
        {
            if (clues == null) throw new ArgumentNullException(nameof(clues));
            return Publish(engine.DebugGrantClues(State, clues.ToArray()));
        }

        /// <summary>调试添加一个合法事实，同时计算后续自动反应。</summary>
        public IReadOnlyList<DomainEvent> DebugAddFact(FactId fact) => Publish(engine.DebugAddFacts(State, new[] { fact }));

        /// <summary>创建已恢复的会话；恢复期间不向外发布过渡事件。</summary>
        /// <param name="rules">与正常游戏相同的规则集合。</param>
        /// <param name="preset">调试预设的数据定义。</param>
        /// <returns>可立即绑定 UI 与 Presenter 的新会话。</returns>
        public static GameSession FromPreset(RuleSet rules, DebugPreset preset)
        {
            // 先恢复后暴露：无效预设不会替换 Unity 中正在运行的旧会话。
            var session = new GameSession(rules);
            session.engine.RestorePreset(session.State, preset);
            return session;
        }

        /// <summary>按引擎返回的次序发布事件；不构建通用事件总线。</summary>
        private IReadOnlyList<DomainEvent> Publish(IReadOnlyList<DomainEvent> events)
        {
            foreach (var e in events) EventRaised?.Invoke(e);
            return events;
        }

        /// <summary>将外部输入转交领域，再通知表现层。</summary>
        private IReadOnlyList<DomainEvent> Dispatch(ICommand command)
        {
            // 发布时整个闭包已经计算完成，订阅者查询的是本条命令的最终状态。
            var events = engine.Execute(State, command);
            return Publish(events);
        }
    }
}
