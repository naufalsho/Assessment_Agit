using Assessment_Agit.Domain.Enums;

namespace Assessment_Agit.Application.DTOs;

public class CreateAccessRequestDto
{
    public string ClientRequestId { get; set; } = string.Empty;
    public Guid ApplicationId { get; set; }
    public EnvironmentType Environment { get; set; }
    public AccessLevel AccessLevel { get; set; }
    public string Justification { get; set; } = string.Empty;
}

public class ApprovalDecisionDto
{
    public int RowVersion { get; set; }
    public bool IsApproved { get; set; }
    public string? RejectReason { get; set; }
}

public class AccessRequestDetailDto
{
    public Guid Id { get; set; }
    public string ClientRequestId { get; set; } = string.Empty;
    public Guid RequesterId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;

    public Guid ApplicationId { get; set; }
    public string ApplicationCode { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;

    public EnvironmentType Environment { get; set; }
    public AccessLevel AccessLevel { get; set; }
    public string Justification { get; set; } = string.Empty;

    public RequestStatus Status { get; set; }
    public string? RejectReason { get; set; }
    public string PolicyVersion { get; set; } = string.Empty;
    public int RowVersion { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsHighRisk { get; set; }
    public bool CanCurrentUserApprove { get; set; }
    public string AssignedApproverRole { get; set; } = string.Empty;
    public string AssignedApproverName { get; set; } = string.Empty;

    public List<AuditLogDto> AuditLogs { get; set; } = new();
}

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public RequestStatus? FromStatus { get; set; }
    public RequestStatus ToStatus { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid? ManagerId { get; set; }
}

public class ApplicationDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid SystemOwnerId { get; set; }
    public string SystemOwnerName { get; set; } = string.Empty;
}

