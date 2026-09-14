using Assessment_Agit.Domain.Entities;

namespace Assessment_Agit.Application.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserEmail { get; }
    Task<User?> GetUserAsync(CancellationToken cancellationToken = default);
    void SetUser(Guid userId, string userEmail);
}

