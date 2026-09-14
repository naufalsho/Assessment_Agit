using Assessment_Agit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Assessment_Agit.Infrastructure.Data;

public static class DbInitializer
{
    public static readonly Guid AliceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid BobId   = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid CarolId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid DanaId  = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid ErinId  = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public static readonly Guid CrmAppId           = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid FinancePortalAppId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static async Task EnsureSeededAsync(AppDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        if (await context.Users.AnyAsync())
        {
            return; // Already seeded
        }

        // 1. Seed Users
        var bob = new User
        {
            Id = BobId,
            Email = "bob@example.local",
            FullName = "Bob (Manager)",
            Role = "Manager",
            ManagerId = null
        };

        var alice = new User
        {
            Id = AliceId,
            Email = "alice@example.local",
            FullName = "Alice (Requester)",
            Role = "Requester",
            ManagerId = BobId // Alice reports to Bob
        };

        var carol = new User
        {
            Id = CarolId,
            Email = "carol@example.local",
            FullName = "Carol (CRM System Owner)",
            Role = "SystemOwner",
            ManagerId = null
        };

        var dana = new User
        {
            Id = DanaId,
            Email = "dana@example.local",
            FullName = "Dana (Finance Portal System Owner)",
            Role = "SystemOwner",
            ManagerId = null
        };

        var erin = new User
        {
            Id = ErinId,
            Email = "erin@example.local",
            FullName = "Erin (Admin/Auditor)",
            Role = "AdminAuditor",
            ManagerId = null
        };

        await context.Users.AddRangeAsync(bob, alice, carol, dana, erin);
        await context.SaveChangesAsync();

        // 2. Seed Applications
        var crm = new ApplicationMaster
        {
            Id = CrmAppId,
            Code = "CRM",
            Name = "CRM",
            SystemOwnerId = CarolId
        };

        var financePortal = new ApplicationMaster
        {
            Id = FinancePortalAppId,
            Code = "FINANCE_PORTAL",
            Name = "Finance Portal",
            SystemOwnerId = DanaId
        };

        await context.Applications.AddRangeAsync(crm, financePortal);
        await context.SaveChangesAsync();
    }
}

