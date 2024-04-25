﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduNexDB.Migrations
{
    /// <inheritdoc />
    public partial class addStatusToTeacher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Teachers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Teachers");
        }
    }
}
