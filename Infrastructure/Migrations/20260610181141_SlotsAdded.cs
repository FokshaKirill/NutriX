using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SlotsAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlannedMeals_MealPlans_MealPlanId",
                table: "PlannedMeals");

            migrationBuilder.DropForeignKey(
                name: "FK_PlannedMeals_MealTypes_MealTypeId",
                table: "PlannedMeals");

            migrationBuilder.RenameColumn(
                name: "DayOffset",
                table: "PlannedMeals",
                newName: "Role");

            migrationBuilder.AddColumn<int>(
                name: "Tags",
                table: "Recipes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "MealTypeId",
                table: "PlannedMeals",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "MealPlanId",
                table: "PlannedMeals",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "MealSlotId",
                table: "PlannedMeals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "MealSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOffset = table.Column<int>(type: "integer", nullable: false),
                    MealTypeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealSlots_MealPlans_MealPlanId",
                        column: x => x.MealPlanId,
                        principalTable: "MealPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MealSlots_MealTypes_MealTypeId",
                        column: x => x.MealTypeId,
                        principalTable: "MealTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedMeals_MealSlotId",
                table: "PlannedMeals",
                column: "MealSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_MealSlots_MealPlanId",
                table: "MealSlots",
                column: "MealPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MealSlots_MealTypeId",
                table: "MealSlots",
                column: "MealTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlannedMeals_MealPlans_MealPlanId",
                table: "PlannedMeals",
                column: "MealPlanId",
                principalTable: "MealPlans",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlannedMeals_MealSlots_MealSlotId",
                table: "PlannedMeals",
                column: "MealSlotId",
                principalTable: "MealSlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlannedMeals_MealTypes_MealTypeId",
                table: "PlannedMeals",
                column: "MealTypeId",
                principalTable: "MealTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlannedMeals_MealPlans_MealPlanId",
                table: "PlannedMeals");

            migrationBuilder.DropForeignKey(
                name: "FK_PlannedMeals_MealSlots_MealSlotId",
                table: "PlannedMeals");

            migrationBuilder.DropForeignKey(
                name: "FK_PlannedMeals_MealTypes_MealTypeId",
                table: "PlannedMeals");

            migrationBuilder.DropTable(
                name: "MealSlots");

            migrationBuilder.DropIndex(
                name: "IX_PlannedMeals_MealSlotId",
                table: "PlannedMeals");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "MealSlotId",
                table: "PlannedMeals");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "PlannedMeals",
                newName: "DayOffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "MealTypeId",
                table: "PlannedMeals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "MealPlanId",
                table: "PlannedMeals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PlannedMeals_MealPlans_MealPlanId",
                table: "PlannedMeals",
                column: "MealPlanId",
                principalTable: "MealPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlannedMeals_MealTypes_MealTypeId",
                table: "PlannedMeals",
                column: "MealTypeId",
                principalTable: "MealTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
