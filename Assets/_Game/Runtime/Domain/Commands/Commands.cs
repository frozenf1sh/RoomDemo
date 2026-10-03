namespace Game.Domain
{
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
