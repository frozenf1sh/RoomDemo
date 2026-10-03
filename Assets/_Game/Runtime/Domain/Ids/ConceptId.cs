using System;
namespace Game.Domain
{
    public readonly struct ConceptId : IEquatable<ConceptId>
    {
        public string Value { get; }
        public ConceptId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("ID cannot be empty.", nameof(value));
            Value = value;
        }
        public bool Equals(ConceptId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ConceptId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? "";
        public static bool operator ==(ConceptId a, ConceptId b) => a.Equals(b);
        public static bool operator !=(ConceptId a, ConceptId b) => !a.Equals(b);
    }
}
