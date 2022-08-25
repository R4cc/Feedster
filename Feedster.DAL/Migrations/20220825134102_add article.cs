using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Feedster.DAL.Migrations
{
    public partial class addarticle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    ArticleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Guid = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FeedId = table.Column<int>(type: "int", nullable: false),
                    ArticleLink = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublicationDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.ArticleId);
                    table.ForeignKey(
                        name: "FK_Articles_Feeds_FeedId",
                        column: x => x.FeedId,
                        principalTable: "Feeds",
                        principalColumn: "FeedId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Feeds",
                columns: new[] { "FeedId", "Name", "RssUrl" },
                values: new object[] { 1, "ycombinator", "https://news.ycombinator.com/rss" });

            migrationBuilder.InsertData(
                table: "Feeds",
                columns: new[] { "FeedId", "Name", "RssUrl" },
                values: new object[] { 2, "wired", "https://wired.com/feed/rss" });

            migrationBuilder.InsertData(
                table: "Feeds",
                columns: new[] { "FeedId", "Name", "RssUrl" },
                values: new object[] { 3, "Threatpost", "https://threatpost.com/feed/" });

            migrationBuilder.CreateIndex(
                name: "IX_Articles_FeedId",
                table: "Articles",
                column: "FeedId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DeleteData(
                table: "Feeds",
                keyColumn: "FeedId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Feeds",
                keyColumn: "FeedId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Feeds",
                keyColumn: "FeedId",
                keyValue: 3);
        }
    }
}
