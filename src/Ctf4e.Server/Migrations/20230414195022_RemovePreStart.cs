using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ctf4e.Server.Migrations
{
    /// <inheritdoc />
    public partial class RemovePreStart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreStart",
                table: "LabExecutions");

            migrationBuilder.DropColumn(
                name: "IsPreStartAvailable",
                table: "Exercises");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PreStart",
                table: "LabExecutions",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsPreStartAvailable",
                table: "Exercises",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }
    }
}
