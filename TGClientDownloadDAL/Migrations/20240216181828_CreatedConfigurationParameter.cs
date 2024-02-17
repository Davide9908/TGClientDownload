using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TGClientDownloadDAL.Migrations
{
    /// <inheritdoc />
    public partial class CreatedConfigurationParameter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_ConfigParam",
                columns: table => new
                {
                    ConfigurationParameterId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParameterName = table.Column<string>(type: "text", nullable: false),
                    ParameterValue = table.Column<string>(type: "text", nullable: false),
                    ParameterType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_ConfigParam", x => x.ConfigurationParameterId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_system_ConfigParam_ParameterName",
                table: "system_ConfigParam",
                column: "ParameterName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_ConfigParam");
        }
    }
}
