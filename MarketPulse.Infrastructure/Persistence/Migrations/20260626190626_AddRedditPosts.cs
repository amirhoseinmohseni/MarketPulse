using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRedditPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RedditPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SearchQueryId = table.Column<Guid>(type: "uuid", nullable: true),
                    RedditPostId = table.Column<string>(type: "text", nullable: false),
                    Subreddit = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    SelfText = table.Column<string>(type: "text", nullable: true),
                    Url = table.Column<string>(type: "text", nullable: true),
                    Permalink = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    CommentCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedditPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RedditPosts_AnalysisRequests_AnalysisRequestId",
                        column: x => x.AnalysisRequestId,
                        principalTable: "AnalysisRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RedditPosts_SearchQueries_SearchQueryId",
                        column: x => x.SearchQueryId,
                        principalTable: "SearchQueries",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RedditPosts_AnalysisRequestId_RedditPostId",
                table: "RedditPosts",
                columns: new[] { "AnalysisRequestId", "RedditPostId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RedditPosts_SearchQueryId",
                table: "RedditPosts",
                column: "SearchQueryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RedditPosts");
        }
    }
}
