// =============================================================================
// 文件：WorldPresenter.cs
// 职责：为所有世界表现提供事件绑定与状态恢复边界。
// 边界：仅消费事实；禁止写入 GameState 或执行 Reaction。
// =============================================================================
using System;
using Game.Application;
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>为所有世界表现提供事件绑定与状态恢复边界。</summary>
    public abstract class WorldPresenter : MonoBehaviour
    {
        /// <summary>当前会话引用；重开与跳转会整体替换。</summary>
        protected GameSession Session { get; private set; }
        private Func<FactId, bool> shouldPresent;
        /// <summary>解除旧会话订阅，绑定新会话并立即恢复最终状态。</summary>
        /// <param name="session">本次游戏唯一的应用层会话，由启动组件显式提供。</param>
        /// <remarks>恢复不重播动画；调用方应在 Presenter 的 Awake 缓存初始姿态后绑定。</remarks>
        public void Bind(GameSession session, Func<FactId, bool> filter = null)
        {
            // 先解绑后绑定，重开游戏不会叠加同一组件的事件监听。
            if (Session != null) Session.EventRaised -= OnEvent;
            Session = session; shouldPresent = filter; Session.EventRaised += OnEvent; ApplyState(Session.State, false);
        }
        /// <summary>仅转发新增事实事件，不消费 UI 或动画命令。</summary>
        private void OnEvent(DomainEvent e) { if (e is FactAdded fact && (shouldPresent == null || shouldPresent(fact.Fact))) OnFact(fact.Fact); }
        /// <summary>由具体表现组件筛选自己负责的事实。</summary>
        /// <param name="fact">第一次成立的事实。组件只能改变自身负责的视觉与音频。</param>
        protected abstract void OnFact(FactId fact);
        /// <summary>从领域状态设置表现；animate 为 false 时必须立即到达最终姿态。</summary>
        /// <param name="state">只读查询的世界状态，禁止从表现组件写入。</param>
        /// <param name="animate">为 true 时设置动画目标；为 false 时直接恢复终态并取消旧过渡。</param>
        /// <remarks>Update 只推进这里设置的目标，不计算任何前置条件或 Reaction。</remarks>
        public abstract void ApplyState(GameState state, bool animate);
        /// <summary>销毁时解除订阅，避免会话保留失效组件。</summary>
        protected virtual void OnDestroy() { if (Session != null) Session.EventRaised -= OnEvent; }
    }
}
