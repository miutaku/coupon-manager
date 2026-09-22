using CouponManager.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CouponManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCouponInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Coupons")
            ?? throw new InvalidOperationException("ConnectionStrings:Coupons is required.");
        services.AddDbContext<CouponDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ICouponRepository, EfCouponRepository>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<INotificationRepository, EfNotificationRepository>();
        services.AddScoped<INotificationSettingsService, NotificationSettingsService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
