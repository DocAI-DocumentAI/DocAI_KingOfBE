using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Init15 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExpiredNotificationCron",
                table: "NotificationConfigs",
                newName: "DocumentStatusUpdateCron");

            migrationBuilder.UpdateData(
                table: "NotificationConfigs",
                keyColumn: "Id",
                keyValue: new Guid("c3d4e5f6-a7b8-9012-3456-7890abcdef12"),
                column: "DocumentStatusUpdateCron",
                value: "0 0 0 * * ?");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DocumentStatusUpdateCron",
                table: "NotificationConfigs",
                newName: "ExpiredNotificationCron");

            migrationBuilder.UpdateData(
                table: "NotificationConfigs",
                keyColumn: "Id",
                keyValue: new Guid("c3d4e5f6-a7b8-9012-3456-7890abcdef12"),
                column: "ExpiredNotificationCron",
                value: "0 0 6 * * ?");
        }
    }
}
