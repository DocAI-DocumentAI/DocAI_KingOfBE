using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Domain.Migrations
{
    /// <inheritdoc />
    public partial class migration2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "CityCode",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "District",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "DistrictCode",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "Province",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "ProvinceCode",
                table: "Members");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "CreatAt", "Email", "FullName", "Password", "Phone", "Role", "TwoFactorEnabled", "TwoFactorMethod", "UpdateAt", "UserName" },
                values: new object[] { new Guid("34eed860-a6e6-4774-b5db-1b44ae54e4cf"), new DateTime(2025, 5, 14, 8, 40, 44, 325, DateTimeKind.Utc).AddTicks(1361), "admin@gmail.com", "Admin", "jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=", "0847911068", "Manager", false, "Email", new DateTime(2025, 5, 14, 8, 40, 44, 325, DateTimeKind.Utc).AddTicks(1493), "admin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: new Guid("34eed860-a6e6-4774-b5db-1b44ae54e4cf"));

            migrationBuilder.AlterColumn<int>(
                name: "Role",
                table: "Users",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CityCode",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistrictCode",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Province",
                table: "Members",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvinceCode",
                table: "Members",
                type: "text",
                nullable: true);
        }
    }
}
