using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunicationApi.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    Target = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });
        }
    }
}
