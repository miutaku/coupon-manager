using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CouponManager.Infrastructure.Migrations;

[DbContext(typeof(CouponDbContext))]
[Migration("20260922130000_CombineNotificationLeadTime")]
public partial class CombineNotificationLeadTime : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("LeadTimeDays", "notification_rules", "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>("LeadTimeHours", "notification_rules", "integer", nullable: false, defaultValue: 0);
        migrationBuilder.Sql("""
            UPDATE notification_rules
            SET "LeadTimeDays" = CASE WHEN "LeadTimeUnit" = 'Days' THEN "LeadTimeValue" ELSE "LeadTimeValue" / 24 END,
                "LeadTimeHours" = CASE WHEN "LeadTimeUnit" = 'Hours' THEN "LeadTimeValue" % 24 ELSE 0 END
            """);
        migrationBuilder.DropColumn("LeadTimeUnit", "notification_rules");
        migrationBuilder.DropColumn("LeadTimeValue", "notification_rules");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("LeadTimeUnit", "notification_rules", "character varying(20)", maxLength: 20,
            nullable: false, defaultValue: "Days");
        migrationBuilder.AddColumn<int>("LeadTimeValue", "notification_rules", "integer", nullable: false, defaultValue: 1);
        migrationBuilder.Sql("""
            UPDATE notification_rules
            SET "LeadTimeUnit" = CASE WHEN "LeadTimeDays" > 0 THEN 'Days' ELSE 'Hours' END,
                "LeadTimeValue" = CASE WHEN "LeadTimeDays" > 0 THEN "LeadTimeDays" ELSE "LeadTimeHours" END
            """);
        migrationBuilder.DropColumn("LeadTimeDays", "notification_rules");
        migrationBuilder.DropColumn("LeadTimeHours", "notification_rules");
    }
}
