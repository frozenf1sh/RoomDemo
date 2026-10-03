// =============================================================================
// 文件：ObjectId.cs
// 职责：场景物体标识符，连接 SceneEntity 与发现规则。
// 边界：保持与场景资产一致的稳定 ID；不持有 GameObject。
// =============================================================================
using System;
namespace Game.Domain
{
    /// <summary>与具体场景实现无关的物体身份。</summary>
    public readonly struct ObjectId : IEquatable<ObjectId>
    {
        /// <summary>用于值相等、哈希和绑定的稳定字符串；不是中文显示名。</summary>
        public string Value { get; }
        /// <summary>创建非空的稳定身份；显示名变化不改变身份。</summary>
        /// <param name="value">内容协议中的非空字符串。</param>
        /// <exception cref="ArgumentException">输入为空或仅含空白。</exception>
        public ObjectId(string value)
        {
            // 拒绝空身份，避免不同未配置场景实体被误认为同一个对象。
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("标识符不能为空。", nameof(value));
            Value = value;
        }
        // 使用序号比较保证跨语言环境一致；中文显示名不进入比较。
        /// <summary>按稳定字符串进行值相等比较。</summary>
        public bool Equals(ObjectId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ObjectId other && Equals(other);
        // readonly struct 可能存在默认值，哈希函数需安全处理其空字符串字段。
        /// <summary>使用与相等判断一致的序号哈希。</summary>
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        /// <summary>返回调试用原始 ID；默认值返回空字符串。</summary>
        public override string ToString() => Value ?? "";
        public static bool operator ==(ObjectId a, ObjectId b) => a.Equals(b);
        public static bool operator !=(ObjectId a, ObjectId b) => !a.Equals(b);
    }
}
