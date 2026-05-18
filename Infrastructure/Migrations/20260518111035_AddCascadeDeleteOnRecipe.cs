using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCascadeDeleteOnRecipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlannedMeals_Recipes_RecipeId",
                table: "PlannedMeals");

            migrationBuilder.DropIndex(
                name: "IX_PlannedMeals_MealPlanId_DayOffset_MealTypeId",
                table: "PlannedMeals");

            migrationBuilder.CreateIndex(
                name: "IX_PlannedMeals_MealPlanId",
                table: "PlannedMeals",
                column: "MealPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlannedMeals_Recipes_RecipeId",
                table: "PlannedMeals",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlannedMeals_Recipes_RecipeId",
                table: "PlannedMeals");

            migrationBuilder.DropIndex(
                name: "IX_PlannedMeals_MealPlanId",
                table: "PlannedMeals");

            migrationBuilder.CreateIndex(
                name: "IX_PlannedMeals_MealPlanId_DayOffset_MealTypeId",
                table: "PlannedMeals",
                columns: new[] { "MealPlanId", "DayOffset", "MealTypeId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PlannedMeals_Recipes_RecipeId",
                table: "PlannedMeals",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id");
        }
    }
}
