using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrumpStockAlert.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedAtUtcToTruthPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SavedAtUtc",
                table: "truth_posts",
                type: "datetimeoffset(7)",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SavedAtUtc",
                table: "truth_posts");
        }
    }
}
