// =============================================================================
// 文件：SceneEntity.cs
// 职责：将 Inspector 中的字符串连接为稳定 ObjectId。
// 边界：仅是领域身份适配器，不保存事实或配方状态。
// =============================================================================
using Game.Domain;
using UnityEngine;
namespace Game.Unity
{
    /// <summary>将 Inspector 中的字符串连接为稳定 ObjectId。</summary>
    public sealed class SceneEntity : MonoBehaviour
    {
        // 保持序列化字段名稳定；换模型或翻译显示名不得改变此 ID。
        [SerializeField] private string objectId;
        /// <summary>内容的稳定身份，用于查找与去重。</summary>
        public ObjectId Id => new ObjectId(objectId);
        /// <summary>保存稳定物体字符串 ID，供 Inspector 与 Prefab 序列化。</summary>
        public void Configure(ObjectId id) { objectId = id.Value; }
    }
}
