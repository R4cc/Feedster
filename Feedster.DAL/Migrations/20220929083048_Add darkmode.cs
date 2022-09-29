using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Feedster.DAL.Migrations
{
    public partial class Adddarkmode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Folders",
                keyColumn: "FolderId",
                keyValue: 3);

            migrationBuilder.AddColumn<bool>(
                name: "IsDarkMode",
                table: "UserSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Tags",
                table: "Articles",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PublicationDate",
                table: "Articles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ArticleLink",
                table: "Articles",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.InsertData(
                table: "Folders",
                columns: new[] { "FolderId", "Name" },
                values: new object[] { 2, "Security And Privacy" });

            migrationBuilder.UpdateData(
                table: "UserSettings",
                keyColumn: "UserSettingsId",
                keyValue: 1,
                columns: new[] { "ArticleExpirationAfterDays", "IsDarkMode" },
                values: new object[] { 0, true });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Folders",
                keyColumn: "FolderId",
                keyValue: 2);

            migrationBuilder.DropColumn(
                name: "IsDarkMode",
                table: "UserSettings");

            migrationBuilder.AlterColumn<string>(
                name: "Tags",
                table: "Articles",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PublicationDate",
                table: "Articles",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "ArticleLink",
                table: "Articles",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.InsertData(
                table: "Folders",
                columns: new[] { "FolderId", "Name" },
                values: new object[] { 3, "Security And Privacy" });

            migrationBuilder.UpdateData(
                table: "UserSettings",
                keyColumn: "UserSettingsId",
                keyValue: 1,
                column: "ArticleExpirationAfterDays",
                value: 30);
        }
    }
}
