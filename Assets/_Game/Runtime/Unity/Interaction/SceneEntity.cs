using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    public sealed class SceneEntity : MonoBehaviour
    {
        [SerializeField] private string objectId;
        public ObjectId Id => new ObjectId(objectId);
        public void Configure(ObjectId id) { objectId = id.Value; }
    }
}
