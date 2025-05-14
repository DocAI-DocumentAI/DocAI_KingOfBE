using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Domain.Migrations
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
                keyValue: new Guid("34eed860-a6e6-4774-b5db-1b44ae54e4cf"));

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "CreatAt", "Email", "FullName", "Password", "Phone", "Role", "TwoFactorEnabled", "TwoFactorMethod", "UpdateAt", "UserName" },
                values: new object[] { new Guid("9ffd9537-0f71-4ffa-970d-376d3cd2d884"), new DateTime(2025, 5, 14, 8, 44, 45, 444, DateTimeKind.Utc).AddTicks(1106), "admin@gmail.com", "Admin", "OGhu6gLFs9JGQWIHBGkEkD9TXcNLJ2a1ej2ndRdGQffzCmq231zdWJAEzrK2fZ7w", "0847911068", "Manager", false, "Email", new DateTime(2025, 5, 14, 8, 44, 45, 444, DateTimeKind.Utc).AddTicks(1256), "admin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: new Guid("9ffd9537-0f71-4ffa-970d-376d3cd2d884"));

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "CreatAt", "Email", "FullName", "Password", "Phone", "Role", "TwoFactorEnabled", "TwoFactorMethod", "UpdateAt", "UserName" },
                values: new object[] { new Guid("34eed860-a6e6-4774-b5db-1b44ae54e4cf"), new DateTime(2025, 5, 14, 8, 40, 44, 325, DateTimeKind.Utc).AddTicks(1361), "admin@gmail.com", "Admin", "jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=", "0847911068", "Manager", false, "Email", new DateTime(2025, 5, 14, 8, 40, 44, 325, DateTimeKind.Utc).AddTicks(1493), "admin" });
        }
    }
}
