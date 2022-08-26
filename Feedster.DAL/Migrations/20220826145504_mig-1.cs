using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Feedster.DAL.Migrations
{
    public partial class mig1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeedGroup_Group_TagsGroupId",
                table: "FeedGroup");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Group",
                table: "Group");

            migrationBuilder.RenameTable(
                name: "Group",
                newName: "Groups");

            migrationBuilder.RenameColumn(
                name: "TagsGroupId",
                table: "FeedGroup",
                newName: "GroupsGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_FeedGroup_TagsGroupId",
                table: "FeedGroup",
                newName: "IX_FeedGroup_GroupsGroupId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Groups",
                table: "Groups",
                column: "GroupId");

            migrationBuilder.InsertData(
                table: "Groups",
                columns: new[] { "GroupId", "Name" },
                values: new object[] { 1, "Tech News" });

            migrationBuilder.InsertData(
                table: "Groups",
                columns: new[] { "GroupId", "Name" },
                values: new object[] { 2, "Local News" });

            migrationBuilder.InsertData(
                table: "Groups",
                columns: new[] { "GroupId", "Name" },
                values: new object[] { 3, "Security And Privacy" });

            migrationBuilder.AddForeignKey(
                name: "FK_FeedGroup_Groups_GroupsGroupId",
                table: "FeedGroup",
                column: "GroupsGroupId",
                principalTable: "Groups",
                principalColumn: "GroupId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeedGroup_Groups_GroupsGroupId",
                table: "FeedGroup");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Groups",
                table: "Groups");

            migrationBuilder.DeleteData(
                table: "Groups",
                keyColumn: "GroupId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Groups",
                keyColumn: "GroupId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Groups",
                keyColumn: "GroupId",
                keyValue: 3);

            migrationBuilder.RenameTable(
                name: "Groups",
                newName: "Group");

            migrationBuilder.RenameColumn(
                name: "GroupsGroupId",
                table: "FeedGroup",
                newName: "TagsGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_FeedGroup_GroupsGroupId",
                table: "FeedGroup",
                newName: "IX_FeedGroup_TagsGroupId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Group",
                table: "Group",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_FeedGroup_Group_TagsGroupId",
                table: "FeedGroup",
                column: "TagsGroupId",
                principalTable: "Group",
                principalColumn: "GroupId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
