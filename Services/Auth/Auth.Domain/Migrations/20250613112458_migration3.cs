using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Auth.Domain.Migrations
{
    /// <inheritdoc />
    public partial class migration3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.InsertData(
                table: "ActiveKeys",
                columns: new[] { "Id", "ActivationCode", "RoleName" },
                values: new object[,]
                {
                    { new Guid("23deb45b-705a-4c86-be05-61201fcac8be"), "g1UHzv7McAbpRKeYwXd29fQsTNLqJo5C", "Director" },
                    { new Guid("50b64957-bae3-4377-aa7a-fee36d25ccd6"), "P4rBZtdXa5YvEGJNmKLcQq7RfW9HU61o", "Editor" },
                    { new Guid("65de7f7d-0bcc-4cdf-bd8c-f8d1ac290cd8"), "zXYmN7pLcVTEqF59jKADrCbhQuU630aw", "Manager" }
                });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 11, 24, 57, 766, DateTimeKind.Utc).AddTicks(9356), new DateTime(2025, 6, 13, 11, 24, 57, 766, DateTimeKind.Utc).AddTicks(9463) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 11, 24, 57, 766, DateTimeKind.Utc).AddTicks(9543), new DateTime(2025, 6, 13, 11, 24, 57, 766, DateTimeKind.Utc).AddTicks(9544) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 11, 24, 57, 766, DateTimeKind.Utc).AddTicks(9545), new DateTime(2025, 6, 13, 11, 24, 57, 766, DateTimeKind.Utc).AddTicks(9546) });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("038e1f74-4cb4-439d-95bb-72392eb287b4"), new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f") },
                    { new Guid("145ecec9-7832-4c8a-9ec1-76c4276ffc81"), new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb") },
                    { new Guid("51badb5e-f95e-4e31-aaac-5486f7d0c70f"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb") },
                    { new Guid("826f4cea-78ec-4aa9-9bef-52474b1b87c5"), new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6") },
                    { new Guid("c735035b-6a25-4687-aa75-1b053b8378c9"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("8e7d55e4-67d3-4b73-9995-21b163493136") },
                    { new Guid("e2a0273b-875d-4de9-a224-525a10d0325a"), new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new Guid("8e7d55e4-67d3-4b73-9995-21b163493136") },
                    { new Guid("ff7cb06c-ceb7-4ee9-b59e-a1c747d371f6"), new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new Guid("9ede53c4-e407-492c-8dfe-d87185575cf0") }
                });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 11, 24, 57, 767, DateTimeKind.Utc).AddTicks(2108), new DateTime(2025, 6, 13, 11, 24, 57, 767, DateTimeKind.Utc).AddTicks(2198) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 11, 24, 57, 767, DateTimeKind.Utc).AddTicks(2280), new DateTime(2025, 6, 13, 11, 24, 57, 767, DateTimeKind.Utc).AddTicks(2280) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("9ede53c4-e407-492c-8dfe-d87185575cf0"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 11, 24, 57, 767, DateTimeKind.Utc).AddTicks(2282), new DateTime(2025, 6, 13, 11, 24, 57, 767, DateTimeKind.Utc).AddTicks(2282) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 11, 24, 57, 767, DateTimeKind.Utc).AddTicks(2278), new DateTime(2025, 6, 13, 11, 24, 57, 767, DateTimeKind.Utc).AddTicks(2278) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 11, 24, 57, 767, DateTimeKind.Utc).AddTicks(2276), new DateTime(2025, 6, 13, 11, 24, 57, 767, DateTimeKind.Utc).AddTicks(2276) });

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "Id", "RoleId", "UserId" },
                values: new object[,]
                {
                    { new Guid("0f0b8157-b313-4ff9-8e35-6ee3452522de"), new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"), new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd") },
                    { new Guid("177b2978-e8ee-4cf0-b75f-4ec5c11e16e5"), new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") },
                    { new Guid("2c0e82ed-444b-4474-aefe-1ea7c82a92fa"), new Guid("9ede53c4-e407-492c-8dfe-d87185575cf0"), new Guid("a9df371e-9249-47b8-83ce-8cd940140b9b") },
                    { new Guid("6864d47f-f5cf-4b66-837d-3a7cd28639cd"), new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"), new Guid("fd05266c-baf5-49bb-a846-554461bcc411") },
                    { new Guid("891e20a1-1d68-46b0-8019-8f7edd11795c"), new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"), new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616") }
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 11, 24, 57, 791, DateTimeKind.Utc).AddTicks(3833), "baXt++NEsqjRMW2jx4k5srt+YMOLG3JVG7+9abANDw0HyoBVUB/vcb2FXjMDPXqJ", new DateTime(2025, 6, 13, 11, 24, 57, 791, DateTimeKind.Utc).AddTicks(3973) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 11, 24, 57, 828, DateTimeKind.Utc).AddTicks(4158), "h6ks7YC/YuaIJHRDV9XsCcIvXcdC/MOO281YjOfPovHjwtMAX6Z6a2OfgNhJ6YY1", new DateTime(2025, 6, 13, 11, 24, 57, 828, DateTimeKind.Utc).AddTicks(4163) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 11, 24, 57, 865, DateTimeKind.Utc).AddTicks(9614), "gf+67eSJisOBAHM+ouJNh+LonniztEwWg9Tgw3QgiW9FneQVVK/dHjibWXh2kovW", new DateTime(2025, 6, 13, 11, 24, 57, 865, DateTimeKind.Utc).AddTicks(9619) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a9df371e-9249-47b8-83ce-8cd940140b9b"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 11, 24, 57, 809, DateTimeKind.Utc).AddTicks(9018), "58YULs2gyUTVDsWupFKewNrOySTOcSSNKIW6SsZ11071zKmb53sT3kGFCBXDSKHW", new DateTime(2025, 6, 13, 11, 24, 57, 809, DateTimeKind.Utc).AddTicks(9025) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("fd05266c-baf5-49bb-a846-554461bcc411"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 11, 24, 57, 847, DateTimeKind.Utc).AddTicks(4112), "dssDadhplLlg/i2sqAP7PkUQi2JZsumEIrhS4H63PADxD2YzqF3vl+rLM2mEHLs7", new DateTime(2025, 6, 13, 11, 24, 57, 847, DateTimeKind.Utc).AddTicks(4119) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveKeys");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("038e1f74-4cb4-439d-95bb-72392eb287b4"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("145ecec9-7832-4c8a-9ec1-76c4276ffc81"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("51badb5e-f95e-4e31-aaac-5486f7d0c70f"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("826f4cea-78ec-4aa9-9bef-52474b1b87c5"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("c735035b-6a25-4687-aa75-1b053b8378c9"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("e2a0273b-875d-4de9-a224-525a10d0325a"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("ff7cb06c-ceb7-4ee9-b59e-a1c747d371f6"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("0f0b8157-b313-4ff9-8e35-6ee3452522de"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("177b2978-e8ee-4cf0-b75f-4ec5c11e16e5"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("2c0e82ed-444b-4474-aefe-1ea7c82a92fa"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("6864d47f-f5cf-4b66-837d-3a7cd28639cd"));

            migrationBuilder.DeleteData(
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("891e20a1-1d68-46b0-8019-8f7edd11795c"));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 31, 12, 679, DateTimeKind.Utc).AddTicks(9847), new DateTime(2025, 6, 13, 7, 31, 12, 679, DateTimeKind.Utc).AddTicks(9949) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(125), new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(126) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(128), new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(128) });

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

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7447), new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7544) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7622), new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7623) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("9ede53c4-e407-492c-8dfe-d87185575cf0"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7624), new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7624) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7620), new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7621) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7618), new DateTime(2025, 6, 13, 7, 31, 12, 680, DateTimeKind.Utc).AddTicks(7619) });

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
        }
    }
}
