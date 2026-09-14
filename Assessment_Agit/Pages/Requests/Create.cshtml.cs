using Assessment_Agit.Application.DTOs;
using Assessment_Agit.Application.Interfaces;
using Assessment_Agit.Domain.Entities;
using Assessment_Agit.Domain.Enums;
using Assessment_Agit.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Assessment_Agit.Pages.Requests;

public class CreateModel : PageModel
{
    private readonly IAccessRequestService _requestService;
    private readonly ICurrentUserService _currentUserService;

    public User? CurrentUser { get; private set; }
    public List<ApplicationDto> Applications { get; private set; } = new();

    [BindProperty]
    public CreateAccessRequestDto Input { get; set; } = new()
    {
        ClientRequestId = Guid.NewGuid().ToString(),
        Environment = EnvironmentType.NonProduction,
        AccessLevel = AccessLevel.Read
    };

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public CreateModel(IAccessRequestService requestService, ICurrentUserService currentUserService)
    {
        _requestService = requestService;
        _currentUserService = currentUserService;
    }

    public async Task OnGetAsync()
    {
        CurrentUser = await _currentUserService.GetUserAsync();
        Applications = await _requestService.GetApplicationsAsync();
        if (string.IsNullOrEmpty(Input.ClientRequestId))
        {
            Input.ClientRequestId = Guid.NewGuid().ToString();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        CurrentUser = await _currentUserService.GetUserAsync();
        Applications = await _requestService.GetApplicationsAsync();

        if (string.IsNullOrWhiteSpace(Input.Justification))
        {
            ErrorMessage = "Justification is mandatory.";
            return Page();
        }

        try
        {
            var result = await _requestService.CreateRequestAsync(Input);
            return RedirectToPage("/Requests/Detail", new { id = result.Id });
        }
        catch (DomainValidationException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (ForbiddenException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }
}

