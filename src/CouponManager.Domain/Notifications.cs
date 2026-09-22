namespace CouponManager.Domain;

public static class NotificationTypes
{
    public const string CouponExpiration = "coupon-expiration";
    public const string Discord = "discord";
    public const string ShellScript = "shell-script";
}

public sealed class NotificationRule
{
    private NotificationRule() { }

    public NotificationRule(string type, bool enabled, int leadTimeDays, int leadTimeHours) =>
        Update(type, enabled, leadTimeDays, leadTimeHours);

    public string Type { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public int LeadTimeDays { get; private set; }
    public int LeadTimeHours { get; private set; }

    public void Update(string type, bool enabled, int days, int hours)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("通知種別は必須です。", nameof(type));
        }

        if (days is < 0 or > 3650)
        {
            throw new ArgumentOutOfRangeException(nameof(days));
        }

        if (hours is < 0 or > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(hours));
        }

        if (days == 0 && hours == 0)
        {
            throw new ArgumentException("通知タイミングは1時間以上前に設定してください。");
        }

        Type = type.Trim();
        Enabled = enabled;
        LeadTimeDays = days;
        LeadTimeHours = hours;
    }

    public TimeSpan LeadTime => TimeSpan.FromDays(LeadTimeDays) + TimeSpan.FromHours(LeadTimeHours);
}

public sealed class NotificationChannelSetting
{
    private NotificationChannelSetting() { }

    public NotificationChannelSetting(string type, bool enabled, string configurationJson) =>
        Update(type, enabled, configurationJson);

    public string Type { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public string ConfigurationJson { get; private set; } = "{}";

    public void Update(string type, bool enabled, string configurationJson)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("通知先種別は必須です。", nameof(type));
        }

        Type = type.Trim();
        Enabled = enabled;
        ConfigurationJson = string.IsNullOrWhiteSpace(configurationJson) ? "{}" : configurationJson;
    }
}

public sealed class NotificationDelivery
{
    private NotificationDelivery() { }

    public NotificationDelivery(string eventKey, DateTimeOffset sentAt)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
        {
            throw new ArgumentException("通知イベントキーは必須です。", nameof(eventKey));
        }

        EventKey = eventKey.Trim();
        SentAt = sentAt;
    }

    public long Id { get; private set; }
    public string EventKey { get; private set; } = string.Empty;
    public DateTimeOffset SentAt { get; private set; }
}
