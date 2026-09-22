using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CouponManager.Infrastructure.Migrations;

[DbContext(typeof(CouponDbContext))]
[Migration("20260920230000_RemoveTrashConcept")]
public partial class RemoveTrashConcept : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DeletedAt", table: "coupons");
        migrationBuilder.RenameColumn(
            name: "TrashRetentionDays",
            table: "app_settings",
            newName: "InactiveRetentionDays");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "InactiveRetentionDays",
            table: "app_settings",
            newName: "TrashRetentionDays");
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAt",
            table: "coupons",
            type: "timestamp with time zone",
            nullable: true);
    }
}
