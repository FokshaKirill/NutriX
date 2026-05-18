using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTotalCostColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MealPlans_Users_UserId",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "TotalCost",
                table: "Recipes");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "MealPlans",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlans_Users_UserId",
                table: "MealPlans",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MealPlans_Users_UserId",
                table: "MealPlans");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "Recipes",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "MealPlans",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlans_Users_UserId",
                table: "MealPlans",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
