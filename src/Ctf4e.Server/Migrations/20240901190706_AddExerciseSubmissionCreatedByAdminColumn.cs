using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ctf4e.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseSubmissionCreatedByAdminColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CreatedByAdmin",
                table: "ExerciseSubmissions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByAdmin",
                table: "ExerciseSubmissions");
        }
    }
}
