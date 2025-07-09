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
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorMethod = table.Column<string>(type: "text", nullable: true),
                    NotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "CreateAt", "Description", "Name", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), new DateTime(2025, 7, 9, 6, 48, 28, 468, DateTimeKind.Utc).AddTicks(4958), "DepartentA", "DepartentA", new DateTime(2025, 7, 9, 6, 48, 28, 468, DateTimeKind.Utc).AddTicks(5081) },
                    { new Guid("d8854d21-8fae-46aa-b51b-0de060b92ee3"), new DateTime(2025, 7, 9, 6, 48, 28, 468, DateTimeKind.Utc).AddTicks(5208), "Company", "Company", new DateTime(2025, 7, 9, 6, 48, 28, 468, DateTimeKind.Utc).AddTicks(5208) }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "CreateAt", "Description", "Name", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new DateTime(2025, 7, 9, 6, 48, 28, 469, DateTimeKind.Utc).AddTicks(2061), "Quyền xem mọi tài liệu trong hệ thống ", "VIEW_ANY_DOCUMENT", new DateTime(2025, 7, 9, 6, 48, 28, 469, DateTimeKind.Utc).AddTicks(2173) },
                    { new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new DateTime(2025, 7, 9, 6, 48, 28, 469, DateTimeKind.Utc).AddTicks(2343), "Quyền xem tài liệu thuộc phòng ban của mình.", "VIEW_OWN_DEPARTMENT_DOCUMENT", new DateTime(2025, 7, 9, 6, 48, 28, 469, DateTimeKind.Utc).AddTicks(2344) },
                    { new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new DateTime(2025, 7, 9, 6, 48, 28, 469, DateTimeKind.Utc).AddTicks(2346), "Quyền xem tài liệu của mình.", "VIEW_DEPARTMENT_DOCUMENT", new DateTime(2025, 7, 9, 6, 48, 28, 469, DateTimeKind.Utc).AddTicks(2346) }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreateAt", "Description", "RoleName", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new DateTime(2025, 7, 9, 6, 48, 28, 469, DateTimeKind.Utc).AddTicks(4793), "Employee", "Employee", new DateTime(2025, 7, 9, 6, 48, 28, 469, DateTimeKind.Utc).AddTicks(4892) },
                    { new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"), new DateTime(2025, 7, 9, 6, 48, 28, 469, DateTimeKind.Utc).AddTicks(5023), "Editor", "Editor", new DateTime(2025, 7, 9, 6, 48, 28, 469, DateTimeKind.Utc).AddTicks(5023) },
                    { new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new DateTime(2025, 7, 9, 6, 48, 28, 469, DateTimeKind.Utc).AddTicks(5021), "Manager", "Manager", new DateTime(2025, 7, 9, 6, 48, 28, 469, DateTimeKind.Utc).AddTicks(5021) },
                    { new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), new DateTime(2025, 7, 9, 6, 48, 28, 469, DateTimeKind.Utc).AddTicks(5018), "Admin", "Admin", new DateTime(2025, 7, 9, 6, 48, 28, 469, DateTimeKind.Utc).AddTicks(5019) }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("0843307d-cf05-414b-84ab-394f884f4b45"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb") },
                    { new Guid("1014f444-b3de-4db8-9410-9c362a38f2db"), new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6") },
                    { new Guid("17566104-3004-4663-a242-a1770d65d5c6"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f") },
                    { new Guid("33bb5518-3383-46fb-91a5-01f95d3793c6"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("8e7d55e4-67d3-4b73-9995-21b163493136") },
                    { new Guid("d4a7f8e1-5571-46e5-b3b2-2ee79d408f2a"), new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb") }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Active", "CreatAt", "DepartmentId", "Email", "FullName", "Password", "Phone", "RoleId", "UpdateAt", "UserName" },
                values: new object[,]
                {
                    { new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616"), false, new DateTime(2025, 7, 9, 6, 48, 28, 495, DateTimeKind.Utc).AddTicks(9028), new Guid("d8854d21-8fae-46aa-b51b-0de060b92ee3"), "admin@gmail.com", "Admin", "zbr+GIjiZexQYo70lTjQuXndqIJ+tu3g1V4z2TE91VgXJ/u0fFqN1VGmxZgd5aUU", "0847911068", new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), new DateTime(2025, 7, 9, 6, 48, 28, 495, DateTimeKind.Utc).AddTicks(9144), "admin" },
                    { new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"), false, new DateTime(2025, 7, 9, 6, 48, 28, 516, DateTimeKind.Utc).AddTicks(4714), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), "manager@gmail.com", "Manager", "sh4ehkewlS3cFVYuqkKrFMLj+fN000U9OFusoTdINQFY8XBUrUC5Uv5NEv2EQphV", "0123456789", new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new DateTime(2025, 7, 9, 6, 48, 28, 516, DateTimeKind.Utc).AddTicks(4719), "manager" },
                    { new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"), false, new DateTime(2025, 7, 9, 6, 48, 28, 556, DateTimeKind.Utc).AddTicks(8304), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), "editor@gmail.com", "Editor", "ZWsFN7GneikIvlo6nENDWv0HN2klxOgnXjQ6bVsMwnSOoEIC8n4GPXY2RqyPlaPK", "0123456789", new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"), new DateTime(2025, 7, 9, 6, 48, 28, 556, DateTimeKind.Utc).AddTicks(8310), "editor" },
                    { new Guid("fd05266c-baf5-49bb-a846-554461bcc411"), false, new DateTime(2025, 7, 9, 6, 48, 28, 536, DateTimeKind.Utc).AddTicks(6481), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), "employee@gmail.com", "Employee", "XwnHf6qw79crCIIBreZro5bRr0EHlndsBYrpBowqLoBfQdUHxwZRLkwulIbobrXi", "0123456789", new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new DateTime(2025, 7, 9, 6, 48, 28, 536, DateTimeKind.Utc).AddTicks(6486), "employee" }
                });

            migrationBuilder.InsertData(
                table: "UserSettings",
                columns: new[] { "Id", "NotificationsEnabled", "TwoFactorEnabled", "TwoFactorMethod", "UpdateAt", "UserId" },
                values: new object[,]
                {
                    { new Guid("4e8bff21-b470-4b9e-92da-400d21992f96"), true, false, "email", new DateTime(2025, 7, 9, 6, 48, 28, 557, DateTimeKind.Utc).AddTicks(3796), new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd") },
                    { new Guid("86254802-1d1e-4734-a25b-ef22ff39cefc"), true, false, "email", new DateTime(2025, 7, 9, 6, 48, 28, 557, DateTimeKind.Utc).AddTicks(3794), new Guid("fd05266c-baf5-49bb-a846-554461bcc411") },
                    { new Guid("dd9105eb-4df0-4c32-bc55-fd0169e386fc"), true, false, "email", new DateTime(2025, 7, 9, 6, 48, 28, 557, DateTimeKind.Utc).AddTicks(3791), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") },
                    { new Guid("ddfcbea3-56e9-4187-97f6-521ca24c2412"), true, false, "email", new DateTime(2025, 7, 9, 6, 48, 28, 557, DateTimeKind.Utc).AddTicks(3524), new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616") }
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_UserId",
                table: "UserSettings",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
