using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TGClientDownloadDAL.Migrations
{
    /// <inheritdoc />
    public partial class TGChannelCreated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TgChannel",
                columns: table => new
                {
                    ChannelId = table.Column<int>(type: "integer", nullable: false),
                    AccessHash = table.Column<long>(type: "bigint", nullable: false),
                    ChannelName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TgChannel", x => new { x.ChannelId, x.AccessHash });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TgChannel");
        }
    }
}
