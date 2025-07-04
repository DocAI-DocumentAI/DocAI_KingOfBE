using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document.Domain.Migrations
{
    /// <inheritdoc />
    public partial class addcascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentChunks_DocumentFiles_DocumentFileId",
                table: "DocumentChunks");

            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_DocumentFileId",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "DocumentFileId",
                table: "DocumentChunks");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_DocumentId",
                table: "DocumentChunks",
                column: "DocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentChunks_DocumentFiles_DocumentId",
                table: "DocumentChunks",
                column: "DocumentId",
                principalTable: "DocumentFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentChunks_DocumentFiles_DocumentId",
                table: "DocumentChunks");

            migrationBuilder.DropIndex(
                name: "IX_DocumentChunks_DocumentId",
                table: "DocumentChunks");

            migrationBuilder.AddColumn<string>(
                name: "DocumentFileId",
                table: "DocumentChunks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_DocumentFileId",
                table: "DocumentChunks",
                column: "DocumentFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentChunks_DocumentFiles_DocumentFileId",
                table: "DocumentChunks",
                column: "DocumentFileId",
                principalTable: "DocumentFiles",
                principalColumn: "Id");
        }
    }
}
