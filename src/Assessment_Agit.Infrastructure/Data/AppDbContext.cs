using Assessment_Agit.Application.Interfaces;
using Assessment_Agit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Assessment_Agit.Infrastructure.Data;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<ApplicationMaster> Applications => Set<ApplicationMaster>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.Property(u => u.FullName).HasMaxLength(256).IsRequired();
            entity.Property(u => u.Role).HasMaxLength(64).IsRequired();

            entity.HasOne(u => u.Manager)
                .WithMany(u => u.DirectReports)
                .HasForeignKey(u => u.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ApplicationMaster
        modelBuilder.Entity<ApplicationMaster>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.Code).IsUnique();
            entity.Property(a => a.Code).HasMaxLength(64).IsRequired();
            entity.Property(a => a.Name).HasMaxLength(128).IsRequired();

            entity.HasOne(a => a.SystemOwner)
                .WithMany()
                .HasForeignKey(a => a.SystemOwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // AccessRequest
        modelBuilder.Entity<AccessRequest>(entity =>
        {
            entity.HasKey(r => r.Id);

            // Database constraint: Unique ClientRequestId for Idempotency
            entity.HasIndex(r => r.ClientRequestId).IsUnique();
            entity.Property(r => r.ClientRequestId).HasMaxLength(128).IsRequired();

            entity.Property(r => r.Justification).HasMaxLength(1000).IsRequired();
            entity.Property(r => r.RejectReason).HasMaxLength(1000);
            entity.Property(r => r.PolicyVersion).HasMaxLength(16).IsRequired();

            // Optimistic Concurrency Token
            entity.Property(r => r.RowVersion)
                .IsConcurrencyToken()
                .IsRequired();

            entity.HasOne(r => r.Requester)
                .WithMany(u => u.CreatedRequests)
                .HasForeignKey(r => r.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Application)
                .WithMany()
                .HasForeignKey(r => r.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Action).HasMaxLength(64).IsRequired();
            entity.Property(a => a.Details).HasMaxLength(1000);

            entity.HasOne(a => a.AccessRequest)
                .WithMany(r => r.AuditLogs)
                .HasForeignKey(a => a.AccessRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Actor)
                .WithMany()
                .HasForeignKey(a => a.ActorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

