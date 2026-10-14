using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdeioCreative.Migrations
{
    /// <inheritdoc />
    public partial class ServicePageTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PageType",
                table: "Services",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PageType",
                table: "Services");
        }
    }
}
