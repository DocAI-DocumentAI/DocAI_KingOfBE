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
                    GoogleId = table.Column<string>(type: "text", nullable: true),
                    RequirePasswordChange = table.Column<bool>(type: "boolean", nullable: false),
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
                values: new object[] { new Guid("d8854d21-8fae-46aa-b51b-0de060b92ee3"), new DateTime(2025, 8, 19, 11, 48, 47, 394, DateTimeKind.Utc).AddTicks(6495), "Company", "Company", new DateTime(2025, 8, 19, 11, 48, 47, 394, DateTimeKind.Utc).AddTicks(6615) });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "CreateAt", "Description", "Name", "UpdateAt" },
                values: new object[] { new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new DateTime(2025, 8, 19, 11, 48, 47, 395, DateTimeKind.Utc).AddTicks(2502), "Quyền xem mọi tài liệu trong hệ thống ", "VIEW_ANY_DOCUMENT", new DateTime(2025, 8, 19, 11, 48, 47, 395, DateTimeKind.Utc).AddTicks(2608) });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreateAt", "Description", "RoleName", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new DateTime(2025, 8, 19, 11, 48, 47, 395, DateTimeKind.Utc).AddTicks(5268), "Employee", "Employee", new DateTime(2025, 8, 19, 11, 48, 47, 395, DateTimeKind.Utc).AddTicks(5369) },
                    { new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"), new DateTime(2025, 8, 19, 11, 48, 47, 395, DateTimeKind.Utc).AddTicks(5460), "Editor", "Editor", new DateTime(2025, 8, 19, 11, 48, 47, 395, DateTimeKind.Utc).AddTicks(5460) },
                    { new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new DateTime(2025, 8, 19, 11, 48, 47, 395, DateTimeKind.Utc).AddTicks(5458), "Manager", "Manager", new DateTime(2025, 8, 19, 11, 48, 47, 395, DateTimeKind.Utc).AddTicks(5458) },
                    { new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), new DateTime(2025, 8, 19, 11, 48, 47, 395, DateTimeKind.Utc).AddTicks(5455), "Admin", "Admin", new DateTime(2025, 8, 19, 11, 48, 47, 395, DateTimeKind.Utc).AddTicks(5456) }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Active", "CreatAt", "DepartmentId", "Email", "FullName", "GoogleId", "Password", "Phone", "RequirePasswordChange", "RoleId", "UpdateAt" },
                values: new object[] { new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616"), true, new DateTime(2025, 8, 19, 11, 48, 47, 421, DateTimeKind.Utc).AddTicks(213), new Guid("d8854d21-8fae-46aa-b51b-0de060b92ee3"), "nguyenhuyphc@gmail.com", "Phc Admin", null, "oG9lRXLNaM5iGqbFI5HI8nc/yUHuqNsz9L0pWruCY4odChrfISxGfgRFbrFureV9", "0847911068", true, new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), new DateTime(2025, 8, 19, 11, 48, 47, 421, DateTimeKind.Utc).AddTicks(362) });

            migrationBuilder.InsertData(
                table: "UserPermissions",
                columns: new[] { "Id", "PermissionId", "UserId" },
                values: new object[] { new Guid("bf658719-57e7-4c2a-9a59-34e4d47a5024"), new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616") });

            migrationBuilder.InsertData(
                table: "UserSettings",
                columns: new[] { "Id", "NotificationsEnabled", "TwoFactorEnabled", "TwoFactorMethod", "UpdateAt", "UserId" },
                values: new object[] { new Guid("ddfcbea3-56e9-4187-97f6-521ca24c2412"), true, false, "email", new DateTime(2025, 8, 19, 11, 48, 47, 422, DateTimeKind.Utc).AddTicks(78), new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616") });

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
