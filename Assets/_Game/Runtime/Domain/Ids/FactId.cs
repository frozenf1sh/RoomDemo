using System;
namespace Game.Domain
{
    public readonly struct FactId : IEquatable<FactId>
    {
        public string Value { get; }
        public FactId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("ID cannot be empty.", nameof(value));
            Value = value;
        }
        public bool Equals(FactId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FactId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "";
        public static bool operator ==(FactId a, FactId b) => a.Equals(b);
        public static bool operator !=(FactId a, FactId b) => !a.Equals(b);
    }
}
