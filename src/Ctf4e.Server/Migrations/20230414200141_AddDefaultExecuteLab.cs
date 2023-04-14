using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ctf4e.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultExecuteLab : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DefaultExecuteLabEnd",
                table: "Slots",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultExecuteLabId",
                table: "Slots",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Slots_DefaultExecuteLabId",
                table: "Slots",
                column: "DefaultExecuteLabId");

            migrationBuilder.AddForeignKey(
                name: "FK_Slots_Labs_DefaultExecuteLabId",
                table: "Slots",
                column: "DefaultExecuteLabId",
                principalTable: "Labs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Slots_Labs_DefaultExecuteLabId",
                table: "Slots");

            migrationBuilder.DropIndex(
                name: "IX_Slots_DefaultExecuteLabId",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "DefaultExecuteLabEnd",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "DefaultExecuteLabId",
                table: "Slots");
        }
    }
}
