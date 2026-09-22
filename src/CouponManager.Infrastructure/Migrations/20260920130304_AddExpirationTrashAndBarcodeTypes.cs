using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CouponManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpirationTrashAndBarcodeTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BarcodeType",
                table: "coupons",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Code128");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "coupons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TrashRetentionDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_settings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "app_settings",
                columns: ["Id", "TrashRetentionDays"],
                values: [1, 30]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropColumn(
                name: "BarcodeType",
                table: "coupons");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "coupons");
        }
    }
}
