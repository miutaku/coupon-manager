using CouponManager.Domain;
using Xunit;

namespace CouponManager.Domain.Tests;

public sealed class CouponTests
{
    [Fact]
    public void New_coupon_starts_with_all_uses_remaining()
    {
        var now = DateTimeOffset.Parse("2026-01-02T03:04:05Z");
        var coupon = new Coupon(" 10% OFF ", null, null, 3, CouponDisplayType.QrCode, CouponBarcodeType.Code128, "https://example.test", now);

        Assert.Equal("10% OFF", coupon.Name);
        Assert.Equal(3, coupon.RemainingUses);
        Assert.False(coupon.IsUsed);
    }

    [Fact]
    public void MarkUsed_consumes_every_remaining_use()
    {
        var coupon = new Coupon("ticket", null, null, 4, CouponDisplayType.Serial, CouponBarcodeType.Code128, "ABC", DateTimeOffset.MinValue);
        coupon.MarkUsed(DateTimeOffset.UtcNow);
        Assert.Equal(0, coupon.RemainingUses);
        Assert.True(coupon.IsUsed);
    }

    [Fact]
    public void Duplicate_label_keys_are_allowed()
    {
        var coupon = new Coupon("ticket", null, null, 1, CouponDisplayType.Serial, CouponBarcodeType.Code128, "ABC", DateTimeOffset.MinValue);
        coupon.Update("ticket", null, null, 1, 1, CouponDisplayType.Serial, CouponBarcodeType.Code128, "ABC",
            [("店舗", "楽天"), ("店舗", "Amazon")], DateTimeOffset.UtcNow);
        Assert.Equal(2, coupon.Labels.Count(x => x.Key == "店舗"));
    }

    [Fact]
    public void Inactive_retention_period_is_configurable()
    {
        var settings = new AppSettings(30);
        settings.SetInactiveRetentionDays(60);
        Assert.Equal(60, settings.InactiveRetentionDays);
    }

    [Fact]
    public void Inactive_cleanup_can_be_disabled_without_losing_retention_period()
    {
        var settings = new AppSettings(60);
        settings.SetInactiveCleanupEnabled(false);

        Assert.False(settings.InactiveCleanupEnabled);
        Assert.Equal(60, settings.InactiveRetentionDays);
    }

    [Fact]
    public void Notification_rule_supports_hour_and_day_lead_times()
    {
        var rule = new NotificationRule(NotificationTypes.CouponExpiration, true, 0, 12);
        Assert.Equal(TimeSpan.FromHours(12), rule.LeadTime);

        rule.Update(NotificationTypes.CouponExpiration, true, 3, 6);
        Assert.Equal(TimeSpan.FromHours(78), rule.LeadTime);
    }

    [Fact]
    public void Use_once_decrements_only_one_use()
    {
        var coupon = new Coupon("ticket", null, null, 3, CouponDisplayType.Serial, CouponBarcodeType.Code128, "ABC", DateTimeOffset.MinValue);
        coupon.UseOnce(DateTimeOffset.UtcNow);
        Assert.Equal(2, coupon.RemainingUses);
    }

    [Theory]
    [InlineData(CouponBarcodeType.Ean13, "123")]
    [InlineData(CouponBarcodeType.Ean8, "ABCDEFG")]
    public void Invalid_ean_is_rejected(CouponBarcodeType type, string value)
    {
        Assert.Throws<ArgumentException>(() => new Coupon("ticket", null, null, 1,
            CouponDisplayType.Barcode, type, value, DateTimeOffset.MinValue));
    }

    [Fact]
    public void Values_larger_than_the_persistence_limits_are_rejected_by_the_domain()
    {
        var longName = new string('x', Coupon.MaximumNameLength + 1);

        Assert.Throws<ArgumentException>(() => new Coupon(longName, null, null, 1,
            CouponDisplayType.Serial, CouponBarcodeType.Code128, "ABC", DateTimeOffset.MinValue));
    }

    [Fact]
    public void Incomplete_labels_are_rejected_instead_of_silently_discarded()
    {
        Assert.Throws<ArgumentException>(() => new Coupon("ticket", null, null, 1,
            CouponDisplayType.Serial, CouponBarcodeType.Code128, "ABC", DateTimeOffset.MinValue,
            [("store", "")]));
    }
}
