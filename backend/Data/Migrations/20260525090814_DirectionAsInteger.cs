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

Z            // AlterColumn<int> cannot cast varchar → integer automatically on PostgreSQL.
            // Explicit USING maps the old string values to their integer equivalents.
            migrationBuilder.Sql("""
                ALTER TABLE post_analyses
                ALTER COLUMN "Direction"
                TYPE integer
                USING CASE
                    WHEN "Direction" = 'negative' THEN -35
                    WHEN "Direction" = 'positive' THEN 25
                    WHEN "Direction" = 'neutral'  THEN 0
                    WHEN "Direction" IS NULL       THEN 0
                    ELSE CAST("Direction" AS integer)
                END;
                """);

            // Backfill any NULL Confidence values before adding the NOT NULL constraint.
            migrationBuilder.Sql("""
                UPDATE post_analyses SET "Confidence" = 0 WHERE "Confidence" IS NULL;
                """);

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

            // Reverse the integer → varchar conversion with best-effort label mapping.
            migrationBuilder.Sql("""
                ALTER TABLE post_analyses
                ALTER COLUMN "Direction"
                TYPE character varying(30)
                USING CASE
                    WHEN "Direction" = -35 THEN 'negative'
                    WHEN "Direction" =  25 THEN 'positive'
                    WHEN "Direction" =   0 THEN 'neutral'
                    ELSE "Direction"::text
                END;
                """);

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
