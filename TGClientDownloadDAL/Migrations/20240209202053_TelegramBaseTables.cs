using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TGClientDownloadDAL.Migrations
{
    /// <inheritdoc />
    public partial class TelegramBaseTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TgChannel");

            migrationBuilder.CreateTable(
                name: "TelegramChat",
                columns: table => new
                {
                    TelegramChatId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    AccessHash = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramChat", x => x.TelegramChatId);
                });

            migrationBuilder.CreateTable(
                name: "TelegramFile",
                columns: table => new
                {
                    TelegramFileId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileId = table.Column<long>(type: "bigint", nullable: false),
                    AccessHash = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramFile", x => x.TelegramFileId);
                });

            migrationBuilder.CreateTable(
                name: "TelegramChannel",
                columns: table => new
                {
                    TelegramChatId = table.Column<int>(type: "integer", nullable: false),
                    ChannelName = table.Column<string>(type: "text", nullable: false),
                    AutoDownloadEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    FileNameTemplate = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramChannel", x => x.TelegramChatId);
                    table.ForeignKey(
                        name: "FK_TelegramChannel_TelegramChat_TelegramChatId",
                        column: x => x.TelegramChatId,
                        principalTable: "TelegramChat",
                        principalColumn: "TelegramChatId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TelegramMediaDocument",
                columns: table => new
                {
                    TelegramFileId = table.Column<int>(type: "integer", nullable: false),
                    SourceChatId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    DataTransmitted = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DownloadStatus = table.Column<int>(type: "integer", nullable: false),
                    ErrorType = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramMediaDocument", x => x.TelegramFileId);
                    table.ForeignKey(
                        name: "FK_TelegramMediaDocument_TelegramChat_SourceChatId",
                        column: x => x.SourceChatId,
                        principalTable: "TelegramChat",
                        principalColumn: "TelegramChatId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TelegramMediaDocument_TelegramFile_TelegramFileId",
                        column: x => x.TelegramFileId,
                        principalTable: "TelegramFile",
                        principalColumn: "TelegramFileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramChat_ChatId_AccessHash",
                table: "TelegramChat",
                columns: new[] { "ChatId", "AccessHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelegramFile_FileId_AccessHash",
                table: "TelegramFile",
                columns: new[] { "FileId", "AccessHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelegramMediaDocument_SourceChatId",
                table: "TelegramMediaDocument",
                column: "SourceChatId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelegramChannel");

            migrationBuilder.DropTable(
                name: "TelegramMediaDocument");

            migrationBuilder.DropTable(
                name: "TelegramChat");

            migrationBuilder.DropTable(
                name: "TelegramFile");

            migrationBuilder.CreateTable(
                name: "TgChannel",
                columns: table => new
                {
                    ChannelId = table.Column<int>(type: "integer", nullable: false),
                    AccessHash = table.Column<long>(type: "bigint", nullable: false),
                    ChannelName = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TgChannel", x => new { x.ChannelId, x.AccessHash });
                });
        }
    }
}
