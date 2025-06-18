using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Auth.Domain.Migrations
{
    /// <inheritdoc />
    public partial class migration2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "DepartmentRolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("3709cb77-7539-4423-9f97-a1dc49e155ab"));

            migrationBuilder.DeleteData(
                table: "DepartmentRolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("65a1468f-753e-46be-ad7c-31ae60ed5a65"));

            migrationBuilder.DeleteData(
                table: "DepartmentRolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("79f5ecd3-50a3-46af-8ce8-f258f00efd4a"));

            migrationBuilder.DeleteData(
                table: "DepartmentRolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("ab0d0397-a755-4e55-8368-0d3ddc026727"));

            migrationBuilder.DeleteData(
                table: "DepartmentRolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("e055e80e-294b-4707-afa9-5bc2a412218c"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("0660d80c-3d49-4249-96ef-875d588b1c65"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("66e1e900-ba76-4657-8cf0-613cb064793e"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("7bede109-ee65-4aa5-ac38-5bb9b571ebf2"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("d6afded8-c5bc-4062-92c9-b72edac28d90"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("ea5b9fe4-f490-4792-a425-dfb853f7dce1"));

            migrationBuilder.DeleteData(
                table: "UserDepartments",
                keyColumn: "Id",
                keyValue: new Guid("2e1487eb-81a8-4925-8bac-7facd7277b71"));

            migrationBuilder.DeleteData(
                table: "UserDepartments",
                keyColumn: "Id",
                keyValue: new Guid("68385e8b-e03a-414a-941f-0e889f6cea64"));

            migrationBuilder.DeleteData(
                table: "UserDepartments",
                keyColumn: "Id",
                keyValue: new Guid("dea22e99-739b-43cc-b7ed-865a481a6412"));

            migrationBuilder.DeleteData(
                table: "UserDepartments",
                keyColumn: "Id",
                keyValue: new Guid("fab49049-4972-4070-9d69-1aef5f0081c8"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("3e0809c4-5432-41b3-9161-cabf967a128e"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("60a9dc18-e62e-4e7c-aaf6-f10713851342"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("b12b0456-a76a-436b-a7a2-8b3c179e7a50"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("c978f375-1a64-413a-88c5-a89456490df9"));

            migrationBuilder.DropColumn(
                name: "IsDepartmentHead",
                table: "DepartmentRolePermissions");

            migrationBuilder.InsertData(
                table: "DepartmentRolePermissions",
                columns: new[] { "Id", "CreatAt", "DepartmentId", "PermissionId", "RoleId", "UpdateAt", "UserId" },
                values: new object[,]
                {
                    { new Guid("5976f05f-2a3e-4f94-84f7-7178c8a1d572"), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(3391), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(3392), new Guid("fd05266c-baf5-49bb-a846-554461bcc411") },
                    { new Guid("a716fb6a-dc2a-417e-987f-006f7c254e96"), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(3377), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(3377), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") },
                    { new Guid("bf3efb31-c414-4913-84fd-12c9443a8f53"), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(3395), new Guid("86deff8b-cb4b-4daf-88d4-6f366b051836"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(3395), new Guid("fd05266c-baf5-49bb-a846-554461bcc411") },
                    { new Guid("e55561aa-7c0a-4dc8-844c-9a24acb678c4"), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(3203), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(3294), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") },
                    { new Guid("f6528a1f-2104-41f3-b70d-b71bf9930639"), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(3381), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(3381), new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd") }
                });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: new Guid("86deff8b-cb4b-4daf-88d4-6f366b051836"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 11, 13, 26, 220, DateTimeKind.Utc).AddTicks(9785), new DateTime(2025, 6, 18, 11, 13, 26, 220, DateTimeKind.Utc).AddTicks(9786) });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: new Guid("8bf13891-1ce9-405c-add9-0ada93308671"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 11, 13, 26, 220, DateTimeKind.Utc).AddTicks(9563), new DateTime(2025, 6, 18, 11, 13, 26, 220, DateTimeKind.Utc).AddTicks(9701) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(5624), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(5733) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(5816), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(5817) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(5819), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(5819) });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("013a9f0a-328d-4a51-afae-5b88fddac343"), new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6") },
                    { new Guid("05f54cb0-5fb9-4932-8e95-d2b32bd0de42"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("8e7d55e4-67d3-4b73-9995-21b163493136") },
                    { new Guid("7c8e3837-f864-47cb-9adf-03905e001002"), new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb") },
                    { new Guid("99d16ed9-63b3-485b-abc6-e88e1d7e6449"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb") },
                    { new Guid("fd0cd98e-119d-48ba-9867-7650cefd39c8"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f") }
                });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(7962), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(8050) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(8127), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(8127) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(8125), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(8126) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(8123), new DateTime(2025, 6, 18, 11, 13, 26, 221, DateTimeKind.Utc).AddTicks(8124) });

            migrationBuilder.InsertData(
                table: "UserDepartments",
                columns: new[] { "Id", "DepartmentId", "IsDepartmentHead", "UserId" },
                values: new object[,]
                {
                    { new Guid("5a60e61c-2ad5-4c60-abd2-c189eb38779a"), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), true, new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") },
                    { new Guid("9951e8a8-d4f2-48dc-a9fa-37a466226ce8"), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), false, new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd") },
                    { new Guid("9b65b3e7-7985-4aae-86cf-bc8b625e6d38"), new Guid("8bf13891-1ce9-405c-add9-0ada93308671"), false, new Guid("fd05266c-baf5-49bb-a846-554461bcc411") },
                    { new Guid("d7b046e2-5fe3-4ce4-aa0b-1182feb04f54"), new Guid("86deff8b-cb4b-4daf-88d4-6f366b051836"), false, new Guid("fd05266c-baf5-49bb-a846-554461bcc411") }
                });

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "Id", "RoleId", "UserId" },
                values: new object[,]
                {
                    { new Guid("15731756-347d-4812-bff7-18fb43895bd6"), new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616") },
                    { new Guid("46170431-f2ed-4423-a73c-270cf206fa3a"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") },
                    { new Guid("691565e3-1c09-4789-9f96-631c654e2f23"), new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"), new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd") },
                    { new Guid("79c13a86-cd80-40d4-aee5-f40172d2fc1d"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new Guid("fd05266c-baf5-49bb-a846-554461bcc411") }
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 11, 13, 26, 244, DateTimeKind.Utc).AddTicks(8189), "gMTHtekzIhbeHPv6xtJvqE9EFueAQ5ru99SvzyDmeXSzt/dOKvgiy8KdtmHMdKir", new DateTime(2025, 6, 18, 11, 13, 26, 244, DateTimeKind.Utc).AddTicks(8417) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 11, 13, 26, 263, DateTimeKind.Utc).AddTicks(4051), "g4MLMebanPnPBgWf9bAs9VnApl2dGgD7bmlJSzQ1DvBzrrpkTe+5kiVq5qoVmLCm", new DateTime(2025, 6, 18, 11, 13, 26, 263, DateTimeKind.Utc).AddTicks(4057) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 11, 13, 26, 300, DateTimeKind.Utc).AddTicks(1775), "lIyv321NZsWbEAKGZ3AL4xKPtD+riDn0dY0dr71xOEmPp4s6ax03I/BqNXLEq9jZ", new DateTime(2025, 6, 18, 11, 13, 26, 300, DateTimeKind.Utc).AddTicks(1780) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("fd05266c-baf5-49bb-a846-554461bcc411"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 11, 13, 26, 281, DateTimeKind.Utc).AddTicks(9414), "0YywcDKKggnOfD5eiHC/qMZ6OY5JIR8Aabcg3WiczvxHDfLYpMo8S6+enM37D2Hn", new DateTime(2025, 6, 18, 11, 13, 26, 281, DateTimeKind.Utc).AddTicks(9421) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "DepartmentRolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("5976f05f-2a3e-4f94-84f7-7178c8a1d572"));

            migrationBuilder.DeleteData(
                table: "DepartmentRolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("a716fb6a-dc2a-417e-987f-006f7c254e96"));

            migrationBuilder.DeleteData(
                table: "DepartmentRolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("bf3efb31-c414-4913-84fd-12c9443a8f53"));

            migrationBuilder.DeleteData(
                table: "DepartmentRolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("e55561aa-7c0a-4dc8-844c-9a24acb678c4"));

            migrationBuilder.DeleteData(
                table: "DepartmentRolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("f6528a1f-2104-41f3-b70d-b71bf9930639"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("013a9f0a-328d-4a51-afae-5b88fddac343"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("05f54cb0-5fb9-4932-8e95-d2b32bd0de42"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("7c8e3837-f864-47cb-9adf-03905e001002"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("99d16ed9-63b3-485b-abc6-e88e1d7e6449"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("fd0cd98e-119d-48ba-9867-7650cefd39c8"));

            migrationBuilder.DeleteData(
                table: "UserDepartments",
                keyColumn: "Id",
                keyValue: new Guid("5a60e61c-2ad5-4c60-abd2-c189eb38779a"));

            migrationBuilder.DeleteData(
                table: "UserDepartments",
                keyColumn: "Id",
                keyValue: new Guid("9951e8a8-d4f2-48dc-a9fa-37a466226ce8"));

            migrationBuilder.DeleteData(
                table: "UserDepartments",
                keyColumn: "Id",
                keyValue: new Guid("9b65b3e7-7985-4aae-86cf-bc8b625e6d38"));

            migrationBuilder.DeleteData(
                table: "UserDepartments",
                keyColumn: "Id",
                keyValue: new Guid("d7b046e2-5fe3-4ce4-aa0b-1182feb04f54"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("15731756-347d-4812-bff7-18fb43895bd6"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("46170431-f2ed-4423-a73c-270cf206fa3a"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("691565e3-1c09-4789-9f96-631c654e2f23"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("79c13a86-cd80-40d4-aee5-f40172d2fc1d"));

            migrationBuilder.AddColumn<bool>(
                name: "IsDepartmentHead",
                table: "DepartmentRolePermissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

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

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: new Guid("86deff8b-cb4b-4daf-88d4-6f366b051836"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(3215), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(3215) });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: new Guid("8bf13891-1ce9-405c-add9-0ada93308671"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(3015), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(3134) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(9111), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(9210) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(9293), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(9294) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(9295), new DateTime(2025, 6, 18, 10, 31, 3, 912, DateTimeKind.Utc).AddTicks(9296) });

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

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1517), new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1608) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1702), new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1702) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1700), new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1701) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1698), new DateTime(2025, 6, 18, 10, 31, 3, 913, DateTimeKind.Utc).AddTicks(1699) });

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

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 10, 31, 3, 939, DateTimeKind.Utc).AddTicks(6915), "q2JeW34Zf0qgo5Ra6+iBTp0R5u1BvoRrRWEWQRmKK9j4TV+hPSc3E3RGV5zm7Elc", new DateTime(2025, 6, 18, 10, 31, 3, 939, DateTimeKind.Utc).AddTicks(7055) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 10, 31, 3, 957, DateTimeKind.Utc).AddTicks(9247), "QXB0X/Yqm3s2WhU2A0tKMh7ECPNhKJXuqzwe2vPRyLFkj2vzPQRnVvkUYPq1VERq", new DateTime(2025, 6, 18, 10, 31, 3, 957, DateTimeKind.Utc).AddTicks(9252) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 10, 31, 3, 994, DateTimeKind.Utc).AddTicks(4293), "I1SWkV6xjXmynG61xnc37kNCzYlJ/A4hpgmxMK70eDjmB9GGXHUfY/f0dinwAu9h", new DateTime(2025, 6, 18, 10, 31, 3, 994, DateTimeKind.Utc).AddTicks(4298) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("fd05266c-baf5-49bb-a846-554461bcc411"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 18, 10, 31, 3, 975, DateTimeKind.Utc).AddTicks(9534), "YddDKQDbGZYjGznmnLXnZTHkX7lxFX4pUjyVJmaobVsQxP5BANwidpyf4SeYB3iQ", new DateTime(2025, 6, 18, 10, 31, 3, 975, DateTimeKind.Utc).AddTicks(9540) });
        }
    }
}
