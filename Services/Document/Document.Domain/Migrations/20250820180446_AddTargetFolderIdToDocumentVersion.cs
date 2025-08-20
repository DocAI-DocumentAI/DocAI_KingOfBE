using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetFolderIdToDocumentVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetFolderId",
                table: "DocumentVersions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_TargetFolderId",
                table: "DocumentVersions",
                column: "TargetFolderId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentVersions_Folders_TargetFolderId",
                table: "DocumentVersions",
                column: "TargetFolderId",
                principalTable: "Folders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentVersions_Folders_TargetFolderId",
                table: "DocumentVersions");

            migrationBuilder.DropIndex(
                name: "IX_DocumentVersions_TargetFolderId",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "TargetFolderId",
                table: "DocumentVersions");
        }
    }
}
