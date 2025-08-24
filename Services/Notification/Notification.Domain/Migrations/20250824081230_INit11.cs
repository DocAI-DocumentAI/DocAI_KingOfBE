using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Domain.Migrations
{
    /// <inheritdoc />
    public partial class INit11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "NotificationConfigs",
                keyColumn: "Id",
                keyValue: new Guid("c3d4e5f6-a7b8-9012-3456-7890abcdef12"),
                columns: new[] { "ExpiredNotificationCron", "NearExpiredMode", "NearExpiredNotificationCron" },
                values: new object[] { "0 0 6 * * ?", 2, "0 0 6 * * ?" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "NotificationConfigs",
                keyColumn: "Id",
                keyValue: new Guid("c3d4e5f6-a7b8-9012-3456-7890abcdef12"),
                columns: new[] { "ExpiredNotificationCron", "NearExpiredMode", "NearExpiredNotificationCron" },
                values: new object[] { "0 0 8 * * ?", 1, "0 0 9 ? * MON" });
        }
    }
}
