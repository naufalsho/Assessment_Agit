using Assessment_Agit.Application.DTOs;
using Assessment_Agit.Application.Interfaces;
using Assessment_Agit.Domain.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Assessment_Agit.Pages.Approvals;

public class IndexModel : PageModel
{
    private readonly IAccessRequestService _requestService;
    private readonly ICurrentUserService _currentUserService;

    public User? CurrentUser { get; private set; }
    public List<AccessRequestDetailDto> PendingRequests { get; private set; } = new();

    public IndexModel(IAccessRequestService requestService, ICurrentUserService currentUserService)
    {
        _requestService = requestService;
        _currentUserService = currentUserService;
    }

    public async Task OnGetAsync()
    {
        CurrentUser = await _currentUserService.GetUserAsync();
        PendingRequests = await _requestService.GetPendingApprovalsAsync();
    }
}

