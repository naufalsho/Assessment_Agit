using Assessment_Agit.Application.Interfaces;
using Assessment_Agit.Application.Services;
using Assessment_Agit.Infrastructure.Data;
using Assessment_Agit.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Assessment_Agit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=access_hub.db";

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAccessRequestService, AccessRequestService>();

        return services;
    }
}

