using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CouponManager.Infrastructure.Migrations;

[DbContext(typeof(CouponDbContext))]
[Migration("20260922110000_AddNotifications")]
public partial class AddNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "notification_channels",
            columns: table => new
            {
                Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                ConfigurationJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_notification_channels", x => x.Type));

        migrationBuilder.CreateTable(
            name: "notification_deliveries",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                EventKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_notification_deliveries", x => x.Id));

        migrationBuilder.CreateTable(
            name: "notification_rules",
            columns: table => new
            {
                Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                LeadTimeValue = table.Column<int>(type: "integer", nullable: false),
                LeadTimeUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_notification_rules", x => x.Type));

        migrationBuilder.CreateIndex(
            name: "IX_notification_deliveries_EventKey",
            table: "notification_deliveries",
            column: "EventKey",
            unique: true);

        migrationBuilder.InsertData("notification_rules",
            ["Type", "Enabled", "LeadTimeValue", "LeadTimeUnit"],
            ["character varying(100)", "boolean", "integer", "character varying(20)"],
            ["coupon-expiration", false, 1, "Days"]);
        migrationBuilder.InsertData("notification_channels",
            ["Type", "Enabled", "ConfigurationJson"],
            ["character varying(100)", "boolean", "jsonb"],
            new object[,]
            {
                { "discord", false, "{\"webhookUrl\":\"\"}" },
                { "shell-script", false, "{\"executablePath\":\"\"}" }
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("notification_channels");
        migrationBuilder.DropTable("notification_deliveries");
        migrationBuilder.DropTable("notification_rules");
    }
}
