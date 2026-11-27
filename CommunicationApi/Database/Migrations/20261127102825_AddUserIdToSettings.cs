using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunicationApi.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ProviderSettings;");
            migrationBuilder.Sql("DELETE FROM ChatSettings;");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProviderSettings",
                table: "ProviderSettings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatSettings",
                table: "ChatSettings");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "ProviderSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "ChatSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProviderSettings",
                table: "ProviderSettings",
                columns: new[] { "UserId", "Target" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatSettings",
                table: "ChatSettings",
                columns: new[] { "UserId", "Target", "ChatId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChatSettings_Users_UserId",
                table: "ChatSettings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProviderSettings_Users_UserId",
                table: "ProviderSettings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ProviderSettings;");
            migrationBuilder.Sql("DELETE FROM ChatSettings;");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatSettings_Users_UserId",
                table: "ChatSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_ProviderSettings_Users_UserId",
                table: "ProviderSettings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProviderSettings",
                table: "ProviderSettings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatSettings",
                table: "ChatSettings");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ProviderSettings");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ChatSettings");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProviderSettings",
                table: "ProviderSettings",
                column: "Target");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatSettings",
                table: "ChatSettings",
                columns: new[] { "Target", "ChatId" });
        }
    }
}
