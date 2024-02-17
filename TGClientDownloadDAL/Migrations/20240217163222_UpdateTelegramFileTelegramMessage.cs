using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TGClientDownloadDAL.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTelegramFileTelegramMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TelegramMessageId",
                table: "TelegramFile",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TelegramMessage",
                columns: table => new
                {
                    TelegramMessageId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramMessage", x => x.TelegramMessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramFile_TelegramMessageId",
                table: "TelegramFile",
                column: "TelegramMessageId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TelegramFile_TelegramMessage_TelegramMessageId",
                table: "TelegramFile",
                column: "TelegramMessageId",
                principalTable: "TelegramMessage",
                principalColumn: "TelegramMessageId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TelegramFile_TelegramMessage_TelegramMessageId",
                table: "TelegramFile");

            migrationBuilder.DropTable(
                name: "TelegramMessage");

            migrationBuilder.DropIndex(
                name: "IX_TelegramFile_TelegramMessageId",
                table: "TelegramFile");

            migrationBuilder.DropColumn(
                name: "TelegramMessageId",
                table: "TelegramFile");
        }
    }
}
