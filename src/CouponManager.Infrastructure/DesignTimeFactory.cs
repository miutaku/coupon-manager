using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CouponManager.Infrastructure;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<CouponDbContext>
{
    public CouponDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<CouponDbContext>();
        builder.UseNpgsql("Host=localhost;Database=coupons;Username=coupons;Password=coupons");
        return new CouponDbContext(builder.Options);
    }
}
