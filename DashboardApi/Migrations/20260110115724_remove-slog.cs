using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DashboardApi.Migrations
{
    /// <inheritdoc />
    public partial class removeslog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Dashboards");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Dashboards",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Dashboards");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Dashboards",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
