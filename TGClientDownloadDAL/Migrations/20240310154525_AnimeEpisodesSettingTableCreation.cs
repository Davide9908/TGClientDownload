using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TGClientDownloadDAL.Migrations
{
    /// <inheritdoc />
    public partial class AnimeEpisodesSettingTableCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnimeEpisodesSetting",
                columns: table => new
                {
                    TelegramChannelId = table.Column<int>(type: "integer", nullable: false),
                    FileNameTemplate = table.Column<string>(type: "text", nullable: true),
                    MALAnimeId = table.Column<int>(type: "integer", nullable: true),
                    AnimeFolderPath = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeEpisodesSetting", x => x.TelegramChannelId);
                    table.ForeignKey(
                        name: "FK_AnimeEpisodesSetting_TelegramChannel_TelegramChannelId",
                        column: x => x.TelegramChannelId,
                        principalTable: "TelegramChannel",
                        principalColumn: "TelegramChatId",
                        onDelete: ReferentialAction.Cascade);
                });

            var script = Properties.Resources.AnimeEpisodesSettingTableCreation.ToString();
            migrationBuilder.Sql(script);

            migrationBuilder.DropColumn(
                name: "FileNameTemplate",
                table: "TelegramChannel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimeEpisodesSetting");

            migrationBuilder.AddColumn<string>(
                name: "FileNameTemplate",
                table: "TelegramChannel",
                type: "text",
                nullable: true);
        }
    }
}
