// =============================================================================
// 文件：Clickable.cs
// 职责：把命中的场景实体转交应用层调查。
// 边界：不决定可调查性、不发放线索、不直接控制其他物体。
// =============================================================================
using Game.Application;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>把命中的场景实体转交应用层调查。</summary>
    [RequireComponent(typeof(SceneEntity), typeof(Collider))]
    public sealed class Clickable : MonoBehaviour
    {
        // 缓存身份组件，点击只转交命令，不在适配器中授予概念。
        private SceneEntity entity;
        /// <summary>缓存当前 SceneEntity，仅提供交互身份。</summary>
        public SceneEntity Entity => entity != null ? entity : (entity = GetComponent<SceneEntity>());
        /// <summary>仅提交实体 ID，由 Session 决定能否发现内容。</summary>
        public void Inspect(GameSession session)
        {
            session.Inspect(Entity.Id);
            var details = GetComponent<WorldObservationSource>();
            if (details != null) details.ObserveAvailable(session, Entity.Id);
        }
    }
}
