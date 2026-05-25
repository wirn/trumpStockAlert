using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrumpStockAlert.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DirectionAsInteger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_post_analyses_Confidence_1_100",
                table: "post_analyses");

            migrationBuilder.AlterColumn<int>(
                name: "Direction",
                table: "post_analyses",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<int>(
                name: "Confidence",
                table: "post_analyses",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_post_analyses_Confidence_1_100",
                table: "post_analyses",
                sql: "\"Confidence\" >= 1 AND \"Confidence\" <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_post_analyses_Direction_neg50_50",
                table: "post_analyses",
                sql: "\"Direction\" >= -50 AND \"Direction\" <= 50");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_post_analyses_Confidence_1_100",
                table: "post_analyses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_post_analyses_Direction_neg50_50",
                table: "post_analyses");

            migrationBuilder.AlterColumn<string>(
                name: "Direction",
                table: "post_analyses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Confidence",
                table: "post_analyses",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddCheckConstraint(
                name: "CK_post_analyses_Confidence_1_100",
                table: "post_analyses",
                sql: "\"Confidence\" IS NULL OR (\"Confidence\" >= 1 AND \"Confidence\" <= 100)");
        }
    }
}
