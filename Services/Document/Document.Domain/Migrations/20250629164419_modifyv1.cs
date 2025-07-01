using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document.Domain.Migrations
{
    /// <inheritdoc />
    public partial class modifyv1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentTags_DocumentFiles_DocumentFileId",
                table: "DocumentTags");

            migrationBuilder.DropIndex(
                name: "IX_DocumentTags_DocumentFileId",
                table: "DocumentTags");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "DocumentFileId",
                table: "DocumentTags");

            migrationBuilder.DropColumn(
                name: "DocumentName",
                table: "DocumentFiles");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "DocumentFiles");

            migrationBuilder.DropColumn(
                name: "EffectiveUntil",
                table: "DocumentFiles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DocumentFiles");

            migrationBuilder.RenameColumn(
                name: "Version",
                table: "DocumentVersions",
                newName: "VersionName");

            migrationBuilder.RenameColumn(
                name: "DocumentId",
                table: "DocumentTags",
                newName: "DocumentVersionId");

            migrationBuilder.RenameColumn(
                name: "StoragePath",
                table: "DocumentFiles",
                newName: "OwnerId");

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveFrom",
                table: "DocumentVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveUntil",
                table: "DocumentVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedBy",
                table: "DocumentVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "DocumentVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "DocumentVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "DocumentVersions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TotalDownloads",
                table: "DocumentVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "DocumentFiles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DepartmentId",
                table: "DocumentFiles",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ApprovalLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Comments = table.Column<string>(type: "text", nullable: true),
                    DocumentVersionId = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalLogs_DocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTags_DocumentVersionId",
                table: "DocumentTags",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalLogs_DocumentVersionId",
                table: "ApprovalLogs",
                column: "DocumentVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentTags_DocumentVersions_DocumentVersionId",
                table: "DocumentTags",
                column: "DocumentVersionId",
                principalTable: "DocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentTags_DocumentVersions_DocumentVersionId",
                table: "DocumentTags");

            migrationBuilder.DropTable(
                name: "ApprovalLogs");

            migrationBuilder.DropIndex(
                name: "IX_DocumentTags_DocumentVersionId",
                table: "DocumentTags");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "EffectiveUntil",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "SignedBy",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "TotalDownloads",
                table: "DocumentVersions");

            migrationBuilder.RenameColumn(
                name: "VersionName",
                table: "DocumentVersions",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "DocumentVersionId",
                table: "DocumentTags",
                newName: "DocumentId");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                table: "DocumentFiles",
                newName: "StoragePath");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Tags",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentFileId",
                table: "DocumentTags",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "DocumentFiles",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DepartmentId",
                table: "DocumentFiles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "DocumentName",
                table: "DocumentFiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveFrom",
                table: "DocumentFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveUntil",
                table: "DocumentFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "DocumentFiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTags_DocumentFileId",
                table: "DocumentTags",
                column: "DocumentFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentTags_DocumentFiles_DocumentFileId",
                table: "DocumentTags",
                column: "DocumentFileId",
                principalTable: "DocumentFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
