using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceBasedMarketInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MarketScore",
                table: "AnalysisResults",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "SignalStrength",
                table: "AnalysisResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AnalysisInsights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisInsights", x => x.Id);
                    table.CheckConstraint("CK_AnalysisInsights_Position_NonNegative", "\"Position\" >= 0");
                    table.CheckConstraint("CK_AnalysisInsights_Type_Range", "\"Type\" >= 0 AND \"Type\" <= 3");
                    table.ForeignKey(
                        name: "FK_AnalysisInsights_AnalysisResults_AnalysisResultId",
                        column: x => x.AnalysisResultId,
                        principalTable: "AnalysisResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisEvidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisInsightId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectedMarketItemId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisEvidences_AnalysisInsights_AnalysisInsightId",
                        column: x => x.AnalysisInsightId,
                        principalTable: "AnalysisInsights",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnalysisEvidences_CollectedMarketItems_CollectedMarketItemId",
                        column: x => x.CollectedMarketItemId,
                        principalTable: "CollectedMarketItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AnalysisResults_MarketScore_Range",
                table: "AnalysisResults",
                sql: "\"MarketScore\" IS NULL OR (\"MarketScore\" >= 0 AND \"MarketScore\" <= 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AnalysisResults_SignalStrength_Range",
                table: "AnalysisResults",
                sql: "\"SignalStrength\" >= 0 AND \"SignalStrength\" <= 2");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisEvidences_AnalysisInsightId_CollectedMarketItemId",
                table: "AnalysisEvidences",
                columns: new[] { "AnalysisInsightId", "CollectedMarketItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisEvidences_CollectedMarketItemId",
                table: "AnalysisEvidences",
                column: "CollectedMarketItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisInsights_AnalysisResultId_Type_Position",
                table: "AnalysisInsights",
                columns: new[] { "AnalysisResultId", "Type", "Position" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "AnalysisInsights" ("Id", "AnalysisResultId", "Type", "Position", "Text")
                SELECT
                    md5(
                        legacy."AnalysisResultId"::text
                        || ':' || legacy."Type"::text
                        || ':' || legacy."Ordinality"::text
                    )::uuid,
                    legacy."AnalysisResultId",
                    legacy."Type",
                    (legacy."Ordinality" - 1)::integer,
                    btrim(legacy."Text")
                FROM
                (
                    SELECT
                        result."Id" AS "AnalysisResultId",
                        0 AS "Type",
                        split.value AS "Text",
                        split.ordinality AS "Ordinality"
                    FROM "AnalysisResults" AS result
                    CROSS JOIN LATERAL regexp_split_to_table(
                        COALESCE(result."Strengths", ''),
                        ';'
                    ) WITH ORDINALITY AS split(value, ordinality)

                    UNION ALL

                    SELECT
                        result."Id",
                        1,
                        split.value,
                        split.ordinality
                    FROM "AnalysisResults" AS result
                    CROSS JOIN LATERAL regexp_split_to_table(
                        COALESCE(result."Weaknesses", ''),
                        ';'
                    ) WITH ORDINALITY AS split(value, ordinality)

                    UNION ALL

                    SELECT
                        result."Id",
                        2,
                        split.value,
                        split.ordinality
                    FROM "AnalysisResults" AS result
                    CROSS JOIN LATERAL regexp_split_to_table(
                        COALESCE(result."Opportunities", ''),
                        ';'
                    ) WITH ORDINALITY AS split(value, ordinality)

                    UNION ALL

                    SELECT
                        result."Id",
                        3,
                        split.value,
                        split.ordinality
                    FROM "AnalysisResults" AS result
                    CROSS JOIN LATERAL regexp_split_to_table(
                        COALESCE(result."Risks", ''),
                        ';'
                    ) WITH ORDINALITY AS split(value, ordinality)
                ) AS legacy
                WHERE btrim(legacy."Text") <> '';
                """);

            migrationBuilder.DropColumn(
                name: "Opportunities",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "Risks",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "Strengths",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "Weaknesses",
                table: "AnalysisResults");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Opportunities",
                table: "AnalysisResults",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Risks",
                table: "AnalysisResults",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Strengths",
                table: "AnalysisResults",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Weaknesses",
                table: "AnalysisResults",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "AnalysisResults" AS result
                SET
                    "Strengths" = COALESCE(
                        (
                            SELECT string_agg(insight."Text", '; ' ORDER BY insight."Position")
                            FROM "AnalysisInsights" AS insight
                            WHERE insight."AnalysisResultId" = result."Id"
                                AND insight."Type" = 0
                        ),
                        ''
                    ),
                    "Weaknesses" = COALESCE(
                        (
                            SELECT string_agg(insight."Text", '; ' ORDER BY insight."Position")
                            FROM "AnalysisInsights" AS insight
                            WHERE insight."AnalysisResultId" = result."Id"
                                AND insight."Type" = 1
                        ),
                        ''
                    ),
                    "Opportunities" = COALESCE(
                        (
                            SELECT string_agg(insight."Text", '; ' ORDER BY insight."Position")
                            FROM "AnalysisInsights" AS insight
                            WHERE insight."AnalysisResultId" = result."Id"
                                AND insight."Type" = 2
                        ),
                        ''
                    ),
                    "Risks" = COALESCE(
                        (
                            SELECT string_agg(insight."Text", '; ' ORDER BY insight."Position")
                            FROM "AnalysisInsights" AS insight
                            WHERE insight."AnalysisResultId" = result."Id"
                                AND insight."Type" = 3
                        ),
                        ''
                    );
                """);

            migrationBuilder.DropTable(
                name: "AnalysisEvidences");

            migrationBuilder.DropTable(
                name: "AnalysisInsights");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AnalysisResults_MarketScore_Range",
                table: "AnalysisResults");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AnalysisResults_SignalStrength_Range",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "SignalStrength",
                table: "AnalysisResults");

            migrationBuilder.Sql(
                """
                UPDATE "AnalysisResults"
                SET "MarketScore" = 0
                WHERE "MarketScore" IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "MarketScore",
                table: "AnalysisResults",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
