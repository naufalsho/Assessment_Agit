using Assessment_Agit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Assessment_Agit.Application.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<ApplicationMaster> Applications { get; }
    DbSet<AccessRequest> AccessRequests { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

