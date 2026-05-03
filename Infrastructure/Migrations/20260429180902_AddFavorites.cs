using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MealTypes_Users_UserId",
                table: "MealTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Users_UserId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Users_UserId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Products_UserId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_MealTypes_UserId",
                table: "MealTypes");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "MealTypes");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Recipes",
                newName: "AuthorId");

            migrationBuilder.RenameIndex(
                name: "IX_Recipes_UserId",
                table: "Recipes",
                newName: "IX_Recipes_AuthorId");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DailyCalorieGoal",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyCarbsGoal",
                table: "Users",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyFatGoal",
                table: "Users",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyProteinGoal",
                table: "Users",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Recipes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Recipes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FavoriteRecipes",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteRecipes", x => new { x.UserId, x.RecipeId });
                    table.ForeignKey(
                        name: "FK_FavoriteRecipes_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FavoriteRecipes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteRecipes_RecipeId",
                table: "FavoriteRecipes",
                column: "RecipeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Users_AuthorId",
                table: "Recipes",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Users_AuthorId",
                table: "Recipes");

            migrationBuilder.DropTable(
                name: "FavoriteRecipes");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Bio",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DailyCalorieGoal",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DailyCarbsGoal",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DailyFatGoal",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DailyProteinGoal",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Recipes");

            migrationBuilder.RenameColumn(
                name: "AuthorId",
                table: "Recipes",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Recipes_AuthorId",
                table: "Recipes",
                newName: "IX_Recipes_UserId");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "MealTypes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_UserId",
                table: "Products",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MealTypes_UserId",
                table: "MealTypes",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MealTypes_Users_UserId",
                table: "MealTypes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Users_UserId",
                table: "Products",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Users_UserId",
                table: "Recipes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
