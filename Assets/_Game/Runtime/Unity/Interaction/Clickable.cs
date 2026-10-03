using Game.Application;
using UnityEngine;
namespace Game.Unity
{
    [RequireComponent(typeof(SceneEntity), typeof(Collider))]
    public sealed class Clickable : MonoBehaviour
    {
        private SceneEntity entity;
        public SceneEntity Entity => entity != null ? entity : (entity = GetComponent<SceneEntity>());
        public void Inspect(GameSession session) { session.Inspect(Entity.Id); }
    }
}
