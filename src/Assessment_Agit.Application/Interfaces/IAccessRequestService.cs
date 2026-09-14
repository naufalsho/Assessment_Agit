using Assessment_Agit.Application.DTOs;

namespace Assessment_Agit.Application.Interfaces;

public interface IAccessRequestService
{
    Task<AccessRequestDetailDto> CreateRequestAsync(CreateAccessRequestDto dto, CancellationToken cancellationToken = default);
    Task<List<AccessRequestDetailDto>> GetMyRequestsAsync(CancellationToken cancellationToken = default);
    Task<List<AccessRequestDetailDto>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default);
    Task<List<AccessRequestDetailDto>> GetAllRequestsAsync(CancellationToken cancellationToken = default);
    Task<AccessRequestDetailDto> GetRequestByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AccessRequestDetailDto> ProcessApprovalAsync(Guid requestId, ApprovalDecisionDto decision, CancellationToken cancellationToken = default);
    Task<List<ApplicationDto>> GetApplicationsAsync(CancellationToken cancellationToken = default);
    Task<List<UserDto>> GetDemoUsersAsync(CancellationToken cancellationToken = default);
}

