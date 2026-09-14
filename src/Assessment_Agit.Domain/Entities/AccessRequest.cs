using System.ComponentModel.DataAnnotations;
using Assessment_Agit.Domain.Enums;

namespace Assessment_Agit.Domain.Entities;

public class AccessRequest
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Client-supplied idempotency key.
    /// </summary>
    public string ClientRequestId { get; set; } = string.Empty;

    public Guid RequesterId { get; set; }
    public User Requester { get; set; } = null!;

    public Guid ApplicationId { get; set; }
    public ApplicationMaster Application { get; set; } = null!;

    public EnvironmentType Environment { get; set; }
    public AccessLevel AccessLevel { get; set; }
    public string Justification { get; set; } = string.Empty;

    public RequestStatus Status { get; set; } = RequestStatus.PendingManager;
    public string? RejectReason { get; set; }

    public string PolicyVersion { get; set; } = "v1";

    /// <summary>
    /// Optimistic concurrency token.
    /// </summary>
    [ConcurrencyCheck]
    public int RowVersion { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsHighRisk => Environment == EnvironmentType.Production || AccessLevel == AccessLevel.Admin;

    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

