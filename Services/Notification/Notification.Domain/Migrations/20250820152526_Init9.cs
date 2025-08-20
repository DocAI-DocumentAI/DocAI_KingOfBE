using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Init9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "NotificationLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "NotificationLogs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "NotificationLogs");
        }
    }
}
