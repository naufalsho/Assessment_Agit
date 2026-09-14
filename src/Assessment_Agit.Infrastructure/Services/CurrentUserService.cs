using Assessment_Agit.Application.Interfaces;
using Assessment_Agit.Domain.Entities;
using Assessment_Agit.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Assessment_Agit.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAppDbContext _context;

    public const string SessionUserKey = "AccessHub_CurrentUserId";
    public const string CookieUserKey = "AccessHub_CurrentUserId";
    public const string HeaderUserKey = "X-User-Id";

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, IAppDbContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    public Guid? UserId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return DbInitializer.AliceId;

            // 1. Check custom Header (useful for API/Testing)
            if (httpContext.Request.Headers.TryGetValue(HeaderUserKey, out var headerVal) &&
                Guid.TryParse(headerVal.ToString(), out var headerGuid))
            {
                return headerGuid;
            }

            // 2. Check Session
            var sessionVal = httpContext.Session?.GetString(SessionUserKey);
            if (!string.IsNullOrEmpty(sessionVal) && Guid.TryParse(sessionVal, out var sessionGuid))
            {
                return sessionGuid;
            }

            // 3. Check Cookie
            if (httpContext.Request.Cookies.TryGetValue(CookieUserKey, out var cookieVal) &&
                Guid.TryParse(cookieVal, out var cookieGuid))
            {
                return cookieGuid;
            }

            // Default demo fallback: Alice
            return DbInitializer.AliceId;
        }
    }

    public string? UserEmail => null; // Resolved via GetUserAsync

    public async Task<User?> GetUserAsync(CancellationToken cancellationToken = default)
    {
        var id = UserId;
        if (!id.HasValue) return null;

        return await _context.Users
            .Include(u => u.Manager)
            .FirstOrDefaultAsync(u => u.Id == id.Value, cancellationToken);
    }

    public void SetUser(Guid userId, string userEmail)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        httpContext.Session?.SetString(SessionUserKey, userId.ToString());
        httpContext.Response.Cookies.Append(CookieUserKey, userId.ToString(), new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }
}

