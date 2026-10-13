using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunicationApi.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddChatSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatSettings",
                columns: table => new
                {
                    Target = table.Column<int>(type: "INTEGER", nullable: false),
                    ChatId = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSettings", x => new { x.Target, x.ChatId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatSettings");
        }
    }
}
