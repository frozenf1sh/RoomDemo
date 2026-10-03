namespace Game.Domain
{
    public sealed class Combine : ICommand
    {
        public System.Collections.Generic.IReadOnlyList<ClueId> Clues { get; }
        public Combine(System.Collections.Generic.IReadOnlyList<ClueId> clues) { Clues = System.Array.AsReadOnly(System.Linq.Enumerable.ToArray(clues)); }
    }
    public interface ICommand { }
    public readonly struct InspectObject : ICommand
    {
        public ObjectId Object { get; }
        public InspectObject(ObjectId id) { Object = id; }
    }
    public readonly struct Interpret : ICommand
    {
        public ConceptId Concept { get; }
        public ObjectId Target { get; }
        public Interpret(ConceptId concept, ObjectId target) { Concept = concept; Target = target; }
    }
}
