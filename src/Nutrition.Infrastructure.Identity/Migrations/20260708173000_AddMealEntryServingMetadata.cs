using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nutrition.Infrastructure.Identity.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NutritionIdentityDbContext))]
    [Migration("20260708173000_AddMealEntryServingMetadata")]
    public partial class AddMealEntryServingMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LoggedAtUtc",
                table: "UserMealEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortionLabel",
                table: "UserMealEntries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Порция не указана");

            migrationBuilder.AddColumn<decimal>(
                name: "ServingGrams",
                table: "UserMealEntries",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                table: "UserMealEntries",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "UserMealEntries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.Sql("UPDATE \"UserMealEntries\" SET \"LoggedAtUtc\" = \"CreatedAtUtc\" WHERE \"LoggedAtUtc\" IS NULL;");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LoggedAtUtc",
                table: "UserMealEntries",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMealEntries_UserId_LoggedAtUtc",
                table: "UserMealEntries",
                columns: new[] { "UserId", "LoggedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserMealEntries_UserId_LoggedAtUtc",
                table: "UserMealEntries");

            migrationBuilder.DropColumn(
                name: "LoggedAtUtc",
                table: "UserMealEntries");

            migrationBuilder.DropColumn(
                name: "PortionLabel",
                table: "UserMealEntries");

            migrationBuilder.DropColumn(
                name: "ServingGrams",
                table: "UserMealEntries");

            migrationBuilder.DropColumn(
                name: "SourceReference",
                table: "UserMealEntries");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "UserMealEntries");
        }
    }
}
