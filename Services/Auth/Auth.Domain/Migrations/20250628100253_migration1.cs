using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Auth.Domain.Migrations
{
    /// <inheritdoc />
    public partial class migration1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "text", nullable: false),
                    Password = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorMethod = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActiveKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivationCode = table.Column<string>(type: "text", nullable: false),
                    UsedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                table: "Departments",
                columns: new[] { "Id", "CreateAt", "Description", "Name", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(7563), "DepartentA", "DepartentA", new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(7663) },
                    { new Guid("d8854d21-8fae-46aa-b51b-0de060b92ee3"), new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(7739), "Company", "Company", new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(7740) }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "CreateAt", "Description", "Name", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(9885), "Quyền xem mọi tài liệu trong hệ thống ", "VIEW_ANY_DOCUMENT", new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(9976) },
                    { new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(61), "Quyền xem tài liệu thuộc phòng ban của mình.", "VIEW_OWN_DEPARTMENT_DOCUMENT", new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(61) },
                    { new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(63), "Quyền xem tài liệu của mình.", "VIEW_DEPARTMENT_DOCUMENT", new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(63) }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreateAt", "Description", "RoleName", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(2876), "Member", "Member", new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(2969) },
                    { new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"), new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(3048), "Editor", "Editor", new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(3048) },
                    { new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(3046), "Manager", "Manager", new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(3047) },
                    { new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(3044), "Admin", "Admin", new DateTime(2025, 6, 28, 10, 2, 53, 329, DateTimeKind.Utc).AddTicks(3044) }
                });

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

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatAt", "DepartmentId", "Email", "FullName", "Password", "Phone", "RoleId", "TwoFactorEnabled", "TwoFactorMethod", "UpdateAt", "UserName" },
                values: new object[,]
                {
                    { new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616"), new DateTime(2025, 6, 28, 10, 2, 53, 355, DateTimeKind.Utc).AddTicks(6989), new Guid("d8854d21-8fae-46aa-b51b-0de060b92ee3"), "admin@gmail.com", "Admin", "lAb/k5Augxm4v5KJoEWgkItbHiGSZoA/iEqIYLYQH2EGZTht2bebXj5vEzYeS5Ah", "0847911068", new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), false, "Email", new DateTime(2025, 6, 28, 10, 2, 53, 355, DateTimeKind.Utc).AddTicks(7097), "admin" },
                    { new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"), new DateTime(2025, 6, 28, 10, 2, 53, 375, DateTimeKind.Utc).AddTicks(9262), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), "manager@gmail.com", "Manager", "bFB0dDY+qUIrzTSi+joSfiOeUbGBmoBNtlI7m2KEWBlfk7KzOH0EPbZMumZIvJnc", "0123456789", new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), false, "Email", new DateTime(2025, 6, 28, 10, 2, 53, 375, DateTimeKind.Utc).AddTicks(9271), "manager" },
                    { new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"), new DateTime(2025, 6, 28, 10, 2, 53, 416, DateTimeKind.Utc).AddTicks(3836), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), "editor@gmail.com", "Editor", "pAZwp+b7CkfzJqNAcT1axeS9Btvlaoc1G/aNDvCg9JgT4w3tsJK65jBArqE9aceN", "0123456789", new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"), false, "Email", new DateTime(2025, 6, 28, 10, 2, 53, 416, DateTimeKind.Utc).AddTicks(3843), "editor" },
                    { new Guid("fd05266c-baf5-49bb-a846-554461bcc411"), new DateTime(2025, 6, 28, 10, 2, 53, 395, DateTimeKind.Utc).AddTicks(9915), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), "member@gmail.com", "Member", "Ddb4IpvIOUdDtvT7aw2XMkoNa17IeKLib28+pEv4CGDn8srSwbxfMb69lOw4cPbC", "0123456789", new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), false, "Email", new DateTime(2025, 6, 28, 10, 2, 53, 395, DateTimeKind.Utc).AddTicks(9920), "member" }
                });

            migrationBuilder.InsertData(
                table: "ActiveKeys",
                columns: new[] { "Id", "ActivationCode", "CreatedAt", "CreatedByUserId", "DepartmentId", "RoleId", "Status", "UpdatedAt", "UsedByUserId" },
                values: new object[] { new Guid("50b64957-bae3-4377-aa7a-fee36d25ccd6"), "P4rBZtdXa5YvEGJNmKLcQq7RfW9HU61o", new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(1421), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), "On", new DateTime(2025, 6, 28, 10, 2, 53, 328, DateTimeKind.Utc).AddTicks(1527), null });

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

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DepartmentId",
                table: "Users",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveKeys");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
