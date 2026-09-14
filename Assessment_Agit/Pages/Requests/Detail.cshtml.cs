using Assessment_Agit.Application.DTOs;
using Assessment_Agit.Application.Interfaces;
using Assessment_Agit.Domain.Entities;
using Assessment_Agit.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Assessment_Agit.Pages.Requests;

public class DetailModel : PageModel
{
    private readonly IAccessRequestService _requestService;
    private readonly ICurrentUserService _currentUserService;

    public User? CurrentUser { get; private set; }
    public AccessRequestDetailDto? RequestDetail { get; private set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty]
    public int FormRowVersion { get; set; }

    [BindProperty]
    public string? RejectReason { get; set; }

    public DetailModel(IAccessRequestService requestService, ICurrentUserService currentUserService)
    {
        _requestService = requestService;
        _currentUserService = currentUserService;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        CurrentUser = await _currentUserService.GetUserAsync();
        try
        {
            RequestDetail = await _requestService.GetRequestByIdAsync(id);
            FormRowVersion = RequestDetail.RowVersion;
            return Page();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        CurrentUser = await _currentUserService.GetUserAsync();
        try
        {
            var decision = new ApprovalDecisionDto
            {
                IsApproved = true,
                RowVersion = FormRowVersion
            };
            RequestDetail = await _requestService.ProcessApprovalAsync(id, decision);
            SuccessMessage = "Request approved successfully!";
            FormRowVersion = RequestDetail.RowVersion;
            return Page();
        }
        catch (ForbiddenException ex)
        {
            ErrorMessage = $"[403 Forbidden] {ex.Message}";
        }
        catch (ConcurrencyConflictException ex)
        {
            ErrorMessage = $"[409 Conflict] {ex.Message}";
        }
        catch (DomainValidationException ex)
        {
            ErrorMessage = $"[400 Bad Request] {ex.Message}";
        }

        RequestDetail = await _requestService.GetRequestByIdAsync(id);
        FormRowVersion = RequestDetail.RowVersion;
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id)
    {
        CurrentUser = await _currentUserService.GetUserAsync();
        try
        {
            var decision = new ApprovalDecisionDto
            {
                IsApproved = false,
                RowVersion = FormRowVersion,
                RejectReason = RejectReason
            };
            RequestDetail = await _requestService.ProcessApprovalAsync(id, decision);
            SuccessMessage = "Request rejected successfully.";
            FormRowVersion = RequestDetail.RowVersion;
            return Page();
        }
        catch (ForbiddenException ex)
        {
            ErrorMessage = $"[403 Forbidden] {ex.Message}";
        }
        catch (ConcurrencyConflictException ex)
        {
            ErrorMessage = $"[409 Conflict] {ex.Message}";
        }
        catch (DomainValidationException ex)
        {
            ErrorMessage = $"[400 Bad Request] {ex.Message}";
        }

        RequestDetail = await _requestService.GetRequestByIdAsync(id);
        FormRowVersion = RequestDetail.RowVersion;
        return Page();
    }
}

