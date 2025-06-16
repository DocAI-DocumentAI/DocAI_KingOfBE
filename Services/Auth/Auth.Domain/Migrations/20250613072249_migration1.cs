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
                name: "UserDepartments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    userId = table.Column<Guid>(type: "uuid", nullable: false),
                    departmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDepartmentHead = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDepartments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDepartments_Departments_departmentId",
                        column: x => x.departmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserDepartments_Users_userId",
                        column: x => x.userId,
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
                table: "Users",
                columns: new[] { "Id", "CreatAt", "Email", "FullName", "Password", "Phone", "TwoFactorEnabled", "TwoFactorMethod", "UpdateAt", "UserName" },
                values: new object[,]
                {
                    { new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616"), new DateTime(2025, 6, 13, 7, 22, 48, 946, DateTimeKind.Utc).AddTicks(448), "admin@gmail.com", "Admin", "t46fvHlNjS1VsKWT9awKE3NDNZg1pkqRGo6a7u+8acXXWcpIQEJBDVUT5oH1bWaY", "0847911068", false, "Email", new DateTime(2025, 6, 13, 7, 22, 48, 946, DateTimeKind.Utc).AddTicks(593), "admin" },
                    { new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"), new DateTime(2025, 6, 13, 7, 22, 48, 992, DateTimeKind.Utc).AddTicks(5564), "manager@gmail.com", "Manager", "qyAWT5yKspDqvzMb+zNrloaRiZILt1CgI80TkNBrhvFbq0QyTQB5y3ECN8oaeInl", "0123456789", false, "Email", new DateTime(2025, 6, 13, 7, 22, 48, 992, DateTimeKind.Utc).AddTicks(5570), "manager" },
                    { new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"), new DateTime(2025, 6, 13, 7, 22, 49, 33, DateTimeKind.Utc).AddTicks(3191), "editor@gmail.com", "Member1", "jjjKkjQHNHl5WVwGP3Sh0CPbONKaY6iXYcRb6Tt5sokROKBjnxxNeLD6mfBGA+/1", "0123456789", false, "Email", new DateTime(2025, 6, 13, 7, 22, 49, 33, DateTimeKind.Utc).AddTicks(3196), "editor" },
                    { new Guid("a9df371e-9249-47b8-83ce-8cd940140b9b"), new DateTime(2025, 6, 13, 7, 22, 48, 967, DateTimeKind.Utc).AddTicks(7004), "director@gmail.com", "Director", "C1wMjDFj2SH/X+FkqPAvtzhcHmHgUarGcHciJ6H5EvxYlkiLuaOWlV9aAQhpZtkE", "0123456789", false, "Email", new DateTime(2025, 6, 13, 7, 22, 48, 967, DateTimeKind.Utc).AddTicks(7011), "director" },
                    { new Guid("fd05266c-baf5-49bb-a846-554461bcc411"), new DateTime(2025, 6, 13, 7, 22, 49, 13, DateTimeKind.Utc).AddTicks(2967), "member@gmail.com", "Member", "wDGEikVIAgVki0rcudm15Gme0zrpLqTy4Cdw7DDqohv9FOGsUTBVBVivhfRINX8g", "0123456789", false, "Email", new DateTime(2025, 6, 13, 7, 22, 49, 13, DateTimeKind.Utc).AddTicks(2973), "member" }
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
                name: "IX_UserDepartments_departmentId",
                table: "UserDepartments",
                column: "departmentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartments_userId",
                table: "UserDepartments",
                column: "userId");

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
