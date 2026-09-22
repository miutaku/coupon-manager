using CouponManager.Domain;

namespace CouponManager.Application;

public sealed class CouponService(ICouponRepository repository, TimeProvider timeProvider) : ICouponService
{
    public async Task<IReadOnlyList<CouponDto>> ListAsync(
        CouponQuery query,
        CancellationToken cancellationToken)
    {
        var today = GetToday();
        var coupons = await repository.ListAsync(query with { Today = today }, cancellationToken);
        return [.. coupons.Select(coupon => Map(coupon, today))];
    }

    public async Task<CouponDto?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var coupon = await repository.FindAsync(id, cancellationToken);
        return coupon is null ? null : Map(coupon, GetToday());
    }

    public async Task<CouponDto> CreateAsync(SaveCouponRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var coupon = new Coupon(request.Name, request.Description, request.ExpiresOn, request.TotalUses,
            request.DisplayType, request.BarcodeType, request.DisplayValue, now,
            (request.Labels ?? []).Select(label => (label.Key, label.Value)), request.RemainingUses);
        await repository.AddAsync(coupon, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(coupon, GetToday());
    }

    public async Task<CouponDto?> UpdateAsync(Guid id, SaveCouponRequest request, CancellationToken cancellationToken)
    {
        var coupon = await repository.FindAsync(id, cancellationToken);
        if (coupon is null)
        {
            return null;
        }

        coupon.Update(request.Name, request.Description, request.ExpiresOn, request.TotalUses,
            request.RemainingUses ?? request.TotalUses, request.DisplayType, request.BarcodeType, request.DisplayValue,
            (request.Labels ?? []).Select(label => (label.Key, label.Value)), timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Map(coupon, GetToday());
    }

    public async Task<bool> MarkUsedAsync(Guid id, CancellationToken cancellationToken) =>
        await MarkUsedAsync([id], cancellationToken) > 0;

    public async Task<bool> UseOnceAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await repository.FindAsync(id, cancellationToken) is not { } coupon)
        {
            return false;
        }

        coupon.UseOnce(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkUsedAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var coupons = await repository.FindManyAsync(ids, cancellationToken);
        if (coupons.Count == 0)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var coupon in coupons)
        {
            coupon.MarkUsed(now);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return coupons.Count;
    }

    public async Task<CouponDeletionResult> DeleteAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        var coupons = await repository.FindManyAsync(ids, cancellationToken);
        if (coupons.Count == 0)
        {
            return new CouponDeletionResult(0, []);
        }

        var imagePaths = coupons
            .Select(coupon => coupon.ImagePath)
            .OfType<string>()
            .ToList();

        foreach (var coupon in coupons)
        {
            repository.Remove(coupon);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return new CouponDeletionResult(coupons.Count, imagePaths);
    }

    public async Task<AppSettingsDto> GetSettingsAsync(CancellationToken cancellationToken) =>
        Map(await repository.GetSettingsAsync(cancellationToken));

    public async Task<AppSettingsDto> UpdateSettingsAsync(AppSettingsDto request, CancellationToken cancellationToken)
    {
        var settings = await repository.GetSettingsAsync(cancellationToken);
        settings.SetInactiveRetentionDays(request.InactiveRetentionDays);
        settings.SetInactiveCleanupEnabled(request.InactiveCleanupEnabled);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(settings);
    }

    private static CouponDto Map(Coupon coupon, DateOnly today) => new(
        coupon.Id,
        coupon.Name,
        coupon.Description,
        coupon.ExpiresOn,
        coupon.TotalUses,
        coupon.RemainingUses,
        coupon.DisplayType,
        coupon.BarcodeType,
        coupon.DisplayValue,
        coupon.ImagePath is not null,
        coupon.ExpiresOn is { } expires && expires < today,
        coupon.CreatedAt,
        coupon.UpdatedAt,
        [.. coupon.Labels.Select(label => new LabelDto(label.Key, label.Value))]);

    private static AppSettingsDto Map(AppSettings settings) =>
        new(settings.InactiveRetentionDays, settings.InactiveCleanupEnabled);

    private DateOnly GetToday() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
