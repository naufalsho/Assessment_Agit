using Assessment_Agit.Application.DTOs;
using Assessment_Agit.Application.Interfaces;
using Assessment_Agit.Domain.Exceptions;
using Assessment_Agit.Infrastructure;
using Assessment_Agit.Infrastructure.Data;
using Assessment_Agit.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorPages();

// Add Clean Architecture Infrastructure and Application services
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// Auto-seed database on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbInitializer.EnsureSeededAsync(dbContext);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthorization();

// Minimal API Endpoints for AJAX & Integration Tests
var apiGroup = app.MapGroup("/api");

// Switch active demo user
apiGroup.MapPost("/switch-user", async ([FromBody] SwitchUserRequest request, ICurrentUserService currentUserService, IAppDbContext context) =>
{
    var user = await context.Users.FindAsync(request.UserId);
    if (user == null) return Results.NotFound(new { message = "User not found." });

    currentUserService.SetUser(user.Id, user.Email);
    return Results.Ok(new { message = $"Switched to {user.FullName}", user = new { user.Id, user.FullName, user.Email, user.Role } });
});

// Get current active user info
apiGroup.MapGet("/current-user", async (ICurrentUserService currentUserService) =>
{
    var user = await currentUserService.GetUserAsync();
    if (user == null) return Results.Unauthorized();
    return Results.Ok(new { user.Id, user.FullName, user.Email, user.Role });
});

// List demo users
apiGroup.MapGet("/demo-users", async (IAccessRequestService service) =>
{
    var users = await service.GetDemoUsersAsync();
    return Results.Ok(users);
});

// List applications
apiGroup.MapGet("/applications", async (IAccessRequestService service) =>
{
    var apps = await service.GetApplicationsAsync();
    return Results.Ok(apps);
});

// Create access request (Idempotent)
apiGroup.MapPost("/requests", async ([FromBody] CreateAccessRequestDto dto, IAccessRequestService service) =>
{
    try
    {
        var result = await service.CreateRequestAsync(dto);
        return Results.Created($"/api/requests/{result.Id}", result);
    }
    catch (DomainValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (ForbiddenException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
    }
    catch (ConcurrencyConflictException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

// Get request detail by Id
apiGroup.MapGet("/requests/{id:guid}", async (Guid id, IAccessRequestService service) =>
{
    try
    {
        var result = await service.GetRequestByIdAsync(id);
        return Results.Ok(result);
    }
    catch (NotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

// Approve request
apiGroup.MapPost("/requests/{id:guid}/approve", async (Guid id, [FromBody] ApprovalDecisionDto decision, IAccessRequestService service) =>
{
    try
    {
        decision.IsApproved = true;
        var result = await service.ProcessApprovalAsync(id, decision);
        return Results.Ok(result);
    }
    catch (NotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (ForbiddenException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
    }
    catch (ConcurrencyConflictException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
    catch (DomainValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Reject request
apiGroup.MapPost("/requests/{id:guid}/reject", async (Guid id, [FromBody] ApprovalDecisionDto decision, IAccessRequestService service) =>
{
    try
    {
        decision.IsApproved = false;
        var result = await service.ProcessApprovalAsync(id, decision);
        return Results.Ok(result);
    }
    catch (NotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (ForbiddenException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
    }
    catch (ConcurrencyConflictException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
    catch (DomainValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapRazorPages();

app.Run();

public record SwitchUserRequest(Guid UserId);

// Partial program class for WebApplicationFactory in tests
public partial class Program { }

