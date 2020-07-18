using Microsoft.EntityFrameworkCore.Migrations;

namespace Ctf4e.Server.Migrations
{
    public partial class FixLabExecutionRelation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabExecutions_Slots_SlotEntityId",
                table: "LabExecutions");

            migrationBuilder.DropIndex(
                name: "IX_LabExecutions_SlotEntityId",
                table: "LabExecutions");

            migrationBuilder.DropColumn(
                name: "SlotEntityId",
                table: "LabExecutions");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SlotEntityId",
                table: "LabExecutions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabExecutions_SlotEntityId",
                table: "LabExecutions",
                column: "SlotEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_LabExecutions_Slots_SlotEntityId",
                table: "LabExecutions",
                column: "SlotEntityId",
                principalTable: "Slots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
