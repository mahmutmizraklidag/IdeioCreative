using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdeioCreative.Migrations
{
    /// <inheritdoc />
    public partial class bannerimg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BannerImage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BannerImage",
                table: "SiteSettings");
        }
    }
}
