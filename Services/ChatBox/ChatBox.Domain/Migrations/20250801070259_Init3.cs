using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatBox.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Init3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPreferences_ChatSessions_ChatSessionId",
                table: "UserPreferences");

            migrationBuilder.DropIndex(
                name: "IX_UserPreferences_ChatSessionId",
                table: "UserPreferences");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_SessionId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ChatSessionId",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "ApiKey",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "Endpoint",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "TopK",
                table: "AIConfigurations");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "UserPreferences",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "UserPreferences",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "UserPreferences",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "SessionId",
                table: "UserPreferences",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "UserPreferences",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ChatSessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "ChatSessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "ModelName",
                table: "ChatSessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ModelName",
                table: "AIConfigurations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "AIConfigurations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "AIConfigurations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsFree",
                table: "AIConfigurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_SessionId",
                table: "UserPreferences",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_UserId_SessionId",
                table: "UserPreferences",
                columns: new[] { "UserId", "SessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_UserId_IsActive",
                table: "ChatSessions",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SessionId_CreatedAt",
                table: "ChatMessages",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AIConfigurations_IsActive",
                table: "AIConfigurations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AIConfigurations_ModelName",
                table: "AIConfigurations",
                column: "ModelName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPreferences_ChatSessions_SessionId",
                table: "UserPreferences",
                column: "SessionId",
                principalTable: "ChatSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPreferences_ChatSessions_SessionId",
                table: "UserPreferences");

            migrationBuilder.DropIndex(
                name: "IX_UserPreferences_SessionId",
                table: "UserPreferences");

            migrationBuilder.DropIndex(
                name: "IX_UserPreferences_UserId_SessionId",
                table: "UserPreferences");

            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_UserId_IsActive",
                table: "ChatSessions");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_SessionId_CreatedAt",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_AIConfigurations_IsActive",
                table: "AIConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_AIConfigurations_ModelName",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "AIConfigurations");

            migrationBuilder.DropColumn(
                name: "IsFree",
                table: "AIConfigurations");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "UserPreferences",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "UserPreferences",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "UserPreferences",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SessionId",
                table: "UserPreferences",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "UserPreferences",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChatSessionId",
                table: "UserPreferences",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ChatSessions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "ChatSessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "ModelName",
                table: "ChatSessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "ModelName",
                table: "AIConfigurations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "AIConfigurations",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiKey",
                table: "AIConfigurations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Endpoint",
                table: "AIConfigurations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "AIConfigurations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "TopK",
                table: "AIConfigurations",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_ChatSessionId",
                table: "UserPreferences",
                column: "ChatSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SessionId",
                table: "ChatMessages",
                column: "SessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserPreferences_ChatSessions_ChatSessionId",
                table: "UserPreferences",
                column: "ChatSessionId",
                principalTable: "ChatSessions",
                principalColumn: "Id");
        }
    }
}
