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
                name: "ActiveKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivationCode = table.Column<string>(type: "text", nullable: false),
                    RoleName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveKeys", x => x.Id);
                });

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
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "text", nullable: false),
                    Password = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorMethod = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
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
                name: "DepartmentRolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDepartmentHead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentRolePermissions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepartmentRolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepartmentRolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepartmentRolePermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDepartments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDepartmentHead = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDepartments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDepartments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserDepartments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ActiveKeys",
                columns: new[] { "Id", "ActivationCode", "RoleName" },
                values: new object[,]
                {
                    { new Guid("50b64957-bae3-4377-aa7a-fee36d25ccd6"), "P4rBZtdXa5YvEGJNmKLcQq7RfW9HU61o", "Editor" },
                    { new Guid("65de7f7d-0bcc-4cdf-bd8c-f8d1ac290cd8"), "zXYmN7pLcVTEqF59jKADrCbhQuU630aw", "Manager" }
                });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "CreateAt", "Description", "Name", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("86deff8b-cb4b-4daf-88d4-6f366b051836"), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(3215), "DepartmentB", "DepartmentB", new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(3215) },
                    { new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(3015), "DepartentA", "DepartentA", new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(3134) }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "CreateAt", "Description", "Name", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(9111), "Quyền xem mọi tài liệu trong hệ thống ", "VIEW_ANY_DOCUMENT", new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(9210) },
                    { new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(9293), "Quyền xem tài liệu thuộc phòng ban của mình.", "VIEW_OWN_DEPARTMENT_DOCUMENT", new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(9294) },
                    { new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(9295), "Quyền xem tài liệu của mình.", "VIEW_DEPARTMENT_DOCUMENT", new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(9296) }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreateAt", "Description", "RoleName", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1517), "Member", "Member", new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1608) },
                    { new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"), new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1702), "Editor", "Editor", new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1702) },
                    { new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1700), "Manager", "Manager", new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1701) },
                    { new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1698), "Admin", "Admin", new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1699) }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatAt", "Email", "FullName", "Password", "Phone", "TwoFactorEnabled", "TwoFactorMethod", "UpdateAt", "UserName" },
                values: new object[,]
                {
                    { new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616"), new DateTime(2025, 6, 18, 10, 31, 3, 939, DateTimeKind.Utc).AddTicks(6915), "admin@gmail.com", "Admin", "q2JeW34Zf0qgo5Ra6+iBTp0R5u1BvoRrRWEWQRmKK9j4TV+hPSc3E3RGV5zm7Elc", "0847911068", false, "Email", new DateTime(2025, 6, 18, 10, 31, 3, 939, DateTimeKind.Utc).AddTicks(7055), "admin" },
                    { new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"), new DateTime(2025, 6, 18, 10, 31, 3, 957, DateTimeKind.Utc).AddTicks(9247), "manager@gmail.com", "Manager", "QXB0X/Yqm3s2WhU2A0tKMh7ECPNhKJXuqzwe2vPRyLFkj2vzPQRnVvkUYPq1VERq", "0123456789", false, "Email", new DateTime(2025, 6, 18, 10, 31, 3, 957, DateTimeKind.Utc).AddTicks(9252), "manager" },
                    { new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"), new DateTime(2025, 6, 18, 10, 31, 3, 994, DateTimeKind.Utc).AddTicks(4293), "editor@gmail.com", "Member1", "I1SWkV6xjXmynG61xnc37kNCzYlJ/A4hpgmxMK70eDjmB9GGXHUfY/f0dinwAu9h", "0123456789", false, "Email", new DateTime(2025, 6, 18, 10, 31, 3, 994, DateTimeKind.Utc).AddTicks(4298), "editor" },
                    { new Guid("fd05266c-baf5-49bb-a846-554461bcc411"), new DateTime(2025, 6, 18, 10, 31, 3, 975, DateTimeKind.Utc).AddTicks(9534), "member@gmail.com", "Member", "YddDKQDbGZYjGznmnLXnZTHkX7lxFX4pUjyVJmaobVsQxP5BANwidpyf4SeYB3iQ", "0123456789", false, "Email", new DateTime(2025, 6, 18, 10, 31, 3, 975, DateTimeKind.Utc).AddTicks(9540), "member" }
                });

            migrationBuilder.InsertData(
                table: "DepartmentRolePermissions",
                columns: new[] { "Id", "CreatAt", "DepartmentId", "IsDepartmentHead", "PermissionId", "RoleId", "UpdateAt", "UserId" },
                values: new object[,]
                {
                    { new Guid("3709cb77-7539-4423-9f97-a1dc49e155ab"), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(6861), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), false, new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(6862), new Guid("fd05266c-baf5-49bb-a846-554461bcc411") },
                    { new Guid("65a1468f-753e-46be-ad7c-31ae60ed5a65"), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(6858), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), false, new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(6858), new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd") },
                    { new Guid("79f5ecd3-50a3-46af-8ce8-f258f00efd4a"), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(6689), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), true, new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(6776), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") },
                    { new Guid("ab0d0397-a755-4e55-8368-0d3ddc026727"), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(6865), new Guid("86deff8b-cb4b-4daf-88d4-6f366b051836"), false, new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(6865), new Guid("fd05266c-baf5-49bb-a846-554461bcc411") },
                    { new Guid("e055e80e-294b-4707-afa9-5bc2a412218c"), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(6854), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), true, new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(6854), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("0660d80c-3d49-4249-96ef-875d588b1c65"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb") },
                    { new Guid("66e1e900-ba76-4657-8cf0-613cb064793e"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("8e7d55e4-67d3-4b73-9995-21b163493136") },
                    { new Guid("7bede109-ee65-4aa5-ac38-5bb9b571ebf2"), new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6") },
                    { new Guid("d6afded8-c5bc-4062-92c9-b72edac28d90"), new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb") },
                    { new Guid("ea5b9fe4-f490-4792-a425-dfb853f7dce1"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f") }
                });

            migrationBuilder.InsertData(
                table: "UserDepartments",
                columns: new[] { "Id", "DepartmentId", "IsDepartmentHead", "UserId" },
                values: new object[,]
                {
                    { new Guid("2e1487eb-81a8-4925-8bac-7facd7277b71"), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), true, new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") },
                    { new Guid("68385e8b-e03a-414a-941f-0e889f6cea64"), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), false, new Guid("fd05266c-baf5-49bb-a846-554461bcc411") },
                    { new Guid("dea22e99-739b-43cc-b7ed-865a481a6412"), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), false, new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd") },
                    { new Guid("fab49049-4972-4070-9d69-1aef5f0081c8"), new Guid("86deff8b-cb4b-4daf-88d4-6f366b051836"), false, new Guid("fd05266c-baf5-49bb-a846-554461bcc411") }
                });

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "Id", "RoleId", "UserId" },
                values: new object[,]
                {
                    { new Guid("3e0809c4-5432-41b3-9161-cabf967a128e"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") },
                    { new Guid("60a9dc18-e62e-4e7c-aaf6-f10713851342"), new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616") },
                    { new Guid("b12b0456-a76a-436b-a7a2-8b3c179e7a50"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new Guid("fd05266c-baf5-49bb-a846-554461bcc411") },
                    { new Guid("c978f375-1a64-413a-88c5-a89456490df9"), new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"), new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentRolePermissions_DepartmentId",
                table: "DepartmentRolePermissions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentRolePermissions_PermissionId",
                table: "DepartmentRolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentRolePermissions_RoleId",
                table: "DepartmentRolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentRolePermissions_UserId",
                table: "DepartmentRolePermissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartments_DepartmentId",
                table: "UserDepartments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartments_UserId",
                table: "UserDepartments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveKeys");

            migrationBuilder.DropTable(
                name: "DepartmentRolePermissions");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserDepartments");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
