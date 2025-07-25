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
                table: "UserPermissions",
                keyColumn: "Id",
                keyValue: new Guid("0c56263b-b1f0-43d2-a701-a6d66e9c2b4f"));

            migrationBuilder.DeleteData(
                table: "UserPermissions",
                keyColumn: "Id",
                keyValue: new Guid("42f53500-74bb-4883-b397-6a511ccab4c6"));

            migrationBuilder.DeleteData(
                table: "UserPermissions",
                keyColumn: "Id",
                keyValue: new Guid("68f0ed30-8378-4c7a-9ac1-7e59efd23f7d"));

            migrationBuilder.DeleteData(
                table: "UserPermissions",
                keyColumn: "Id",
                keyValue: new Guid("b00f2a64-ed45-40fa-9941-cee61140529d"));

            migrationBuilder.DeleteData(
                table: "UserPermissions",
                keyColumn: "Id",
                keyValue: new Guid("b1ebed91-c7b3-41cd-9364-dbff90079fbc"));

            migrationBuilder.AddColumn<string>(
                name: "GoogleId",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequirePasswordChange",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: new Guid("8bf13891-1ce9-405c-add9-0ada93308671"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 25, 2, 4, 13, 575, DateTimeKind.Utc).AddTicks(62), new DateTime(2025, 7, 25, 2, 4, 13, 575, DateTimeKind.Utc).AddTicks(210) });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: new Guid("d8854d21-8fae-46aa-b51b-0de060b92ee3"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 25, 2, 4, 13, 575, DateTimeKind.Utc).AddTicks(328), new DateTime(2025, 7, 25, 2, 4, 13, 575, DateTimeKind.Utc).AddTicks(328) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 25, 2, 4, 13, 576, DateTimeKind.Utc).AddTicks(1950), new DateTime(2025, 7, 25, 2, 4, 13, 576, DateTimeKind.Utc).AddTicks(2094) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 25, 2, 4, 13, 576, DateTimeKind.Utc).AddTicks(2209), new DateTime(2025, 7, 25, 2, 4, 13, 576, DateTimeKind.Utc).AddTicks(2210) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 25, 2, 4, 13, 576, DateTimeKind.Utc).AddTicks(2212), new DateTime(2025, 7, 25, 2, 4, 13, 576, DateTimeKind.Utc).AddTicks(2213) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 25, 2, 4, 13, 576, DateTimeKind.Utc).AddTicks(5545), new DateTime(2025, 7, 25, 2, 4, 13, 576, DateTimeKind.Utc).AddTicks(5675) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 25, 2, 4, 13, 576, DateTimeKind.Utc).AddTicks(5786), new DateTime(2025, 7, 25, 2, 4, 13, 576, DateTimeKind.Utc).AddTicks(5787) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 25, 2, 4, 13, 576, DateTimeKind.Utc).AddTicks(5784), new DateTime(2025, 7, 25, 2, 4, 13, 576, DateTimeKind.Utc).AddTicks(5784) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 25, 2, 4, 13, 576, DateTimeKind.Utc).AddTicks(5780), new DateTime(2025, 7, 25, 2, 4, 13, 576, DateTimeKind.Utc).AddTicks(5781) });

            migrationBuilder.InsertData(
                table: "UserPermissions",
                columns: new[] { "Id", "PermissionId", "UserId" },
                values: new object[,]
                {
                    { new Guid("01e87f24-c7f0-46bd-b6c9-7d23cdb61c4b"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("fd05266c-baf5-49bb-a846-554461bcc411") },
                    { new Guid("16315e3e-d39b-4538-a4a5-ddf77f4f9b5e"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") },
                    { new Guid("c166c7a7-45b5-4854-8839-ce429a441796"), new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"), new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd") },
                    { new Guid("cd9131d1-6df6-4d20-9d67-81ecce5d6021"), new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"), new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c") },
                    { new Guid("fcd64c01-c32b-4e53-bbb0-25c1069d3e6e"), new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"), new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616") }
                });

            migrationBuilder.UpdateData(
                table: "UserSettings",
                keyColumn: "Id",
                keyValue: new Guid("4e8bff21-b470-4b9e-92da-400d21992f96"),
                column: "UpdateAt",
                value: new DateTime(2025, 7, 25, 2, 4, 13, 688, DateTimeKind.Utc).AddTicks(158));

            migrationBuilder.UpdateData(
                table: "UserSettings",
                keyColumn: "Id",
                keyValue: new Guid("86254802-1d1e-4734-a25b-ef22ff39cefc"),
                column: "UpdateAt",
                value: new DateTime(2025, 7, 25, 2, 4, 13, 688, DateTimeKind.Utc).AddTicks(155));

            migrationBuilder.UpdateData(
                table: "UserSettings",
                keyColumn: "Id",
                keyValue: new Guid("dd9105eb-4df0-4c32-bc55-fd0169e386fc"),
                column: "UpdateAt",
                value: new DateTime(2025, 7, 25, 2, 4, 13, 688, DateTimeKind.Utc).AddTicks(152));

            migrationBuilder.UpdateData(
                table: "UserSettings",
                keyColumn: "Id",
                keyValue: new Guid("ddfcbea3-56e9-4187-97f6-521ca24c2412"),
                column: "UpdateAt",
                value: new DateTime(2025, 7, 25, 2, 4, 13, 687, DateTimeKind.Utc).AddTicks(9779));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616"),
                columns: new[] { "CreatAt", "GoogleId", "Password", "RequirePasswordChange", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 25, 2, 4, 13, 609, DateTimeKind.Utc).AddTicks(3354), null, "Y3m59mJ6EDDkGYtkeEbe6h7afaJRKgT6EoLzX6rMhGVw/aiamhwhSuXmKx8HSOoq", true, new DateTime(2025, 7, 25, 2, 4, 13, 609, DateTimeKind.Utc).AddTicks(3499) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"),
                columns: new[] { "CreatAt", "GoogleId", "Password", "RequirePasswordChange", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 25, 2, 4, 13, 635, DateTimeKind.Utc).AddTicks(8932), null, "96VuXRHeVRqXO/cAb8l/KcptLkGVjrz2MNLwxsPOwIvoJrneJRPQUhP6vvCzo8jE", true, new DateTime(2025, 7, 25, 2, 4, 13, 635, DateTimeKind.Utc).AddTicks(8939) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"),
                columns: new[] { "CreatAt", "GoogleId", "Password", "RequirePasswordChange", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 25, 2, 4, 13, 687, DateTimeKind.Utc).AddTicks(659), null, "1W1YmeohUmI/KtOedWhW9ys/SVlQH8F3F5xv1z8gaDjJxggcnhrzazLc+Nss4EkN", true, new DateTime(2025, 7, 25, 2, 4, 13, 687, DateTimeKind.Utc).AddTicks(667) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("fd05266c-baf5-49bb-a846-554461bcc411"),
                columns: new[] { "CreatAt", "GoogleId", "Password", "RequirePasswordChange", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 25, 2, 4, 13, 661, DateTimeKind.Utc).AddTicks(7964), null, "+n2eqp7ONRAzDTbR8TAbDHqNrjvnCz6IR6TsD0cjAtndXVwhgOFINW7R2Q4QTb39", true, new DateTime(2025, 7, 25, 2, 4, 13, 661, DateTimeKind.Utc).AddTicks(7970) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "UserPermissions",
                keyColumn: "Id",
                keyValue: new Guid("01e87f24-c7f0-46bd-b6c9-7d23cdb61c4b"));

            migrationBuilder.DeleteData(
                table: "UserPermissions",
                keyColumn: "Id",
                keyValue: new Guid("16315e3e-d39b-4538-a4a5-ddf77f4f9b5e"));

            migrationBuilder.DeleteData(
                table: "UserPermissions",
                keyColumn: "Id",
                keyValue: new Guid("c166c7a7-45b5-4854-8839-ce429a441796"));

            migrationBuilder.DeleteData(
                table: "UserPermissions",
                keyColumn: "Id",
                keyValue: new Guid("cd9131d1-6df6-4d20-9d67-81ecce5d6021"));

            migrationBuilder.DeleteData(
                table: "UserPermissions",
                keyColumn: "Id",
                keyValue: new Guid("fcd64c01-c32b-4e53-bbb0-25c1069d3e6e"));

            migrationBuilder.DropColumn(
                name: "GoogleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RequirePasswordChange",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: new Guid("8bf13891-1ce9-405c-add9-0ada93308671"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 19, 7, 56, 35, 503, DateTimeKind.Utc).AddTicks(5751), new DateTime(2025, 7, 19, 7, 56, 35, 503, DateTimeKind.Utc).AddTicks(5861) });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: new Guid("d8854d21-8fae-46aa-b51b-0de060b92ee3"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 19, 7, 56, 35, 503, DateTimeKind.Utc).AddTicks(6064), new DateTime(2025, 7, 19, 7, 56, 35, 503, DateTimeKind.Utc).AddTicks(6065) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("3796cdb0-7c0a-4cc6-a757-883fe1865fb6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(3401), new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(3502) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("e72214a0-24bc-471a-aca5-d897f4da0aad"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(3672), new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(3674) });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("febebe25-dd94-4ba1-bdbd-810e4503bccd"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(3676), new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(3676) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("4e29a870-9131-4cc2-97ca-eaa748b5f17f"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(5979), new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(6068) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("8e7d55e4-67d3-4b73-9995-21b163493136"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(6150), new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(6150) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a5ddf431-aae9-4d9f-8d61-1a37776bb4bb"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(6148), new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(6148) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("a996692c-1f5e-4458-8dcf-c2494a47b6d6"),
                columns: new[] { "CreateAt", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(6146), new DateTime(2025, 7, 19, 7, 56, 35, 504, DateTimeKind.Utc).AddTicks(6146) });

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

            migrationBuilder.UpdateData(
                table: "UserSettings",
                keyColumn: "Id",
                keyValue: new Guid("4e8bff21-b470-4b9e-92da-400d21992f96"),
                column: "UpdateAt",
                value: new DateTime(2025, 7, 19, 7, 56, 35, 591, DateTimeKind.Utc).AddTicks(3474));

            migrationBuilder.UpdateData(
                table: "UserSettings",
                keyColumn: "Id",
                keyValue: new Guid("86254802-1d1e-4734-a25b-ef22ff39cefc"),
                column: "UpdateAt",
                value: new DateTime(2025, 7, 19, 7, 56, 35, 591, DateTimeKind.Utc).AddTicks(3471));

            migrationBuilder.UpdateData(
                table: "UserSettings",
                keyColumn: "Id",
                keyValue: new Guid("dd9105eb-4df0-4c32-bc55-fd0169e386fc"),
                column: "UpdateAt",
                value: new DateTime(2025, 7, 19, 7, 56, 35, 591, DateTimeKind.Utc).AddTicks(3469));

            migrationBuilder.UpdateData(
                table: "UserSettings",
                keyColumn: "Id",
                keyValue: new Guid("ddfcbea3-56e9-4187-97f6-521ca24c2412"),
                column: "UpdateAt",
                value: new DateTime(2025, 7, 19, 7, 56, 35, 591, DateTimeKind.Utc).AddTicks(3274));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("13d466ed-8a2d-414d-88c0-9c7adcac2616"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 19, 7, 56, 35, 529, DateTimeKind.Utc).AddTicks(9353), "TuCboI2USCS1QIyw6VyWc8tCL2THhdytgapBAeJ3JZLExF6ZwuT4C9Ed0U1GMkIN", new DateTime(2025, 7, 19, 7, 56, 35, 529, DateTimeKind.Utc).AddTicks(9468) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("595dd357-aaec-455e-9fa7-4fc88d4b819c"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 19, 7, 56, 35, 549, DateTimeKind.Utc).AddTicks(7435), "3M8X+PhlXm7jFY2AnRf4vN+DlkNfkHDdob+favJqvElQ3Wu8yc5E8QK9XgROx1zD", new DateTime(2025, 7, 19, 7, 56, 35, 549, DateTimeKind.Utc).AddTicks(7440) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5c49c1cb-719e-42eb-8028-f2eb3eaea4cd"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 19, 7, 56, 35, 590, DateTimeKind.Utc).AddTicks(5804), "8LYWgc6vmJw/N1dWFw6Kme0xHlScHmMEXXKC2ZjS6BhNzUnKV17vqjJhLizxSs52", new DateTime(2025, 7, 19, 7, 56, 35, 590, DateTimeKind.Utc).AddTicks(5810) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("fd05266c-baf5-49bb-a846-554461bcc411"),
                columns: new[] { "CreatAt", "Password", "UpdateAt" },
                values: new object[] { new DateTime(2025, 7, 19, 7, 56, 35, 570, DateTimeKind.Utc).AddTicks(1797), "S4PnHpqjUYW10IHpMqCr6suaVDHTNW09huYZhD4nZn9AOIGbRFQcJ0EKAnD4GImD", new DateTime(2025, 7, 19, 7, 56, 35, 570, DateTimeKind.Utc).AddTicks(1801) });
        }
    }
}
