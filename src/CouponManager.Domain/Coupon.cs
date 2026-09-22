namespace CouponManager.Domain;

public enum CouponDisplayType
{
    QrCode,
    Barcode,
    Serial
}

public enum CouponBarcodeType
{
    Code128,
    Ean13,
    Ean8
}

public sealed class Coupon
{
    public const int MaximumNameLength = 200;
    public const int MaximumDescriptionLength = 4000;
    public const int MaximumDisplayValueLength = 2000;
    public const int MaximumTotalUses = 999;

    private Coupon() { }

    public Coupon(string name, string? description, DateOnly? expiresOn, int totalUses,
        CouponDisplayType displayType, CouponBarcodeType barcodeType, string displayValue, DateTimeOffset now,
        IEnumerable<(string Key, string Value)>? labels = null, int? remainingUses = null)
    {
        CreatedAt = now;
        Update(name, description, expiresOn, totalUses, remainingUses ?? totalUses, displayType, barcodeType,
            displayValue, labels ?? [], now);
    }

    private static void ValidateBarcode(CouponDisplayType displayType, CouponBarcodeType barcodeType, string value)
    {
        if (displayType != CouponDisplayType.Barcode || barcodeType == CouponBarcodeType.Code128)
        {
            return;
        }
        var hasValidLength = barcodeType == CouponBarcodeType.Ean13
            ? value.Length is 12 or 13
            : value.Length is 7 or 8;
        if (!hasValidLength || !value.All(char.IsDigit))
        {
            throw new ArgumentException(barcodeType == CouponBarcodeType.Ean13
                ? "JAN/EAN-13は12桁または13桁の数字で入力してください。"
                : "EAN-8は7桁または8桁の数字で入力してください。", nameof(value));
        }
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public int TotalUses { get; private set; }
    public int RemainingUses { get; private set; }
    public CouponDisplayType DisplayType { get; private set; }
    public CouponBarcodeType BarcodeType { get; private set; }
    public string DisplayValue { get; private set; } = string.Empty;
    public string? ImagePath { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public List<CouponLabel> Labels { get; private set; } = [];

    public bool IsUsed => RemainingUses == 0;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("クーポン名は必須です。", nameof(name));
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > MaximumNameLength)
        {
            throw new ArgumentException($"クーポン名は{MaximumNameLength}文字以内で入力してください。", nameof(name));
        }

        Name = trimmedName;
    }

    public void Update(string name, string? description, DateOnly? expiresOn, int totalUses,
        int remainingUses, CouponDisplayType displayType, CouponBarcodeType barcodeType, string displayValue,
        IEnumerable<(string Key, string Value)> labels, DateTimeOffset now)
    {
        Rename(name);
        if (totalUses is < 1 or > MaximumTotalUses)
        {
            throw new ArgumentOutOfRangeException(nameof(totalUses));
        }

        if (remainingUses < 0 || remainingUses > totalUses)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingUses));
        }

        var trimmedDescription = description?.Trim();
        if (trimmedDescription?.Length > MaximumDescriptionLength)
        {
            throw new ArgumentException($"詳細は{MaximumDescriptionLength}文字以内で入力してください。", nameof(description));
        }

        if (string.IsNullOrWhiteSpace(displayValue))
        {
            throw new ArgumentException("表示値は必須です。", nameof(displayValue));
        }

        var trimmedDisplayValue = displayValue.Trim();
        if (trimmedDisplayValue.Length > MaximumDisplayValueLength)
        {
            throw new ArgumentException($"表示値は{MaximumDisplayValueLength}文字以内で入力してください。", nameof(displayValue));
        }

        Description = trimmedDescription;
        ExpiresOn = expiresOn;
        TotalUses = totalUses;
        RemainingUses = remainingUses;
        DisplayType = displayType;
        BarcodeType = barcodeType;
        DisplayValue = trimmedDisplayValue;
        ValidateBarcode(displayType, barcodeType, DisplayValue);
        Labels = [.. labels.Select(label => new CouponLabel(label.Key, label.Value))];
        UpdatedAt = now;
    }

    public void MarkUsed(DateTimeOffset now)
    {
        RemainingUses = 0;
        UpdatedAt = now;
    }

    public void UseOnce(DateTimeOffset now)
    {
        if (RemainingUses == 0)
        {
            return;
        }

        RemainingUses--;
        UpdatedAt = now;
    }

    public void SetImage(string path, DateTimeOffset now)
    {
        ImagePath = path;
        UpdatedAt = now;
    }
}

public sealed class AppSettings
{
    private AppSettings() { }

    public AppSettings(int inactiveRetentionDays, bool inactiveCleanupEnabled = true)
    {
        SetInactiveRetentionDays(inactiveRetentionDays);
        InactiveCleanupEnabled = inactiveCleanupEnabled;
    }
    public int Id { get; private set; } = 1;
    public int InactiveRetentionDays { get; private set; }
    public bool InactiveCleanupEnabled { get; private set; } = true;

    public void SetInactiveRetentionDays(int days)
    {
        if (days is < 1 or > 3650)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "保持期間は1〜3650日で指定してください。");
        }

        InactiveRetentionDays = days;
    }

    public void SetInactiveCleanupEnabled(bool enabled) => InactiveCleanupEnabled = enabled;
}

public sealed class CouponLabel
{
    public const int MaximumKeyLength = 100;
    public const int MaximumValueLength = 300;

    private CouponLabel() { }

    public CouponLabel(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length > MaximumKeyLength)
        {
            throw new ArgumentException($"ラベルのキーは1〜{MaximumKeyLength}文字で入力してください。", nameof(key));
        }

        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > MaximumValueLength)
        {
            throw new ArgumentException($"ラベルの値は1〜{MaximumValueLength}文字で入力してください。", nameof(value));
        }

        Key = key.Trim();
        Value = value.Trim();
    }

    public long Id { get; private set; }
    public Guid CouponId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
}
