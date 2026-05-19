using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMealPlanDietSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ConsiderBudget",
                table: "MealPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ExcludedProducts",
                table: "MealPlans",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GlutenFree",
                table: "MealPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HighProtein",
                table: "MealPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LowCarb",
                table: "MealPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LowFat",
                table: "MealPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Vegan",
                table: "MealPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Vegetarian",
                table: "MealPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "WeeklyBudget",
                table: "MealPlans",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsiderBudget",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "ExcludedProducts",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "GlutenFree",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "HighProtein",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "LowCarb",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "LowFat",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "Vegan",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "Vegetarian",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "WeeklyBudget",
                table: "MealPlans");
        }
    }
}
