using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document.Domain.Migrations
{
    /// <inheritdoc />
    public partial class addnavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReplacementDocumentId",
                table: "DocumentFiles",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFiles_ReplacementDocumentId",
                table: "DocumentFiles",
                column: "ReplacementDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentFiles_DocumentFiles_ReplacementDocumentId",
                table: "DocumentFiles",
                column: "ReplacementDocumentId",
                principalTable: "DocumentFiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentFiles_DocumentFiles_ReplacementDocumentId",
                table: "DocumentFiles");

            migrationBuilder.DropIndex(
                name: "IX_DocumentFiles_ReplacementDocumentId",
                table: "DocumentFiles");

            migrationBuilder.DropColumn(
                name: "ReplacementDocumentId",
                table: "DocumentFiles");
        }
    }
}
