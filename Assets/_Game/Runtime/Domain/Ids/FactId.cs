// =============================================================================
// 文件：FactId.cs
// 职责：世界事实标识符，作为状态集合与规则条件的键。
// 边界：事实描述世界发生了什么，不指定 Unity 如何播放动画。
// =============================================================================
using System;
namespace Game.Domain
{
    /// <summary>领域事实身份；与表现形式无关。</summary>
    public readonly struct FactId : IEquatable<FactId>
    {
        /// <summary>用于值相等、哈希和绑定的稳定字符串；不是中文显示名。</summary>
        public string Value { get; }
        /// <summary>创建非空的稳定身份；显示名变化不改变身份。</summary>
        /// <param name="value">内容协议中的非空字符串。</param>
        /// <exception cref="ArgumentException">输入为空或仅含空白。</exception>
        public FactId(string value)
        {
            // 拒绝空身份，避免不同未配置场景实体被误认为同一个对象。
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("标识符不能为空。", nameof(value));
            Value = value;
        }
        // 使用序号比较保证跨语言环境一致；中文显示名不进入比较。
        /// <summary>按稳定字符串进行值相等比较。</summary>
        public bool Equals(FactId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FactId other && Equals(other);
        // readonly struct 可能存在默认值，哈希函数需安全处理其空字符串字段。
        /// <summary>使用与相等判断一致的序号哈希。</summary>
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        /// <summary>返回调试用原始 ID；默认值返回空字符串。</summary>
        public override string ToString() => Value ?? "";
        public static bool operator ==(FactId a, FactId b) => a.Equals(b);
        public static bool operator !=(FactId a, FactId b) => !a.Equals(b);
    }
}
