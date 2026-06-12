using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserParamsAdd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlannedMeals_MealPlans_MealPlanId",
                table: "PlannedMeals");

            migrationBuilder.DropIndex(
                name: "IX_PlannedMeals_MealPlanId",
                table: "PlannedMeals");

            migrationBuilder.DropColumn(
                name: "MealPlanId",
                table: "PlannedMeals");

            migrationBuilder.AddColumn<string>(
                name: "ActivityLevel",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Age",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HeightCm",
                table: "Users",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightKg",
                table: "Users",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "MealPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityLevel",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Age",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HeightCm",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "MealPlans");

            migrationBuilder.AddColumn<Guid>(
                name: "MealPlanId",
                table: "PlannedMeals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlannedMeals_MealPlanId",
                table: "PlannedMeals",
                column: "MealPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlannedMeals_MealPlans_MealPlanId",
                table: "PlannedMeals",
                column: "MealPlanId",
                principalTable: "MealPlans",
                principalColumn: "Id");
        }
    }
}
