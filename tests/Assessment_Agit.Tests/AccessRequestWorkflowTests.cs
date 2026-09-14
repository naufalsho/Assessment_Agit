using Assessment_Agit.Application.DTOs;
using Assessment_Agit.Application.Interfaces;
using Assessment_Agit.Application.Services;
using Assessment_Agit.Domain.Entities;
using Assessment_Agit.Domain.Enums;
using Assessment_Agit.Domain.Exceptions;
using Assessment_Agit.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Assessment_Agit.Tests;

public class TestCurrentUserService : ICurrentUserService
{
    private Guid _userId;
    private readonly AppDbContext _context;

    public TestCurrentUserService(AppDbContext context, Guid initialUserId)
    {
        _context = context;
        _userId = initialUserId;
    }

    public Guid? UserId => _userId;
    public string? UserEmail => null;

    public void SetUser(Guid userId, string userEmail = "")
    {
        _userId = userId;
    }

    public async Task<User?> GetUserAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users.Include(u => u.Manager).FirstOrDefaultAsync(u => u.Id == _userId, cancellationToken);
    }
}

public class AccessRequestWorkflowTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly AccessRequestService _service;

    public AccessRequestWorkflowTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        DbInitializer.EnsureSeededAsync(_context).GetAwaiter().GetResult();

        // Default active user is Alice (Requester)
        _currentUserService = new TestCurrentUserService(_context, DbInitializer.AliceId);
        _service = new AccessRequestService(_context, _currentUserService);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    // Scenario 1: Standard request (Alice -> CRM, NonProduction, Read. Bob approve -> Approved)
    [Fact]
    public async Task Scenario1_StandardRequest_AliceToBob_BecomesApproved()
    {
        // 1. Alice creates standard request
        var createDto = new CreateAccessRequestDto
        {
            ClientRequestId = Guid.NewGuid().ToString(),
            ApplicationId = DbInitializer.CrmAppId,
            Environment = EnvironmentType.NonProduction,
            AccessLevel = AccessLevel.Read,
            Justification = "Standard developer read access"
        };

        var created = await _service.CreateRequestAsync(createDto);
        Assert.Equal(RequestStatus.PendingManager, created.Status);
        Assert.False(created.IsHighRisk);

        // 2. Switch to Bob (Manager)
        _currentUserService.SetUser(DbInitializer.BobId);

        // 3. Bob approves
        var approved = await _service.ProcessApprovalAsync(created.Id, new ApprovalDecisionDto
        {
            IsApproved = true,
            RowVersion = created.RowVersion
        });

        // 4. Verify Approved (terminal) and audit trail
        Assert.Equal(RequestStatus.Approved, approved.Status);
        Assert.Equal(2, approved.AuditLogs.Count); // Created + ManagerApproved
        Assert.Contains(approved.AuditLogs, a => a.Action == "ManagerApproved");
    }

    // Scenario 2: Production request (Alice -> CRM, Production, Read. Bob approve -> waiting Carol; Carol approve -> Approved)
    [Fact]
    public async Task Scenario2_ProductionRequest_HighRisk_RequiresCarolSystemOwnerApproval()
    {
        // 1. Alice submits Production request (High Risk)
        var createDto = new CreateAccessRequestDto
        {
            ClientRequestId = Guid.NewGuid().ToString(),
            ApplicationId = DbInitializer.CrmAppId,
            Environment = EnvironmentType.Production, // High Risk!
            AccessLevel = AccessLevel.Read,
            Justification = "Investigating production database incident"
        };

        var created = await _service.CreateRequestAsync(createDto);
        Assert.Equal(RequestStatus.PendingManager, created.Status);
        Assert.True(created.IsHighRisk);

        // 2. Switch to Bob (Manager) and approve
        _currentUserService.SetUser(DbInitializer.BobId);
        var managerApproved = await _service.ProcessApprovalAsync(created.Id, new ApprovalDecisionDto
        {
            IsApproved = true,
            RowVersion = created.RowVersion
        });

        // Must escalate to PendingSystemOwner
        Assert.Equal(RequestStatus.PendingSystemOwner, managerApproved.Status);

        // 3. Switch to Carol (CRM System Owner) and approve
        _currentUserService.SetUser(DbInitializer.CarolId);
        var systemOwnerApproved = await _service.ProcessApprovalAsync(created.Id, new ApprovalDecisionDto
        {
            IsApproved = true,
            RowVersion = managerApproved.RowVersion
        });

        // Final status is Approved
        Assert.Equal(RequestStatus.Approved, systemOwnerApproved.Status);
        Assert.Equal(3, systemOwnerApproved.AuditLogs.Count); // Created, ManagerApproved, SystemOwnerApproved
    }

    // Scenario 3: Admin access request (Alice -> Finance Portal, NonProduction, Admin. Bob approve -> waiting Dana)
    [Fact]
    public async Task Scenario3_AdminAccess_HighRisk_EscalatesToDanaSystemOwner()
    {
        // 1. Alice requests Admin level access (High Risk)
        var createDto = new CreateAccessRequestDto
        {
            ClientRequestId = Guid.NewGuid().ToString(),
            ApplicationId = DbInitializer.FinancePortalAppId,
            Environment = EnvironmentType.NonProduction,
            AccessLevel = AccessLevel.Admin, // High Risk!
            Justification = "Configuration and role assignment testing"
        };

        var created = await _service.CreateRequestAsync(createDto);
        Assert.True(created.IsHighRisk);

        // 2. Bob approves
        _currentUserService.SetUser(DbInitializer.BobId);
        var managerApproved = await _service.ProcessApprovalAsync(created.Id, new ApprovalDecisionDto
        {
            IsApproved = true,
            RowVersion = created.RowVersion
        });

        Assert.Equal(RequestStatus.PendingSystemOwner, managerApproved.Status);

        // 3. Switch to Dana (Finance Portal System Owner) and approve
        _currentUserService.SetUser(DbInitializer.DanaId);
        var finalApproved = await _service.ProcessApprovalAsync(created.Id, new ApprovalDecisionDto
        {
            IsApproved = true,
            RowVersion = managerApproved.RowVersion
        });

        Assert.Equal(RequestStatus.Approved, finalApproved.Status);
    }

    // Scenario 4: Unauthorized approval (User not assigned approver, or requester self-approval -> rejected 403)
    [Fact]
    public async Task Scenario4_UnauthorizedApproval_ThrowsForbiddenException()
    {
        // 1. Alice submits request
        var createDto = new CreateAccessRequestDto
        {
            ClientRequestId = Guid.NewGuid().ToString(),
            ApplicationId = DbInitializer.CrmAppId,
            Environment = EnvironmentType.NonProduction,
            AccessLevel = AccessLevel.Read,
            Justification = "Check reports"
        };
        var created = await _service.CreateRequestAsync(createDto);

        // Case 4a: Alice tries to approve her own request
        await Assert.ThrowsAsync<ForbiddenException>(async () =>
        {
            await _service.ProcessApprovalAsync(created.Id, new ApprovalDecisionDto
            {
                IsApproved = true,
                RowVersion = created.RowVersion
            });
        });

        // Case 4b: Carol tries to approve Manager stage (Carol is CRM owner, not Alice's manager)
        _currentUserService.SetUser(DbInitializer.CarolId);
        await Assert.ThrowsAsync<ForbiddenException>(async () =>
        {
            await _service.ProcessApprovalAsync(created.Id, new ApprovalDecisionDto
            {
                IsApproved = true,
                RowVersion = created.RowVersion
            });
        });

        // Case 4c: Dana tries to approve CRM System Owner stage
        // First Bob approves high risk request
        var prodDto = new CreateAccessRequestDto
        {
            ClientRequestId = Guid.NewGuid().ToString(),
            ApplicationId = DbInitializer.CrmAppId,
            Environment = EnvironmentType.Production,
            AccessLevel = AccessLevel.Read,
            Justification = "Prod test"
        };
        _currentUserService.SetUser(DbInitializer.AliceId);
        var prodCreated = await _service.CreateRequestAsync(prodDto);

        _currentUserService.SetUser(DbInitializer.BobId);
        var stage2 = await _service.ProcessApprovalAsync(prodCreated.Id, new ApprovalDecisionDto
        {
            IsApproved = true,
            RowVersion = prodCreated.RowVersion
        });

        // Dana is System Owner for Finance Portal, not CRM
        _currentUserService.SetUser(DbInitializer.DanaId);
        await Assert.ThrowsAsync<ForbiddenException>(async () =>
        {
            await _service.ProcessApprovalAsync(stage2.Id, new ApprovalDecisionDto
            {
                IsApproved = true,
                RowVersion = stage2.RowVersion
            });
        });
    }

    // Scenario 5: Duplicate submit (Same ClientRequestId resent -> idempotent, returns same request)
    [Fact]
    public async Task Scenario5_DuplicateSubmit_IsIdempotent_DoesNotCreateSecondRow()
    {
        var clientRequestId = "IDEMP-" + Guid.NewGuid().ToString();
        var createDto = new CreateAccessRequestDto
        {
            ClientRequestId = clientRequestId,
            ApplicationId = DbInitializer.CrmAppId,
            Environment = EnvironmentType.NonProduction,
            AccessLevel = AccessLevel.Read,
            Justification = "Idempotency validation test"
        };

        // First submit
        var first = await _service.CreateRequestAsync(createDto);

        // Second submit with identical payload and ClientRequestId
        var second = await _service.CreateRequestAsync(createDto);

        // Must return identical request entity
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.ClientRequestId, second.ClientRequestId);

        // Verify database row count is strictly 1
        var totalCount = await _context.AccessRequests.CountAsync(r => r.ClientRequestId == clientRequestId);
        Assert.Equal(1, totalCount);
    }

    // Scenario 6: Concurrent action (Two actions on same version -> one succeeds, other gets 409 Conflict)
    [Fact]
    public async Task Scenario6_ConcurrentAction_OptimisticConcurrency_ThrowsConflict()
    {
        var createDto = new CreateAccessRequestDto
        {
            ClientRequestId = Guid.NewGuid().ToString(),
            ApplicationId = DbInitializer.CrmAppId,
            Environment = EnvironmentType.NonProduction,
            AccessLevel = AccessLevel.Read,
            Justification = "Concurrency conflict test"
        };
        var created = await _service.CreateRequestAsync(createDto);
        var initialVersion = created.RowVersion;

        // Approver 1 (Bob) processes with initialVersion
        _currentUserService.SetUser(DbInitializer.BobId);
        var approved = await _service.ProcessApprovalAsync(created.Id, new ApprovalDecisionDto
        {
            IsApproved = true,
            RowVersion = initialVersion
        });

        Assert.Equal(initialVersion + 1, approved.RowVersion);

        // Approver 2 tries to process with the stale initialVersion
        await Assert.ThrowsAsync<ConcurrencyConflictException>(async () =>
        {
            await _service.ProcessApprovalAsync(created.Id, new ApprovalDecisionDto
            {
                IsApproved = false,
                RowVersion = initialVersion, // Stale version!
                RejectReason = "Conflicting reject"
            });
        });
    }

    // Scenario 7: Rejected request (Reject reason stored, audit event logged, terminal state)
    [Fact]
    public async Task Scenario7_RejectedRequest_RequiresReason_BecomesTerminal()
    {
        var createDto = new CreateAccessRequestDto
        {
            ClientRequestId = Guid.NewGuid().ToString(),
            ApplicationId = DbInitializer.CrmAppId,
            Environment = EnvironmentType.NonProduction,
            AccessLevel = AccessLevel.Read,
            Justification = "Request that will be rejected"
        };
        var created = await _service.CreateRequestAsync(createDto);

        _currentUserService.SetUser(DbInitializer.BobId);

        // 7a: Rejecting without reason must be rejected
        await Assert.ThrowsAsync<DomainValidationException>(async () =>
        {
            await _service.ProcessApprovalAsync(created.Id, new ApprovalDecisionDto
            {
                IsApproved = false,
                RowVersion = created.RowVersion,
                RejectReason = "   " // Empty reason
            });
        });

        // 7b: Rejecting with reason succeeds
        var rejected = await _service.ProcessApprovalAsync(created.Id, new ApprovalDecisionDto
        {
            IsApproved = false,
            RowVersion = created.RowVersion,
            RejectReason = "Access not justified for this role"
        });

        Assert.Equal(RequestStatus.Rejected, rejected.Status);
        Assert.Equal("Access not justified for this role", rejected.RejectReason);
        Assert.Contains(rejected.AuditLogs, a => a.Action == "ManagerRejected");

        // 7c: Terminal state - cannot approve or reject further
        await Assert.ThrowsAsync<DomainValidationException>(async () =>
        {
            await _service.ProcessApprovalAsync(created.Id, new ApprovalDecisionDto
            {
                IsApproved = true,
                RowVersion = rejected.RowVersion
            });
        });
    }
}

