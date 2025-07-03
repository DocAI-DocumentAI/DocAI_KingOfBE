using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Auth.Domain.Migrations
{
    /// <inheritdoc />
    public partial class migration4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveKeys");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("4095917a-b071-44bd-8014-a9297dd7ccbf"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("441a53eb-c8f0-4cae-9b5a-a8e48af97fca"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("629be188-40ce-4842-bec0-4009a5952b6b"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("6ef2f49a-15bc-409f-bfcf-191c0777b760"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("e05974f6-04bc-4e45-b9a4-7eb24390cf65"));

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: new Guid("8bf13891-1ce9-405c-add9-0ada93308671"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 3, 10, 3, 36, 648, DateTimeKind.Utc).AddTicks(8576), new DateTime(2025, 7, 3, 10, 3, 36, 648, DateTimeKind.Utc).AddTicks(8683) });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: new Guid("d8854d21-8fae-46aa-b51b-0de060b92ee3"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 3, 10, 3, 36, 648, DateTimeKind.Utc).AddTicks(8765), new DateTime(2025, 7, 3, 10, 3, 36, 648, DateTimeKind.Utc).AddTicks(8766) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 3, 10, 3, 36, 649, DateTimeKind.Utc).AddTicks(6550), new DateTime(2025, 7, 3, 10, 3, 36, 649, DateTimeKind.Utc).AddTicks(6650) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 3, 10, 3, 36, 649, DateTimeKind.Utc).AddTicks(6727), new DateTime(2025, 7, 3, 10, 3, 36, 649, DateTimeKind.Utc).AddTicks(6728) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 3, 10, 3, 36, 649, DateTimeKind.Utc).AddTicks(6730), new DateTime(2025, 7, 3, 10, 3, 36, 649, DateTimeKind.Utc).AddTicks(6730) });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("2e29a4df-916e-4ce7-ac3b-ea5cf1655f7a"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("8e7d55e4-67d3-4b73-9995-21b163493136") },
                    { new Guid("466b8527-2b48-4213-804b-d4b8703e2912"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb") },
                    { new Guid("8160062d-42a6-496f-ac39-facbdf2c414b"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f") },
                    { new Guid("c58d11aa-99ef-4154-bf8c-ef68bc76af63"), new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb") },
                    { new Guid("e929753e-7b49-4896-88a6-6bcc70d05427"), new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6") }
                });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 3, 10, 3, 36, 649, DateTimeKind.Utc).AddTicks(9090), new DateTime(2025, 7, 3, 10, 3, 36, 649, DateTimeKind.Utc).AddTicks(9179) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 3, 10, 3, 36, 649, DateTimeKind.Utc).AddTicks(9257), new DateTime(2025, 7, 3, 10, 3, 36, 649, DateTimeKind.Utc).AddTicks(9257) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 3, 10, 3, 36, 649, DateTimeKind.Utc).AddTicks(9255), new DateTime(2025, 7, 3, 10, 3, 36, 649, DateTimeKind.Utc).AddTicks(9255) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 3, 10, 3, 36, 649, DateTimeKind.Utc).AddTicks(9253), new DateTime(2025, 7, 3, 10, 3, 36, 649, DateTimeKind.Utc).AddTicks(9253) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 3, 10, 3, 36, 674, DateTimeKind.Utc).AddTicks(6407), "eYT2WXQtT+wDZ0LZuMxAnMeLPWVKJrQiTtQ3hX6sNR0tNAsWIK2iTJ6MVGrI4o0L", new DateTime(2025, 7, 3, 10, 3, 36, 674, DateTimeKind.Utc).AddTicks(6614) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 3, 10, 3, 36, 694, DateTimeKind.Utc).AddTicks(8096), "lnSxJu92BNFXEnzlPb+57IiLUEKgcHWSXjIwTk/gY1oslwYAmIfYWS7lNbvS6zrz", new DateTime(2025, 7, 3, 10, 3, 36, 694, DateTimeKind.Utc).AddTicks(8103) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 3, 10, 3, 36, 732, DateTimeKind.Utc).AddTicks(4177), "g8MNPYv3YpsR7rBvesb9XMVEhpO5eluy5eW4zP1GGdzV9p0UYGg0HVbVYfGZayrp", new DateTime(2025, 7, 3, 10, 3, 36, 732, DateTimeKind.Utc).AddTicks(4185) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("fd05266c-baf5-49bb-a846-554461bcc411"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 3, 10, 3, 36, 713, DateTimeKind.Utc).AddTicks(6018), "87yKBhE8FyCr/WEaJZsx0zyPXayCnk3uBiSVyFd+vnVJpXVTu8wDiad/WaoE8Bax", new DateTime(2025, 7, 3, 10, 3, 36, 713, DateTimeKind.Utc).AddTicks(6026) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("2e29a4df-916e-4ce7-ac3b-ea5cf1655f7a"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("466b8527-2b48-4213-804b-d4b8703e2912"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("8160062d-42a6-496f-ac39-facbdf2c414b"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("c58d11aa-99ef-4154-bf8c-ef68bc76af63"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("e929753e-7b49-4896-88a6-6bcc70d05427"));

            migrationBuilder.CreateTable(
                name: "ActiveKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivationCode = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActiveKeys_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActiveKeys_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActiveKeys_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActiveKeys_Users_UsedByUserId",
                        column: x => x.UsedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ActiveKeys",
                columns: new[] { "Id", "ActivationCode", "CreatedAt", "CreatedByUserId", "DepartmentId", "RoleId", "Status", "UpdatedAt", "UsedByUserId" },
                values: new object[] { new Guid("50b64957-bae3-4377-aa7a-fee36d25ccd6"), "P4rBZtdXa5YvEGJNmKLcQq7RfW9HU61o", new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(1421), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), "On", new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(1527), null });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: new Guid("8bf13891-1ce9-405c-add9-0ada93308671"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(7563), new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(7663) });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: new Guid("d8854d21-8fae-46aa-b51b-0de060b92ee3"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(7739), new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(7740) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(9885), new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(9976) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(61), new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(61) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(63), new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(63) });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("4095917a-b071-44bd-8014-a9297dd7ccbf"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb") },
                    { new Guid("441a53eb-c8f0-4cae-9b5a-a8e48af97fca"), new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb") },
                    { new Guid("629be188-40ce-4842-bec0-4009a5952b6b"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("8e7d55e4-67d3-4b73-9995-21b163493136") },
                    { new Guid("6ef2f49a-15bc-409f-bfcf-191c0777b760"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f") },
                    { new Guid("e05974f6-04bc-4e45-b9a4-7eb24390cf65"), new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6") }
                });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(2876), new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(2969) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(3048), new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(3048) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(3046), new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(3047) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(3044), new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(3044) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 28, 10, 2, 53, 355, DateTimeKind.Utc).AddTicks(6989), "lAb/k5Augxm4v5KJoEWgkItbHiGSZoA/iEqIYLYQH2EGZTht2bebXj5vEzYeS5Ah", new DateTime(2025, 6, 28, 10, 2, 53, 355, DateTimeKind.Utc).AddTicks(7097) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 28, 10, 2, 53, 375, DateTimeKind.Utc).AddTicks(9262), "bFB0dDY+qUIrzTSi+joSfiOeUbGBmoBNtlI7m2KEWBlfk7KzOH0EPbZMumZIvJnc", new DateTime(2025, 6, 28, 10, 2, 53, 375, DateTimeKind.Utc).AddTicks(9271) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 28, 10, 2, 53, 416, DateTimeKind.Utc).AddTicks(3836), "pAZwp+b7CkfzJqNAcT1axeS9Btvlaoc1G/aNDvCg9JgT4w3tsJK65jBArqE9aceN", new DateTime(2025, 6, 28, 10, 2, 53, 416, DateTimeKind.Utc).AddTicks(3843) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("fd05266c-baf5-49bb-a846-554461bcc411"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 28, 10, 2, 53, 395, DateTimeKind.Utc).AddTicks(9915), "Ddb4IpvIOUdDtvT7aw2XMkoNa17IeKLib28+pEv4CGDn8srSwbxfMb69lOw4cPbC", new DateTime(2025, 6, 28, 10, 2, 53, 395, DateTimeKind.Utc).AddTicks(9920) });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveKeys_CreatedByUserId",
                table: "ActiveKeys",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveKeys_DepartmentId",
                table: "ActiveKeys",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveKeys_RoleId",
                table: "ActiveKeys",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveKeys_UsedByUserId",
                table: "ActiveKeys",
                column: "UsedByUserId");
        }
    }
}
