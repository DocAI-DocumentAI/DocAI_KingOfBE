using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document.Domain.Migrations
{
    /// <inheritdoc />
    public partial class modifybookmark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookmarks_DocumentVersions_DocumentVersionId",
                table: "Bookmarks");

            migrationBuilder.RenameColumn(
                name: "DocumentVersionId",
                table: "Bookmarks",
                newName: "DocumentId");

            migrationBuilder.RenameIndex(
                name: "IX_Bookmarks_DocumentVersionId",
                table: "Bookmarks",
                newName: "IX_Bookmarks_DocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookmarks_DocumentFiles_DocumentId",
                table: "Bookmarks",
                column: "DocumentId",
                principalTable: "DocumentFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookmarks_DocumentFiles_DocumentId",
                table: "Bookmarks");

            migrationBuilder.RenameColumn(
                name: "DocumentId",
                table: "Bookmarks",
                newName: "DocumentVersionId");

            migrationBuilder.RenameIndex(
                name: "IX_Bookmarks_DocumentId",
                table: "Bookmarks",
                newName: "IX_Bookmarks_DocumentVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookmarks_DocumentVersions_DocumentVersionId",
                table: "Bookmarks",
                column: "DocumentVersionId",
                principalTable: "DocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
