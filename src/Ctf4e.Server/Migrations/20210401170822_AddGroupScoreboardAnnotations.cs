using Microsoft.EntityFrameworkCore.Migrations;

namespace Ctf4e.Server.Migrations
{
    public partial class AddGroupScoreboardAnnotations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScoreboardAnnotation",
                table: "Groups",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ScoreboardAnnotationHoverText",
                table: "Groups",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScoreboardAnnotation",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "ScoreboardAnnotationHoverText",
                table: "Groups");
        }
    }
}
