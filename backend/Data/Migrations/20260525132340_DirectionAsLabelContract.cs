using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrumpStockAlert.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DirectionAsLabelContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_post_analyses_Direction_neg50_50",
                table: "post_analyses");

            migrationBuilder.Sql(
                """
                ALTER TABLE post_analyses
                ALTER COLUMN "Direction"
                TYPE character varying(30)
                USING CASE
                    WHEN "Direction" > 0 THEN 'positive'
                    WHEN "Direction" < 0 THEN 'negative'
                    ELSE 'neutral'
                END;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_post_analyses_Direction_label",
                table: "post_analyses",
                sql: "\"Direction\" IN ('positive', 'negative', 'neutral', 'mixed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_post_analyses_Direction_label",
                table: "post_analyses");

            migrationBuilder.Sql(
                """
                ALTER TABLE post_analyses
                ALTER COLUMN "Direction"
                TYPE integer
                USING CASE
                    WHEN "Direction" = 'positive' THEN 25
                    WHEN "Direction" = 'negative' THEN -35
                    ELSE 0
                END;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_post_analyses_Direction_neg50_50",
                table: "post_analyses",
                sql: "\"Direction\" >= -50 AND \"Direction\" <= 50");
        }
    }
}
