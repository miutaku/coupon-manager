using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CouponManager.Infrastructure.Migrations;

[DbContext(typeof(CouponDbContext))]
[Migration("20260922090000_AddInactiveCleanupEnabled")]
public partial class AddInactiveCleanupEnabled : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "InactiveCleanupEnabled",
            table: "app_settings",
            type: "boolean",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "InactiveCleanupEnabled",
            table: "app_settings");
    }
}
