namespace Game.Domain
{
    public abstract class DomainEvent { }
    public sealed class ObjectDiscovered : DomainEvent
    {
        public ObjectId Object { get; }
        public ObjectDiscovered(ObjectId id) { Object = id; }
        public override string ToString() => $"ObjectDiscovered({Object})";
    }
    public sealed class ConceptDiscovered : DomainEvent
    {
        public ConceptId Concept { get; }
        public ConceptDiscovered(ConceptId id) { Concept = id; }
        public override string ToString() => $"ConceptDiscovered({Concept})";
    }
    public sealed class ObjectInspected : DomainEvent
    {
        public ObjectId Object { get; }
        public string Description { get; }
        public ObjectInspected(ObjectId id, string description) { Object = id; Description = description; }
        public override string ToString() => $"ObjectInspected({Object})";
    }
    public sealed class InspectionFailed : DomainEvent
    {
        public ObjectId Object { get; }
        public string Reason { get; }
        public InspectionFailed(ObjectId id, string reason) { Object = id; Reason = reason; }
        public override string ToString() => $"InspectionFailed({Object}: {Reason})";
    }
    public sealed class InterpretationSucceeded : DomainEvent
    {
        public ConceptId Concept { get; }
        public ObjectId Target { get; }
        public InterpretationSucceeded(ConceptId concept, ObjectId target) { Concept = concept; Target = target; }
        public override string ToString() => $"InterpretationSucceeded({Concept}, {Target})";
    }
    public sealed class InterpretationFailed : DomainEvent
    {
        public ConceptId Concept { get; }
        public ObjectId Target { get; }
        public string Reason { get; }
        public InterpretationFailed(ConceptId concept, ObjectId target, string reason) { Concept = concept; Target = target; Reason = reason; }
        public override string ToString() => $"InterpretationFailed({Concept}, {Target}: {Reason})";
    }
    public sealed class FactAdded : DomainEvent
    {
        public FactId Fact { get; }
        public FactAdded(FactId fact) { Fact = fact; }
        public override string ToString() => $"FactAdded({Fact})";
    }
    public sealed class ReactionTriggered : DomainEvent
    {
        public string Id { get; }
        public ReactionTriggered(string id) { Id = id; }
        public override string ToString() => $"ReactionTriggered({Id})";
    }
}
