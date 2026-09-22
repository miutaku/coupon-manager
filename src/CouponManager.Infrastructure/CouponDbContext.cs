using CouponManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace CouponManager.Infrastructure;

public sealed class CouponDbContext(DbContextOptions<CouponDbContext> options) : DbContext(options)
{
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponLabel> CouponLabels => Set<CouponLabel>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<NotificationRule> NotificationRules => Set<NotificationRule>();
    public DbSet<NotificationChannelSetting> NotificationChannels => Set<NotificationChannelSetting>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var coupon = modelBuilder.Entity<Coupon>();
        coupon.ToTable("coupons");
        coupon.HasKey(x => x.Id);
        coupon.Property(x => x.Name).HasMaxLength(Coupon.MaximumNameLength).IsRequired();
        coupon.Property(x => x.Description).HasMaxLength(Coupon.MaximumDescriptionLength);
        coupon.Property(x => x.DisplayType).HasConversion<string>().HasMaxLength(20);
        coupon.Property(x => x.BarcodeType).HasConversion<string>().HasMaxLength(20);
        coupon.Property(x => x.DisplayValue).HasMaxLength(Coupon.MaximumDisplayValueLength).IsRequired();
        coupon.Property(x => x.ImagePath).HasMaxLength(500);
        coupon.HasMany(x => x.Labels).WithOne().HasForeignKey(x => x.CouponId)
            .OnDelete(DeleteBehavior.Cascade);
        coupon.Ignore(x => x.IsUsed);

        var settings = modelBuilder.Entity<AppSettings>();
        settings.ToTable("app_settings");
        settings.HasKey(x => x.Id);
        settings.HasData(new { Id = 1, InactiveRetentionDays = 30, InactiveCleanupEnabled = true });

        var notificationRule = modelBuilder.Entity<NotificationRule>();
        notificationRule.ToTable("notification_rules");
        notificationRule.HasKey(x => x.Type);
        notificationRule.Property(x => x.Type).HasMaxLength(100);
        notificationRule.HasData(new
        {
            Type = NotificationTypes.CouponExpiration,
            Enabled = false,
            LeadTimeDays = 1,
            LeadTimeHours = 0
        });

        var notificationChannel = modelBuilder.Entity<NotificationChannelSetting>();
        notificationChannel.ToTable("notification_channels");
        notificationChannel.HasKey(x => x.Type);
        notificationChannel.Property(x => x.Type).HasMaxLength(100);
        notificationChannel.Property(x => x.ConfigurationJson).HasColumnType("jsonb");
        notificationChannel.HasData(
            new { Type = NotificationTypes.Discord, Enabled = false, ConfigurationJson = "{\"webhookUrl\":\"\"}" },
            new { Type = NotificationTypes.ShellScript, Enabled = false, ConfigurationJson = "{\"executablePath\":\"\"}" });

        var notificationDelivery = modelBuilder.Entity<NotificationDelivery>();
        notificationDelivery.ToTable("notification_deliveries");
        notificationDelivery.HasKey(x => x.Id);
        notificationDelivery.Property(x => x.EventKey).HasMaxLength(500).IsRequired();
        notificationDelivery.HasIndex(x => x.EventKey).IsUnique();

        var label = modelBuilder.Entity<CouponLabel>();
        label.ToTable("coupon_labels");
        label.HasKey(x => x.Id);
        label.Property(x => x.Key).HasMaxLength(CouponLabel.MaximumKeyLength).IsRequired();
        label.Property(x => x.Value).HasMaxLength(CouponLabel.MaximumValueLength).IsRequired();
        label.HasIndex(x => new { x.Key, x.Value });
    }
}
