using Assessment_Agit.Domain.Enums;

namespace Assessment_Agit.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AccessRequestId { get; set; }
    public AccessRequest AccessRequest { get; set; } = null!;

    public string Action { get; set; } = string.Empty;

    public Guid ActorId { get; set; }
    public User Actor { get; set; } = null!;

    public RequestStatus? FromStatus { get; set; }
    public RequestStatus ToStatus { get; set; }

    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

