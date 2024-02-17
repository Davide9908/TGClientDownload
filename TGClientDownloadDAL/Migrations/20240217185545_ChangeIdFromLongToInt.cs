using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TGClientDownloadDAL.Migrations
{
    /// <inheritdoc />
    public partial class ChangeIdFromLongToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MessageId",
                table: "TelegramMessage",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "MessageId",
                table: "TelegramMessage",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
