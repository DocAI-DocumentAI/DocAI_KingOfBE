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
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "CreateAt", "Description", "Name", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new DateTime(2025, 6, 13, 7, 31, 12, 679, DateTimeKind.Utc).AddTicks(9847), "Quyền xem mọi tài liệu trong hệ thống ", "VIEW_ANY_DOCUMENT", new DateTime(2025, 6, 13, 7, 31, 12, 679, DateTimeKind.Utc).AddTicks(9949) },
                    { new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(125), "Quyền xem tài liệu thuộc phòng ban của mình.", "VIEW_OWN_DEPARTMENT_DOCUMENT", new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(126) },
                    { new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(128), "Quyền xem tài liệu của mình.", "VIEW_DEPARTMENT_DOCUMENT", new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(128) }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreateAt", "Description", "RoleName", "UpdateAt" },
                values: new object[,]
                {
                    { new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7447), "Member", "Member", new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7544) },
                    { new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"), new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7622), "Editor", "Editor", new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7623) },
                    { new Guid("9ede53c4-e407-492c-8dfe-d87185575cf0"), new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7624), "Director", "Director", new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7624) },
                    { new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7620), "Manager", "Manager", new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7621) },
                    { new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7618), "Admin", "Admin", new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7619) }
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 31, 12, 704, DateTimeKind.Utc).AddTicks(8337), "i3lHZBayl4MpQJw3DAUkNyVWysdSbqfljDA6C3ngjZQt/ADGGigGimO96KZfJ4fk", new DateTime(2025, 6, 13, 7, 31, 12, 704, DateTimeKind.Utc).AddTicks(8470) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 31, 12, 741, DateTimeKind.Utc).AddTicks(4023), "NwehV43PgS3IXga/gmPc2cGRERQS77cYAo9CKumyEZicCXoQf2nB5EPYW7SdBlAR", new DateTime(2025, 6, 13, 7, 31, 12, 741, DateTimeKind.Utc).AddTicks(4029) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 31, 12, 778, DateTimeKind.Utc).AddTicks(3303), "ydZDKKk4mm8/kv7ySFmucR1zRgUW87Pc0fwTh8TiCw6afotaY6Z1gJi1EUWq/6Po", new DateTime(2025, 6, 13, 7, 31, 12, 778, DateTimeKind.Utc).AddTicks(3309) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a9df371e-9249-47b8-83ce-8cd940140b9b"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 31, 12, 723, DateTimeKind.Utc).AddTicks(2846), "vyyqF/yAhOMYdYF5UW1/YrxB+q9AaKmReVnYBfpfekl+LtoeRVd9hnj561YrUNNX", new DateTime(2025, 6, 13, 7, 31, 12, 723, DateTimeKind.Utc).AddTicks(2853) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("fd05266c-baf5-49bb-a846-554461bcc411"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 31, 12, 759, DateTimeKind.Utc).AddTicks(7525), "iBZx9nqFOupXYL5Czf/AFtgBlFWfVyzJjM3raMrd+xbS32TaNvTtx1TvbHCaP+4y", new DateTime(2025, 6, 13, 7, 31, 12, 759, DateTimeKind.Utc).AddTicks(7531) });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("07e07057-bddd-4dd7-ba54-1980b054a8cf"), new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb") },
                    { new Guid("30d4342b-11fb-478c-af3b-ded9913ba8a9"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("8e7d55e4-67d3-4b73-9995-21b163493136") },
                    { new Guid("31978c7f-e3cf-4f3e-9c4a-614e41275126"), new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f") },
                    { new Guid("55e1e0be-2b9f-46f1-8a39-e373532d57e2"), new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new Guid("9ede53c4-e407-492c-8dfe-d87185575cf0") },
                    { new Guid("c7868880-eaca-44d4-bd64-48766c0b72ae"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb") },
                    { new Guid("e6e42327-d469-4618-b8c8-98ced82e649a"), new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new Guid("8e7d55e4-67d3-4b73-9995-21b163493136") },
                    { new Guid("e781b792-bd6b-4e31-baff-b30c08682ab8"), new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6") }
                });

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "Id", "RoleId", "UserId" },
                values: new object[,]
                {
                    { new Guid("1579305a-615e-4241-a721-132979a0fc25"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new Guid("fd05266c-baf5-49bb-a846-554461bcc411") },
                    { new Guid("18e21089-9868-4520-ab15-9e472c682a2b"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") },
                    { new Guid("47c12250-85a8-479d-b6ef-49c933525847"), new Guid("9ede53c4-e407-492c-8dfe-d87185575cf0"), new Guid("a9df371e-9249-47b8-83ce-8cd940140b9b") },
                    { new Guid("64e234ce-3c50-4f51-95e8-4d813c1d4248"), new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"), new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd") },
                    { new Guid("81f00f35-c41e-4315-a23d-df8a6c138312"), new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("07e07057-bddd-4dd7-ba54-1980b054a8cf"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30d4342b-11fb-478c-af3b-ded9913ba8a9"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("31978c7f-e3cf-4f3e-9c4a-614e41275126"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("55e1e0be-2b9f-46f1-8a39-e373532d57e2"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("c7868880-eaca-44d4-bd64-48766c0b72ae"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("e6e42327-d469-4618-b8c8-98ced82e649a"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("e781b792-bd6b-4e31-baff-b30c08682ab8"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("1579305a-615e-4241-a721-132979a0fc25"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("18e21089-9868-4520-ab15-9e472c682a2b"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("47c12250-85a8-479d-b6ef-49c933525847"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("64e234ce-3c50-4f51-95e8-4d813c1d4248"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("81f00f35-c41e-4315-a23d-df8a6c138312"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("9ede53c4-e407-492c-8dfe-d87185575cf0"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 22, 48, 946, DateTimeKind.Utc).AddTicks(448), "t46fvHlNjS1VsKWT9awKE3NDNZg1pkqRGo6a7u+8acXXWcpIQEJBDVUT5oH1bWaY", new DateTime(2025, 6, 13, 7, 22, 48, 946, DateTimeKind.Utc).AddTicks(593) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 22, 48, 992, DateTimeKind.Utc).AddTicks(5564), "qyAWT5yKspDqvzMb+zNrloaRiZILt1CgI80TkNBrhvFbq0QyTQB5y3ECN8oaeInl", new DateTime(2025, 6, 13, 7, 22, 48, 992, DateTimeKind.Utc).AddTicks(5570) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 22, 49, 33, DateTimeKind.Utc).AddTicks(3191), "jjjKkjQHNHl5WVwGP3Sh0CPbONKaY6iXYcRb6Tt5sokROKBjnxxNeLD6mfBGA+/1", new DateTime(2025, 6, 13, 7, 22, 49, 33, DateTimeKind.Utc).AddTicks(3196) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a9df371e-9249-47b8-83ce-8cd940140b9b"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 22, 48, 967, DateTimeKind.Utc).AddTicks(7004), "C1wMjDFj2SH/X+FkqPAvtzhcHmHgUarGcHciJ6H5EvxYlkiLuaOWlV9aAQhpZtkE", new DateTime(2025, 6, 13, 7, 22, 48, 967, DateTimeKind.Utc).AddTicks(7011) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("fd05266c-baf5-49bb-a846-554461bcc411"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 22, 49, 13, DateTimeKind.Utc).AddTicks(2967), "wDGEikVIAgVki0rcudm15Gme0zrpLqTy4Cdw7DDqohv9FOGsUTBVBVivhfRINX8g", new DateTime(2025, 6, 13, 7, 22, 49, 13, DateTimeKind.Utc).AddTicks(2973) });
        }
    }
}
