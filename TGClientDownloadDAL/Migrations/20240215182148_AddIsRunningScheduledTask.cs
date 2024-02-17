using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TGClientDownloadDAL.Migrations
{
    /// <inheritdoc />
    public partial class AddIsRunningScheduledTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRunning",
                table: "ScheduledTask",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRunning",
                table: "ScheduledTask");
        }
    }
}
