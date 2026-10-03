using System;
namespace Game.Domain
{
    public readonly struct ObjectId : IEquatable<ObjectId>
    {
        public string Value { get; }
        public ObjectId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("ID cannot be empty.", nameof(value));
            Value = value;
        }
        public bool Equals(ObjectId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ObjectId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "";
        public static bool operator ==(ObjectId a, ObjectId b) => a.Equals(b);
        public static bool operator !=(ObjectId a, ObjectId b) => !a.Equals(b);
    }
}
