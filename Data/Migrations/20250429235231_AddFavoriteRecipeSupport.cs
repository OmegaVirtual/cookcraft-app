using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recipes.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFavoriteRecipeSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FavoriteRecipeIds",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FavoriteRecipeIds",
                table: "AspNetUsers");
        }
    }
}
