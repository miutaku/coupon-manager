using CouponManager.Application;
using CouponManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace CouponManager.Infrastructure;

public sealed class EfCouponRepository(CouponDbContext db) : ICouponRepository
{
    public async Task<IReadOnlyList<Coupon>> ListAsync(CouponQuery query, CancellationToken cancellationToken)
    {
        var coupons = db.Coupons.AsNoTracking().Include(x => x.Labels).AsQueryable();
        if (!query.IncludeUsed)
        {
            coupons = coupons.Where(coupon => coupon.RemainingUses > 0);
        }

        if (!query.IncludeExpired && query.Today is { } today)
        {
            coupons = coupons.Where(coupon => coupon.ExpiresOn == null || coupon.ExpiresOn >= today);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLikePattern(query.Search.Trim())}%";
            coupons = query.SearchField?.ToLowerInvariant() switch
            {
                "name" => coupons.Where(coupon => EF.Functions.ILike(coupon.Name, pattern, "\\")),
                "description" => coupons.Where(coupon => coupon.Description != null &&
                    EF.Functions.ILike(coupon.Description, pattern, "\\")),
                "label" => coupons.Where(coupon => coupon.Labels.Any(label =>
                    EF.Functions.ILike(label.Key, pattern, "\\") ||
                    EF.Functions.ILike(label.Value, pattern, "\\"))),
                "code" => coupons.Where(coupon => EF.Functions.ILike(coupon.DisplayValue, pattern, "\\")),
                _ => coupons.Where(coupon =>
                    EF.Functions.ILike(coupon.Name, pattern, "\\") ||
                    (coupon.Description != null && EF.Functions.ILike(coupon.Description, pattern, "\\")) ||
                    coupon.Labels.Any(label =>
                        EF.Functions.ILike(label.Key, pattern, "\\") ||
                        EF.Functions.ILike(label.Value, pattern, "\\")))
            };
        }

        coupons = query.Sort switch
        {
            CouponSort.CreatedOldest => coupons.OrderBy(x => x.CreatedAt),
            CouponSort.ExpirationSoonest => coupons.OrderBy(x => x.ExpiresOn == null).ThenBy(x => x.ExpiresOn),
            CouponSort.Name => coupons.OrderBy(x => x.Name),
            _ => coupons.OrderByDescending(x => x.CreatedAt)
        };
        return await coupons.ToListAsync(cancellationToken);
    }

    public Task<Coupon?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Coupons.Include(x => x.Labels).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Coupon>> FindManyAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        var distinctIds = ids.Distinct().ToArray();
        if (distinctIds.Length == 0)
        {
            return [];
        }

        return await db.Coupons
            .Where(coupon => distinctIds.Contains(coupon.Id))
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(Coupon coupon, CancellationToken cancellationToken) =>
        db.Coupons.AddAsync(coupon, cancellationToken).AsTask();

    public void Remove(Coupon coupon) => db.Coupons.Remove(coupon);

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        if (await db.AppSettings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken) is { } settings)
        {
            return settings;
        }

        settings = new AppSettings(30);
        db.AppSettings.Add(settings);
        return settings;
    }

    public async Task<IReadOnlyList<string>> PurgeInactiveBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        var expiryThreshold = DateOnly.FromDateTime(threshold.UtcDateTime);
        var expired = db.Coupons.Where(x =>
            (x.RemainingUses == 0 && x.UpdatedAt < threshold) ||
            (x.ExpiresOn != null && x.ExpiresOn < expiryThreshold));
        var images = await expired.Where(x => x.ImagePath != null).Select(x => x.ImagePath!).ToListAsync(cancellationToken);
        await expired.ExecuteDeleteAsync(cancellationToken);
        return images;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
