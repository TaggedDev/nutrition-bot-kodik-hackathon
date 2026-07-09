using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nutrition.Infrastructure.Identity.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NutritionIdentityDbContext))]
    [Migration("20260709123000_AddDailyGoalMealPercents")]
    public partial class AddDailyGoalMealPercents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BreakfastPercent",
                table: "UserDailyGoals",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 25m);

            migrationBuilder.AddColumn<decimal>(
                name: "DinnerPercent",
                table: "UserDailyGoals",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 30m);

            migrationBuilder.AddColumn<decimal>(
                name: "LunchPercent",
                table: "UserDailyGoals",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 35m);

            migrationBuilder.AddColumn<decimal>(
                name: "SnackPercent",
                table: "UserDailyGoals",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 10m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BreakfastPercent",
                table: "UserDailyGoals");

            migrationBuilder.DropColumn(
                name: "DinnerPercent",
                table: "UserDailyGoals");

            migrationBuilder.DropColumn(
                name: "LunchPercent",
                table: "UserDailyGoals");

            migrationBuilder.DropColumn(
                name: "SnackPercent",
                table: "UserDailyGoals");
        }
    }
}
