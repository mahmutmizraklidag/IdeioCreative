using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdeioCreative.Migrations
{
    /// <inheritdoc />
    public partial class youlink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "YouTubeLink",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "YouTubeLink",
                table: "SiteSettings");
        }
    }
}
