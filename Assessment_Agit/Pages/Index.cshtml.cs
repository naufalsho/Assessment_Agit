using Assessment_Agit.Application.DTOs;
using Assessment_Agit.Application.Interfaces;
using Assessment_Agit.Domain.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Assessment_Agit.Pages;

public class IndexModel : PageModel
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAccessRequestService _requestService;

    public User? CurrentUser { get; private set; }
    public List<AccessRequestDetailDto> MyRequests { get; private set; } = new();
    public List<AccessRequestDetailDto> PendingApprovals { get; private set; } = new();
    public List<UserDto> DemoUsers { get; private set; } = new();

    public IndexModel(ICurrentUserService currentUserService, IAccessRequestService requestService)
    {
        _currentUserService = currentUserService;
        _requestService = requestService;
    }

    public async Task OnGetAsync()
    {
        CurrentUser = await _currentUserService.GetUserAsync();
        MyRequests = await _requestService.GetMyRequestsAsync();
        PendingApprovals = await _requestService.GetPendingApprovalsAsync();
        DemoUsers = await _requestService.GetDemoUsersAsync();
    }
}

