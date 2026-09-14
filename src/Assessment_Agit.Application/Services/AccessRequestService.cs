using Assessment_Agit.Application.DTOs;
using Assessment_Agit.Application.Interfaces;
using Assessment_Agit.Domain.Entities;
using Assessment_Agit.Domain.Enums;
using Assessment_Agit.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Assessment_Agit.Application.Services;

public class AccessRequestService : IAccessRequestService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AccessRequestService>? _logger;

    public AccessRequestService(IAppDbContext context, ICurrentUserService currentUserService, ILogger<AccessRequestService>? logger = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<AccessRequestDetailDto> CreateRequestAsync(CreateAccessRequestDto dto, CancellationToken cancellationToken = default)
    {
        var currentUser = await _currentUserService.GetUserAsync(cancellationToken);
        if (currentUser == null)
        {
            throw new ForbiddenException("Authentication required to create access requests.");
        }

        // Business Rule 2.6: All requests mandate Manager approval; requester must have an assigned direct manager
        if (currentUser.ManagerId == null)
        {
            throw new DomainValidationException($"User '{currentUser.FullName}' ({currentUser.Role}) does not have an assigned direct manager in the hierarchy. By business rules, all access requests require a Manager approval.");
        }

        if (string.IsNullOrWhiteSpace(dto.ClientRequestId))
        {
            throw new DomainValidationException("ClientRequestId is required for idempotency.");
        }

        if (string.IsNullOrWhiteSpace(dto.Justification))
        {
            throw new DomainValidationException("Justification is mandatory.");
        }

        var application = await _context.Applications
            .Include(a => a.SystemOwner)
            .FirstOrDefaultAsync(a => a.Id == dto.ApplicationId, cancellationToken);

        if (application == null)
        {
            throw new DomainValidationException("The selected application does not exist.");
        }

        // Check for existing request with the same ClientRequestId (Idempotency)
        var existing = await _context.AccessRequests
            .Include(r => r.Requester).ThenInclude(u => u.Manager)
            .Include(r => r.Application).ThenInclude(a => a.SystemOwner)
            .Include(r => r.AuditLogs).ThenInclude(a => a.Actor)
            .FirstOrDefaultAsync(r => r.ClientRequestId == dto.ClientRequestId, cancellationToken);

        if (existing != null)
        {
            // If the same user submitted with identical attributes, return existing (Idempotent success)
            if (existing.RequesterId == currentUser.Id &&
                existing.ApplicationId == dto.ApplicationId &&
                existing.Environment == dto.Environment &&
                existing.AccessLevel == dto.AccessLevel)
            {
                _logger?.LogInformation("Idempotent request detected. Returning existing AccessRequest {RequestId} for ClientRequestId {ClientRequestId}.", existing.Id, dto.ClientRequestId);
                return MapToDetailDto(existing, currentUser);
            }

            throw new DomainValidationException("ClientRequestId has already been used with different request parameters.");
        }

        var request = new AccessRequest
        {
            Id = Guid.NewGuid(),
            ClientRequestId = dto.ClientRequestId.Trim(),
            RequesterId = currentUser.Id,
            ApplicationId = dto.ApplicationId,
            Environment = dto.Environment,
            AccessLevel = dto.AccessLevel,
            Justification = dto.Justification.Trim(),
            Status = RequestStatus.PendingManager,
            PolicyVersion = "v1",
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            AccessRequestId = request.Id,
            Action = "Created",
            ActorId = currentUser.Id,
            FromStatus = null,
            ToStatus = RequestStatus.PendingManager,
            Details = "Access request created and submitted for Manager approval.",
            Timestamp = DateTime.UtcNow
        };

        try
        {
            _context.AccessRequests.Add(request);
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
            _logger?.LogInformation("Successfully created AccessRequest {RequestId} for requester {UserId} (ClientRequestId: {ClientRequestId}).", request.Id, currentUser.Id, request.ClientRequestId);
        }
        }
        catch (DbUpdateException)
        {
            // Handle concurrent race for the same ClientRequestId unique constraint
            var raced = await _context.AccessRequests
                .Include(r => r.Requester).ThenInclude(u => u.Manager)
                .Include(r => r.Application).ThenInclude(a => a.SystemOwner)
                .Include(r => r.AuditLogs).ThenInclude(a => a.Actor)
                .FirstOrDefaultAsync(r => r.ClientRequestId == dto.ClientRequestId, cancellationToken);

            if (raced != null && raced.RequesterId == currentUser.Id)
            {
                return MapToDetailDto(raced, currentUser);
            }

            throw;
        }

        // Reload populated entity
        return await GetRequestByIdAsync(request.Id, cancellationToken);
    }

    public async Task<List<AccessRequestDetailDto>> GetMyRequestsAsync(CancellationToken cancellationToken = default)
    {
        var currentUser = await _currentUserService.GetUserAsync(cancellationToken);
        if (currentUser == null) return new();

        var requests = await _context.AccessRequests
            .Include(r => r.Requester).ThenInclude(u => u.Manager)
            .Include(r => r.Application).ThenInclude(a => a.SystemOwner)
            .Include(r => r.AuditLogs).ThenInclude(a => a.Actor)
            .Where(r => r.RequesterId == currentUser.Id)
            .ToListAsync(cancellationToken);

        return requests
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => MapToDetailDto(r, currentUser))
            .ToList();
    }

    public async Task<List<AccessRequestDetailDto>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default)
    {
        var currentUser = await _currentUserService.GetUserAsync(cancellationToken);
        if (currentUser == null) return new();

        var requests = await _context.AccessRequests
            .Include(r => r.Requester).ThenInclude(u => u.Manager)
            .Include(r => r.Application).ThenInclude(a => a.SystemOwner)
            .Include(r => r.AuditLogs).ThenInclude(a => a.Actor)
            .Where(r =>
                (r.Status == RequestStatus.PendingManager && r.Requester.ManagerId == currentUser.Id && r.RequesterId != currentUser.Id) ||
                (r.Status == RequestStatus.PendingSystemOwner && r.Application.SystemOwnerId == currentUser.Id && r.RequesterId != currentUser.Id))
            .ToListAsync(cancellationToken);

        return requests
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => MapToDetailDto(r, currentUser))
            .ToList();
    }

    public async Task<List<AccessRequestDetailDto>> GetAllRequestsAsync(CancellationToken cancellationToken = default)
    {
        var currentUser = await _currentUserService.GetUserAsync(cancellationToken);

        var requests = await _context.AccessRequests
            .Include(r => r.Requester).ThenInclude(u => u.Manager)
            .Include(r => r.Application).ThenInclude(a => a.SystemOwner)
            .Include(r => r.AuditLogs).ThenInclude(a => a.Actor)
            .ToListAsync(cancellationToken);

        return requests
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => MapToDetailDto(r, currentUser))
            .ToList();
    }

    public async Task<AccessRequestDetailDto> GetRequestByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var currentUser = await _currentUserService.GetUserAsync(cancellationToken);

        var request = await _context.AccessRequests
            .Include(r => r.Requester).ThenInclude(u => u.Manager)
            .Include(r => r.Application).ThenInclude(a => a.SystemOwner)
            .Include(r => r.AuditLogs).ThenInclude(a => a.Actor)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (request == null)
        {
            throw new NotFoundException("AccessRequest", id);
        }

        return MapToDetailDto(request, currentUser);
    }

    public async Task<AccessRequestDetailDto> ProcessApprovalAsync(Guid requestId, ApprovalDecisionDto decision, CancellationToken cancellationToken = default)
    {
        var currentUser = await _currentUserService.GetUserAsync(cancellationToken);
        if (currentUser == null)
        {
            throw new ForbiddenException("Authentication required to process approvals.");
        }

        var request = await _context.AccessRequests
            .Include(r => r.Requester).ThenInclude(u => u.Manager)
            .Include(r => r.Application).ThenInclude(a => a.SystemOwner)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request == null)
        {
            throw new NotFoundException("AccessRequest", requestId);
        }

        // Optimistic Concurrency check: verify client did not submit against a stale snapshot
        if (decision.RowVersion != request.RowVersion)
        {
            _logger?.LogWarning("Concurrency conflict detected on AccessRequest {RequestId}. Expected version {DbVersion}, but received {ClientVersion}.", requestId, request.RowVersion, decision.RowVersion);
            throw new ConcurrencyConflictException("This request has already been modified or approved by another user or session. Please refresh.");
        }

        // Business Rule: Terminal state check
        if (request.Status == RequestStatus.Approved || request.Status == RequestStatus.Rejected)
        {
            throw new DomainValidationException($"Cannot process request in terminal state '{request.Status}'.");
        }

        // Business Rule: Requester cannot approve their own request
        if (request.RequesterId == currentUser.Id)
        {
            _logger?.LogWarning("User {UserId} attempted prohibited self-approval on AccessRequest {RequestId}.", currentUser.Id, requestId);
            throw new ForbiddenException("Requesters are strictly prohibited from approving or rejecting their own requests.");
        }

        var fromStatus = request.Status;
        string auditAction;
        string auditDetails;

        if (request.Status == RequestStatus.PendingManager)
        {
            // Business Rule: Only direct manager can process
            if (request.Requester.ManagerId != currentUser.Id)
            {
                _logger?.LogWarning("User {UserId} unauthorized to approve Manager stage for AccessRequest {RequestId}.", currentUser.Id, requestId);
                throw new ForbiddenException("Only the requester's direct manager is authorized to approve this stage.");
            }

            if (!decision.IsApproved)
            {
                // Business Rule: Reject requires reason
                if (string.IsNullOrWhiteSpace(decision.RejectReason))
                {
                    throw new DomainValidationException("A rejection reason is mandatory.");
                }

                request.Status = RequestStatus.Rejected;
                request.RejectReason = decision.RejectReason.Trim();
                auditAction = "ManagerRejected";
                auditDetails = $"Manager rejected request. Reason: {request.RejectReason}";
            }
            else
            {
                // Check if High Risk
                if (request.IsHighRisk)
                {
                    request.Status = RequestStatus.PendingSystemOwner;
                    auditAction = "ManagerApproved";
                    auditDetails = "Manager approved high-risk request (Production or Admin). Escalated to System Owner for review.";
                }
                else
                {
                    request.Status = RequestStatus.Approved;
                    auditAction = "ManagerApproved";
                    auditDetails = "Manager approved non-high-risk request. Access granted.";
                }
            }
        }
        else if (request.Status == RequestStatus.PendingSystemOwner)
        {
            // Business Rule: Only designated System Owner can process
            if (request.Application.SystemOwnerId != currentUser.Id)
            {
                throw new ForbiddenException("Only the designated System Owner is authorized to approve this stage.");
            }

            if (!decision.IsApproved)
            {
                // Business Rule: Reject requires reason
                if (string.IsNullOrWhiteSpace(decision.RejectReason))
                {
                    throw new DomainValidationException("A rejection reason is mandatory.");
                }

                request.Status = RequestStatus.Rejected;
                request.RejectReason = decision.RejectReason.Trim();
                auditAction = "SystemOwnerRejected";
                auditDetails = $"System Owner rejected request. Reason: {request.RejectReason}";
            }
            else
            {
                request.Status = RequestStatus.Approved;
                auditAction = "SystemOwnerApproved";
                auditDetails = "System Owner approved request. Access granted.";
            }
        }
        else
        {
            throw new DomainValidationException($"Illegal state transition from {request.Status}.");
        }

        // Concurrency token increment
        request.RowVersion++;
        request.UpdatedAt = DateTime.UtcNow;

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            AccessRequestId = request.Id,
            Action = auditAction,
            ActorId = currentUser.Id,
            FromStatus = fromStatus,
            ToStatus = request.Status,
            Details = auditDetails,
            Timestamp = DateTime.UtcNow
        };

        _context.AuditLogs.Add(auditLog);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("A concurrency conflict occurred. The request was modified concurrently by another transaction.");
        }

        return await GetRequestByIdAsync(request.Id, cancellationToken);
    }

    public async Task<List<ApplicationDto>> GetApplicationsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Applications
            .Include(a => a.SystemOwner)
            .OrderBy(a => a.Name)
            .Select(a => new ApplicationDto
            {
                Id = a.Id,
                Code = a.Code,
                Name = a.Name,
                SystemOwnerId = a.SystemOwnerId,
                SystemOwnerName = a.SystemOwner.FullName
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<UserDto>> GetDemoUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .OrderBy(u => u.FullName)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                Role = u.Role,
                ManagerId = u.ManagerId
            })
            .ToListAsync(cancellationToken);
    }

    private static AccessRequestDetailDto MapToDetailDto(AccessRequest r, User? currentUser)
    {
        bool canApprove = false;
        string assignedRole = "None";
        string assignedName = "None";

        if (r.Status == RequestStatus.PendingManager)
        {
            assignedRole = "Manager";
            assignedName = r.Requester.Manager?.FullName ?? "Direct Manager";
            if (currentUser != null && r.Requester.ManagerId == currentUser.Id && r.RequesterId != currentUser.Id)
            {
                canApprove = true;
            }
        }
        else if (r.Status == RequestStatus.PendingSystemOwner)
        {
            assignedRole = "System Owner";
            assignedName = r.Application.SystemOwner?.FullName ?? "System Owner";
            if (currentUser != null && r.Application.SystemOwnerId == currentUser.Id && r.RequesterId != currentUser.Id)
            {
                canApprove = true;
            }
        }

        return new AccessRequestDetailDto
        {
            Id = r.Id,
            ClientRequestId = r.ClientRequestId,
            RequesterId = r.RequesterId,
            RequesterName = r.Requester.FullName,
            RequesterEmail = r.Requester.Email,
            ApplicationId = r.ApplicationId,
            ApplicationCode = r.Application.Code,
            ApplicationName = r.Application.Name,
            Environment = r.Environment,
            AccessLevel = r.AccessLevel,
            Justification = r.Justification,
            Status = r.Status,
            RejectReason = r.RejectReason,
            PolicyVersion = r.PolicyVersion,
            RowVersion = r.RowVersion,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
            IsHighRisk = r.IsHighRisk,
            CanCurrentUserApprove = canApprove,
            AssignedApproverRole = assignedRole,
            AssignedApproverName = assignedName,
            AuditLogs = r.AuditLogs
                .OrderBy(a => a.Timestamp)
                .Select(a => new AuditLogDto
                {
                    Id = a.Id,
                    Action = a.Action,
                    ActorName = a.Actor.FullName,
                    ActorEmail = a.Actor.Email,
                    FromStatus = a.FromStatus,
                    ToStatus = a.ToStatus,
                    Details = a.Details,
                    Timestamp = a.Timestamp
                }).ToList()
        };
    }
}

