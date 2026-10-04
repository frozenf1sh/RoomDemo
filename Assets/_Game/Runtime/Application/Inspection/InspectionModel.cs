// =============================================================================
// 文件：InspectionModel.cs
// 职责：以小型状态表处理开合、翻页与观察热点，允许在普通 C# 中验证检视路径。
// 边界：状态只属于当前检视交互，不是世界事实；不依赖 Unity，不授予线索。
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
namespace Game.Application
{
    /// <summary>有限的检视动作；没有物理抓取或通用冒险交互语言。</summary>
    public enum InspectionAction { Observe, Open, Close, Next, Previous, Flip, Reveal }

    /// <summary>一个热点在什么页可用，以及动作后的临时状态和观察来源。</summary>
    public sealed class InspectionActionDefinition
    {
        /// <summary>近景热点稳定身份，与场景热点组件一一对应。</summary>
        public string Id { get; }
        /// <summary>当前动作的中文提示；不描述世界规则是否已经成立。</summary>
        public string Hint { get; }
        /// <summary>动作类别，只表达少量有限交互。</summary>
        public InspectionAction Action { get; }
        /// <summary>此动作允许执行的临时状态快照。</summary>
        public IReadOnlyList<string> RequiresStates { get; }
        /// <summary>非空时动作切换到该页／状态；观察通常保持原状态。</summary>
        public string NextState { get; }
        /// <summary>交给会话的细节 ID；这里只声明输入，不授予线索。</summary>
        public string ObservationId { get; }
        /// <summary>复制可用状态；观察 ID 只是命令输入，领域仍会验证其发现规则。</summary>
        public InspectionActionDefinition(string id, string hint, InspectionAction action, string[] states, string next = null, string observation = null)
        {
            Id = id; Hint = hint; Action = action;
            RequiresStates = Array.AsReadOnly((string[])states.Clone());
            NextState = next; ObservationId = observation;
        }
    }

    /// <summary>一件检视物的固定状态及热点定义，所有集合均为只读快照。</summary>
    public sealed class InspectionDefinition
    {
        /// <summary>每次新检视的起点；本阶段不持久化书页位置。</summary>
        public string InitialState { get; }
        /// <summary>所有可用页或交互状态。</summary>
        public IReadOnlyList<string> States { get; }
        /// <summary>按状态控制可执行热点的只读表。</summary>
        public IReadOnlyList<InspectionActionDefinition> Actions { get; }
        /// <summary>构建有限状态表，同时拒绝重复热点与不存在的前置或目标状态。</summary>
        /// <exception cref="ArgumentException">初始状态或动作状态不存在，或热点 ID 重复。</exception>
        public InspectionDefinition(string initial, string[] states, InspectionActionDefinition[] actions)
        {
            // 在进入交互前拒绝重复热点与无法到达的目标状态，避免运行时“翻丢页”。
            if (!states.Contains(initial) || actions.Select(x => x.Id).Distinct().Count() != actions.Length ||
                actions.Any(x => x.RequiresStates.Any(s => !states.Contains(s)) || (x.NextState != null && !states.Contains(x.NextState))))
                throw new ArgumentException("检视状态或热点定义无效。");
            InitialState = initial; States = Array.AsReadOnly((string[])states.Clone());
            Actions = Array.AsReadOnly((InspectionActionDefinition[])actions.Clone());
        }
    }

    /// <summary>每次拿近物体时创建的临时交互状态；退出后可以丢弃。</summary>
    public sealed class InspectionModel
    {
        /// <summary>这一检视实例使用的静态内容表。</summary>
        public InspectionDefinition Definition { get; }
        /// <summary>模型拥有的当前状态，适配器只能通过 TryAct 推进。</summary>
        public string State { get; private set; }
        /// <summary>新检视从定义的初始状态开始，不从网格 activeSelf 反推页状态。</summary>
        public InspectionModel(InspectionDefinition definition) { Definition = definition; State = definition.InitialState; }
        /// <summary>只按临时状态判断热点许可，不读取网格或 Animator。</summary>
        public bool CanAct(string id) => Definition.Actions.Any(x => x.Id == id && x.RequiresStates.Contains(State));
        /// <summary>原子地验证并执行一个动作；失败不推进状态，也不输出观察 ID。</summary>
        /// <param name="id">用户点击热点对应的动作 ID。</param>
        /// <param name="action">成功时给适配器动作定义，失败时为 null。</param>
        /// <returns>当前状态允许此动作时返回 true；不会执行世界发现命令。</returns>
        public bool TryAct(string id, out InspectionActionDefinition action)
        {
            action = Definition.Actions.FirstOrDefault(x => x.Id == id && x.RequiresStates.Contains(State));
            if (action == null) return false;
            // 观察可以保持同一页；翻页只切换预先定义的有限状态。
            if (action.NextState != null) State = action.NextState;
            return true;
        }
    }
}
