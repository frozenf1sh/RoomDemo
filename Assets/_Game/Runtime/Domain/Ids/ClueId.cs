using System;
namespace Game.Domain
{
    public readonly struct ClueId : IEquatable<ClueId>
    {
        public string Value { get; }
        public ClueId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("ID cannot be empty.", nameof(value));
            Value = value;
        }
        public bool Equals(ClueId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ClueId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "";
        public static bool operator ==(ClueId a, ClueId b) => a.Equals(b);
        public static bool operator !=(ClueId a, ClueId b) => !a.Equals(b);
    }
}
