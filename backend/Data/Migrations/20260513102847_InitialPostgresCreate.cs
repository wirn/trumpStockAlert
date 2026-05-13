using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TrumpStockAlert.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgresCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "truth_posts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Author = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    CollectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    SavedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_truth_posts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "post_analyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    MarketImpactScore = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reasoning = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AffectedAssetsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Confidence = table.Column<int>(type: "integer", nullable: true),
                    AnalyzerVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RawAiResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnalyzedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_analyses", x => x.Id);
                    table.CheckConstraint("CK_post_analyses_Confidence_1_100", "[Confidence] IS NULL OR ([Confidence] >= 1 AND [Confidence] <= 100)");
                    table.CheckConstraint("CK_post_analyses_MarketImpactScore_1_100", "[MarketImpactScore] >= 1 AND [MarketImpactScore] <= 100");
                    table.ForeignKey(
                        name: "FK_post_analyses_truth_posts_PostId",
                        column: x => x.PostId,
                        principalTable: "truth_posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    PostAnalysisId = table.Column<int>(type: "integer", nullable: false),
                    AlertType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Threshold = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    SendStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.Id);
                    table.CheckConstraint("CK_alerts_Threshold_1_100", "[Threshold] >= 1 AND [Threshold] <= 100");
                    table.ForeignKey(
                        name: "FK_alerts_post_analyses_PostAnalysisId",
                        column: x => x.PostAnalysisId,
                        principalTable: "post_analyses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_alerts_truth_posts_PostId",
                        column: x => x.PostId,
                        principalTable: "truth_posts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_alerts_PostAnalysisId",
                table: "alerts",
                column: "PostAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_alerts_PostId",
                table: "alerts",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_alerts_SendStatus",
                table: "alerts",
                column: "SendStatus");

            migrationBuilder.CreateIndex(
                name: "IX_post_analyses_PostId",
                table: "post_analyses",
                column: "PostId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_truth_posts_Source_ExternalId",
                table: "truth_posts",
                columns: new[] { "Source", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "post_analyses");

            migrationBuilder.DropTable(
                name: "truth_posts");
        }
    }
}
