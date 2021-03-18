using Microsoft.EntityFrameworkCore.Migrations;

namespace Ctf4e.Server.Migrations
{
    public partial class AddLabVisibleColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Visible",
                table: "Labs",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Visible",
                table: "Labs");
        }
    }
}
