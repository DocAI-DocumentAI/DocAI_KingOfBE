using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.API.Migrations
{
    /// <inheritdoc />
    public partial class Migration2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TwoFactorMethod",
                table: "Users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "CreatAt", "Email", "FullName", "Password", "Phone", "Role", "TwoFactorEnabled", "TwoFactorMethod", "UpdateAt", "UserName" },
                values: new object[] { new Guid("8f7a8fd0-a972-4776-b451-f1d866f5e0bb"), new DateTime(2025, 5, 3, 7, 44, 47, 214, DateTimeKind.Utc).AddTicks(9415), "admin@gmail.com", "Admin", "jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=", "0847911068", "Manager", false, "Email", new DateTime(2025, 5, 3, 7, 44, 47, 214, DateTimeKind.Utc).AddTicks(9582), "admin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: new Guid("8f7a8fd0-a972-4776-b451-f1d866f5e0bb"));

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "TwoFactorMethod",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
