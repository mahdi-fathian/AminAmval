using AminAmval.Domain.Repositories;
using AminAmval.Domain.Services;
using AminAmval.Infrastructure.Persistence;
using AminAmval.Infrastructure.Repositories;
using AminAmval.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AminAmval.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<IDispositionRequestRepository, DispositionRequestRepository>();
        services.AddScoped<IAuthSessionRepository, AuthSessionRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IImageRepository, ImageRepository>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<IImageStorageService, ImageStorageService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        return services;
    }
}
