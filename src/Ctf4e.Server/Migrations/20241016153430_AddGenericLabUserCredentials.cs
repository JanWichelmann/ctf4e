using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ctf4e.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddGenericLabUserCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LabPassword",
                table: "Users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LabUserName",
                table: "Users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LabPassword",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LabUserName",
                table: "Users");
        }
    }
}
