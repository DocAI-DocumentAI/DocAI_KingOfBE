using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddReplacedByIdToDocumentFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentFiles_DocumentFiles_ReplacementDocumentId",
                table: "DocumentFiles");

            migrationBuilder.RenameColumn(
                name: "ReplacementDocumentId",
                table: "DocumentFiles",
                newName: "ReplacedById");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentFiles_ReplacementDocumentId",
                table: "DocumentFiles",
                newName: "IX_DocumentFiles_ReplacedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFiles_ReplacementId",
                table: "DocumentFiles",
                column: "ReplacementId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentFiles_DocumentFiles_ReplacedById",
                table: "DocumentFiles",
                column: "ReplacedById",
                principalTable: "DocumentFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentFiles_DocumentFiles_ReplacementId",
                table: "DocumentFiles",
                column: "ReplacementId",
                principalTable: "DocumentFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentFiles_DocumentFiles_ReplacedById",
                table: "DocumentFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentFiles_DocumentFiles_ReplacementId",
                table: "DocumentFiles");

            migrationBuilder.DropIndex(
                name: "IX_DocumentFiles_ReplacementId",
                table: "DocumentFiles");

            migrationBuilder.RenameColumn(
                name: "ReplacedById",
                table: "DocumentFiles",
                newName: "ReplacementDocumentId");

            migrationBuilder.RenameIndex(
                name: "IX_DocumentFiles_ReplacedById",
                table: "DocumentFiles",
                newName: "IX_DocumentFiles_ReplacementDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentFiles_DocumentFiles_ReplacementDocumentId",
                table: "DocumentFiles",
                column: "ReplacementDocumentId",
                principalTable: "DocumentFiles",
                principalColumn: "Id");
        }
    }
}
