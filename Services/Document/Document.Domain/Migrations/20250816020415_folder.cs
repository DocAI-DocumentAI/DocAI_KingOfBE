using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document.Domain.Migrations
{
    /// <inheritdoc />
    public partial class folder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FolderId",
                table: "DocumentVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Id",
                table: "ApprovalClaims",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Folders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DepartmentId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ParentFolderId = table.Column<string>(type: "text", nullable: true),
                    GoogleDriveFolderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsSystemFolder = table.Column<bool>(type: "boolean", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    FullPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FolderType = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    SubFolderCount = table.Column<int>(type: "integer", nullable: false),
                    DocumentCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Folders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Folders_Folders_ParentFolderId",
                        column: x => x.ParentFolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FolderPermissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FolderId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DepartmentId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PermissionType = table.Column<int>(type: "integer", nullable: false),
                    IsInherited = table.Column<bool>(type: "boolean", nullable: false),
                    ParentPermissionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsDenied = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FolderPermissions", x => x.Id);
                    table.CheckConstraint("CK_FolderPermissions_NotBothUserAndDepartment", "NOT (\"UserId\" IS NOT NULL AND \"DepartmentId\" IS NOT NULL)");
                    table.CheckConstraint("CK_FolderPermissions_UserOrDepartment", "\"UserId\" IS NOT NULL OR \"DepartmentId\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_FolderPermissions_FolderPermissions_ParentPermissionId",
                        column: x => x.ParentPermissionId,
                        principalTable: "FolderPermissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FolderPermissions_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_FolderId",
                table: "DocumentVersions",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_DepartmentId",
                table: "FolderPermissions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_ExpiresAt",
                table: "FolderPermissions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_Folder_User_Department",
                table: "FolderPermissions",
                columns: new[] { "FolderId", "UserId", "DepartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_FolderId",
                table: "FolderPermissions",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_IsActive",
                table: "FolderPermissions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_IsInherited",
                table: "FolderPermissions",
                column: "IsInherited");

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_ParentPermissionId",
                table: "FolderPermissions",
                column: "ParentPermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_FolderPermissions_UserId",
                table: "FolderPermissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_DepartmentId",
                table: "Folders",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_FullPath",
                table: "Folders",
                column: "FullPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Folders_GoogleDriveId",
                table: "Folders",
                column: "GoogleDriveFolderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Folders_IsDeleted",
                table: "Folders",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_IsPublic",
                table: "Folders",
                column: "IsPublic");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_IsSystemFolder",
                table: "Folders",
                column: "IsSystemFolder");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_ParentId_Name",
                table: "Folders",
                columns: new[] { "ParentFolderId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentVersions_Folders_FolderId",
                table: "DocumentVersions",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentVersions_Folders_FolderId",
                table: "DocumentVersions");

            migrationBuilder.DropTable(
                name: "FolderPermissions");

            migrationBuilder.DropTable(
                name: "Folders");

            migrationBuilder.DropIndex(
                name: "IX_DocumentVersions_FolderId",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ApprovalClaims");
        }
    }
}
