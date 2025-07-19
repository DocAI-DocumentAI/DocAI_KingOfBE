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
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "UserPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
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
                    { new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), new DateTime(2025, 7, 19, 7, 56, 35, 503, DateTimeKind.Utc).AddTicks(5751), "DepartentA", "DepartentA", new DateTime(2025, 7, 19, 7, 56, 35, 503, DateTimeKind.Utc).AddTicks(5861) },
                    { new Guid("d8854d21-8fae-46aa-b51b-0de060b92ee3"), new DateTime(2025, 7, 19, 7, 56, 35, 503, DateTimeKind.Utc).AddTicks(6064), "Company", "Company", new DateTime(2025, 7, 19, 7, 56, 35, 503, DateTimeKind.Utc).AddTicks(6065) }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "CreateAt", "Description", "Name", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(3401), "Quyền xem mọi tài liệu trong hệ thống ", "VIEW_ANY_DOCUMENT", new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(3502) },
                    { new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(3672), "Quyền xem tài liệu thuộc phòng ban của mình.", "VIEW_OWN_DEPARTMENT_DOCUMENT", new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(3674) },
                    { new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(3676), "Quyền xem tài liệu của mình.", "VIEW_DEPARTMENT_DOCUMENT", new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(3676) }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreateAt", "Description", "RoleName", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(5979), "Employee", "Employee", new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(6068) },
                    { new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"), new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(6150), "Editor", "Editor", new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(6150) },
                    { new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(6148), "Manager", "Manager", new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(6148) },
                    { new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(6146), "Admin", "Admin", new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(6146) }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Active", "CreatAt", "DepartmentId", "Email", "FullName", "Password", "Phone", "RoleId", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616"), false, new DateTime(2025, 7, 19, 7, 56, 35, 529, DateTimeKind.Utc).AddTicks(9353), new Guid("d8854d21-8fae-46aa-b51b-0de060b92ee3"), "admin@gmail.com", "Admin", "TuCboI2USCS1QIyw6VyWc8tCL2THhdytgapBAeJ3JZLExF6ZwuT4C9Ed0U1GMkIN", "0847911068", new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), new DateTime(2025, 7, 19, 7, 56, 35, 529, DateTimeKind.Utc).AddTicks(9468) },
                    { new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"), false, new DateTime(2025, 7, 19, 7, 56, 35, 549, DateTimeKind.Utc).AddTicks(7435), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), "manager@gmail.com", "Manager", "3M8X+PhlXm7jFY2AnRf4vN+DlkNfkHDdob+favJqvElQ3Wu8yc5E8QK9XgROx1zD", "0123456789", new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new DateTime(2025, 7, 19, 7, 56, 35, 549, DateTimeKind.Utc).AddTicks(7440) },
                    { new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"), false, new DateTime(2025, 7, 19, 7, 56, 35, 590, DateTimeKind.Utc).AddTicks(5804), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), "editor@gmail.com", "Editor", "8LYWgc6vmJw/N1dWFw6Kme0xHlScHmMEXXKC2ZjS6BhNzUnKV17vqjJhLizxSs52", "0123456789", new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"), new DateTime(2025, 7, 19, 7, 56, 35, 590, DateTimeKind.Utc).AddTicks(5810) },
                    { new Guid("fd05266c-baf5-49bb-a846-554461bcc411"), false, new DateTime(2025, 7, 19, 7, 56, 35, 570, DateTimeKind.Utc).AddTicks(1797), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), "employee@gmail.com", "Employee", "S4PnHpqjUYW10IHpMqCr6suaVDHTNW09huYZhD4nZn9AOIGbRFQcJ0EKAnD4GImD", "0123456789", new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new DateTime(2025, 7, 19, 7, 56, 35, 570, DateTimeKind.Utc).AddTicks(1801) }
                });

            migrationBuilder.InsertData(
                table: "UserPermissions",
                columns: new[] { "Id", "PermissionId", "UserId" },
                values: new object[,]
                {
                    { new Guid("0c56263b-b1f0-43d2-a701-a6d66e9c2b4f"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("fd05266c-baf5-49bb-a846-554461bcc411") },
                    { new Guid("42f53500-74bb-4883-b397-6a511ccab4c6"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd") },
                    { new Guid("68f0ed30-8378-4c7a-9ac1-7e59efd23f7d"), new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616") },
                    { new Guid("b00f2a64-ed45-40fa-9941-cee61140529d"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") },
                    { new Guid("b1ebed91-c7b3-41cd-9364-dbff90079fbc"), new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") }
                });

            migrationBuilder.InsertData(
                table: "UserSettings",
                columns: new[] { "Id", "NotificationsEnabled", "TwoFactorEnabled", "TwoFactorMethod", "UpdateAt", "UserId" },
                values: new object[,]
                {
                    { new Guid("4e8bff21-b470-4b9e-92da-400d21992f96"), true, false, "email", new DateTime(2025, 7, 19, 7, 56, 35, 591, DateTimeKind.Utc).AddTicks(3474), new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd") },
                    { new Guid("86254802-1d1e-4734-a25b-ef22ff39cefc"), true, false, "email", new DateTime(2025, 7, 19, 7, 56, 35, 591, DateTimeKind.Utc).AddTicks(3471), new Guid("fd05266c-baf5-49bb-a846-554461bcc411") },
                    { new Guid("dd9105eb-4df0-4c32-bc55-fd0169e386fc"), true, false, "email", new DateTime(2025, 7, 19, 7, 56, 35, 591, DateTimeKind.Utc).AddTicks(3469), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") },
                    { new Guid("ddfcbea3-56e9-4187-97f6-521ca24c2412"), true, false, "email", new DateTime(2025, 7, 19, 7, 56, 35, 591, DateTimeKind.Utc).AddTicks(3274), new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_PermissionId",
                table: "UserPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserId",
                table: "UserPermissions",
                column: "UserId");

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
                name: "UserPermissions");

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
