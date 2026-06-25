using System.Collections.ObjectModel;

namespace ShrimpCam.Core.Audit;

public sealed record AuditEventRequest(
    string EventType,
    string ActorUserName,
    string Outcome,
    IReadOnlyDictionary<string, string> Detail)
{
    public AuditEventRequest(string eventType, string actorUserName, string outcome)
        : this(eventType, actorUserName, outcome, ReadOnlyDictionary<string, string>.Empty)
    {
    }
}
