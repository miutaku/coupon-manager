using CouponManager.Application;
using CouponManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace CouponManager.Infrastructure;

public sealed class EfNotificationRepository(CouponDbContext db) : INotificationRepository
{
    public async Task<IReadOnlyList<NotificationRule>> ListRulesAsync(CancellationToken cancellationToken) =>
        await db.NotificationRules.OrderBy(x => x.Type).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NotificationChannelSetting>> ListChannelsAsync(CancellationToken cancellationToken) =>
        await db.NotificationChannels.OrderBy(x => x.Type).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Coupon>> ListExpiringCouponsAsync(
        DateOnly from,
        DateOnly through,
        CancellationToken cancellationToken) =>
        await db.Coupons
            .AsNoTracking()
            .Where(coupon => coupon.RemainingUses > 0 &&
                             coupon.ExpiresOn >= from &&
                             coupon.ExpiresOn <= through)
            .OrderBy(coupon => coupon.ExpiresOn)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<string>> ListDeliveredEventKeysAsync(
        IEnumerable<string> eventKeys,
        CancellationToken cancellationToken)
    {
        var keys = eventKeys.Distinct().ToArray();
        if (keys.Length == 0)
        {
            return new HashSet<string>();
        }

        return (await db.NotificationDeliveries
                .AsNoTracking()
                .Where(delivery => keys.Contains(delivery.EventKey))
                .Select(delivery => delivery.EventKey)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
    }

    public Task AddDeliveryAsync(NotificationDelivery delivery, CancellationToken cancellationToken) =>
        db.NotificationDeliveries.AddAsync(delivery, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
