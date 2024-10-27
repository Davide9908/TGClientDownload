using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TGClientDownloadDAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCourEpisodeNumberGap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_AnimeEpisodesSetting",
                table: "AnimeEpisodesSetting");

            migrationBuilder.AlterColumn<int>(
                name: "MALAnimeId",
                table: "AnimeEpisodesSetting",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<short>(
                name: "CourEpisodeNumberGap",
                table: "AnimeEpisodesSetting",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AnimeEpisodesSetting",
                table: "AnimeEpisodesSetting",
                column: "MALAnimeId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimeEpisodesSetting_TelegramChannelId",
                table: "AnimeEpisodesSetting",
                column: "TelegramChannelId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_AnimeEpisodesSetting",
                table: "AnimeEpisodesSetting");

            migrationBuilder.DropIndex(
                name: "IX_AnimeEpisodesSetting_TelegramChannelId",
                table: "AnimeEpisodesSetting");

            migrationBuilder.DropColumn(
                name: "CourEpisodeNumberGap",
                table: "AnimeEpisodesSetting");

            migrationBuilder.AlterColumn<int>(
                name: "MALAnimeId",
                table: "AnimeEpisodesSetting",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AnimeEpisodesSetting",
                table: "AnimeEpisodesSetting",
                column: "TelegramChannelId");
        }
    }
}
