using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.API.Migrations
{
    /// <inheritdoc />
    public partial class migration3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: new Guid("8f7a8fd0-a972-4776-b451-f1d866f5e0bb"));

            migrationBuilder.AlterColumn<string>(
                name: "ProvinceCode",
                table: "Members",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Province",
                table: "Members",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DistrictCode",
                table: "Members",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "District",
                table: "Members",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CityCode",
                table: "Members",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Members",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Members",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "CreatAt", "Email", "FullName", "Password", "Phone", "Role", "TwoFactorEnabled", "TwoFactorMethod", "UpdateAt", "UserName" },
                values: new object[] { new Guid("e97befe4-2bfc-44b2-ad46-8310a1bec04f"), new DateTime(2025, 5, 3, 9, 38, 31, 945, DateTimeKind.Utc).AddTicks(3264), "admin@gmail.com", "Admin", "jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=", "0847911068", "Manager", false, "Email", new DateTime(2025, 5, 3, 9, 38, 31, 945, DateTimeKind.Utc).AddTicks(3380), "admin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: new Guid("e97befe4-2bfc-44b2-ad46-8310a1bec04f"));

            migrationBuilder.AlterColumn<string>(
                name: "ProvinceCode",
                table: "Members",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Province",
                table: "Members",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DistrictCode",
                table: "Members",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "District",
                table: "Members",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CityCode",
                table: "Members",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Members",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Members",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "CreatAt", "Email", "FullName", "Password", "Phone", "Role", "TwoFactorEnabled", "TwoFactorMethod", "UpdateAt", "UserName" },
                values: new object[] { new Guid("8f7a8fd0-a972-4776-b451-f1d866f5e0bb"), new DateTime(2025, 5, 3, 7, 44, 47, 214, DateTimeKind.Utc).AddTicks(9415), "admin@gmail.com", "Admin", "jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=", "0847911068", "Manager", false, "Email", new DateTime(2025, 5, 3, 7, 44, 47, 214, DateTimeKind.Utc).AddTicks(9582), "admin" });
        }
    }
}
