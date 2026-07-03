using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectedMarketItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectedMarketItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SearchQueryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    Url = table.Column<string>(type: "text", nullable: true),
                    Permalink = table.Column<string>(type: "text", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    CommentCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CollectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RawDataJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectedMarketItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectedMarketItems_AnalysisRequests_AnalysisRequestId",
                        column: x => x.AnalysisRequestId,
                        principalTable: "AnalysisRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectedMarketItems_SearchQueries_SearchQueryId",
                        column: x => x.SearchQueryId,
                        principalTable: "SearchQueries",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectedMarketItems_AnalysisRequestId_Source_ExternalId",
                table: "CollectedMarketItems",
                columns: new[] { "AnalysisRequestId", "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectedMarketItems_SearchQueryId",
                table: "CollectedMarketItems",
                column: "SearchQueryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectedMarketItems");
        }
    }
}
