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
        public string Id { get; }
        public string Hint { get; }
        public InspectionAction Action { get; }
        public IReadOnlyList<string> RequiresStates { get; }
        public string NextState { get; }
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
        public string InitialState { get; }
        public IReadOnlyList<string> States { get; }
        public IReadOnlyList<InspectionActionDefinition> Actions { get; }
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
        public InspectionDefinition Definition { get; }
        public string State { get; private set; }
        public InspectionModel(InspectionDefinition definition) { Definition = definition; State = definition.InitialState; }
        /// <summary>只按临时状态判断热点许可，不读取网格或 Animator。</summary>
        public bool CanAct(string id) => Definition.Actions.Any(x => x.Id == id && x.RequiresStates.Contains(State));
        /// <summary>原子地验证并执行一个动作；失败不推进状态，也不输出观察 ID。</summary>
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
