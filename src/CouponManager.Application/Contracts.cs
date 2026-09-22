using CouponManager.Domain;

namespace CouponManager.Application;

public sealed record LabelDto(string Key, string Value);

public sealed record CouponDto(Guid Id, string Name, string? Description, DateOnly? ExpiresOn,
    int TotalUses, int RemainingUses, CouponDisplayType DisplayType, CouponBarcodeType BarcodeType,
    string DisplayValue, bool HasImage, bool IsExpired,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<LabelDto> Labels);

public sealed record SaveCouponRequest(string Name, string? Description, DateOnly? ExpiresOn,
    int TotalUses, int? RemainingUses, CouponDisplayType DisplayType, CouponBarcodeType BarcodeType, string DisplayValue,
    IReadOnlyList<LabelDto>? Labels);

public sealed record CouponImageAnalysisDto(IReadOnlyList<CouponImageCandidateDto> Coupons);

public sealed record CouponImageCandidateDto(string? Name, string? Description,
    DateOnly? ExpiresOn, int? TotalUses, CouponDisplayType? DisplayType,
    CouponBarcodeType? BarcodeType, string? DisplayValue);

public enum CouponSort { CreatedNewest, CreatedOldest, ExpirationSoonest, Name }

public sealed record CouponQuery(string? Search = null, string? SearchField = null,
    CouponSort Sort = CouponSort.CreatedNewest, bool IncludeUsed = false,
    bool IncludeExpired = false, DateOnly? Today = null);

public sealed record AppSettingsDto(int InactiveRetentionDays, bool InactiveCleanupEnabled);

public sealed record CouponDeletionResult(int Count, IReadOnlyList<string> ImagePaths);

public sealed record NotificationRuleDto(string Type, bool Enabled, int LeadTimeDays, int LeadTimeHours);
public sealed record NotificationChannelDto(string Type, bool Enabled,
    IReadOnlyDictionary<string, string> Configuration);
public sealed record NotificationSettingsDto(IReadOnlyList<NotificationRuleDto> Rules,
    IReadOnlyList<NotificationChannelDto> Channels);

public interface ICouponRepository
{
    Task<IReadOnlyList<Coupon>> ListAsync(CouponQuery query, CancellationToken cancellationToken);
    Task<Coupon?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Coupon>> FindManyAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
    Task AddAsync(Coupon coupon, CancellationToken cancellationToken);
    void Remove(Coupon coupon);
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> PurgeInactiveBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface INotificationRepository
{
    Task<IReadOnlyList<NotificationRule>> ListRulesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationChannelSetting>> ListChannelsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Coupon>> ListExpiringCouponsAsync(DateOnly from, DateOnly through,
        CancellationToken cancellationToken);
    Task<IReadOnlySet<string>> ListDeliveredEventKeysAsync(IEnumerable<string> eventKeys,
        CancellationToken cancellationToken);
    Task AddDeliveryAsync(NotificationDelivery delivery, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface INotificationSettingsService
{
    Task<NotificationSettingsDto> GetAsync(CancellationToken cancellationToken);
    Task<NotificationSettingsDto> UpdateAsync(NotificationSettingsDto request, CancellationToken cancellationToken);
}

public interface ICouponService
{
    Task<IReadOnlyList<CouponDto>> ListAsync(CouponQuery query, CancellationToken cancellationToken);
    Task<CouponDto?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<CouponDto> CreateAsync(SaveCouponRequest request, CancellationToken cancellationToken);
    Task<CouponDto?> UpdateAsync(Guid id, SaveCouponRequest request, CancellationToken cancellationToken);
    Task<bool> MarkUsedAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> UseOnceAsync(Guid id, CancellationToken cancellationToken);
    Task<int> MarkUsedAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
    Task<CouponDeletionResult> DeleteAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
    Task<AppSettingsDto> GetSettingsAsync(CancellationToken cancellationToken);
    Task<AppSettingsDto> UpdateSettingsAsync(AppSettingsDto request, CancellationToken cancellationToken);
}
