using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTypeFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentTypeId",
                table: "DocumentFiles",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    LastUpdatedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFiles_DocumentTypeId",
                table: "DocumentFiles",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_Name",
                table: "DocumentTypes",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentFiles_DocumentTypes_DocumentTypeId",
                table: "DocumentFiles",
                column: "DocumentTypeId",
                principalTable: "DocumentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Create default document types
            var defaultDocumentTypeId = Guid.NewGuid().ToString("N");
            var policyDocumentTypeId = Guid.NewGuid().ToString("N");
            var procedureDocumentTypeId = Guid.NewGuid().ToString("N");
            var manualDocumentTypeId = Guid.NewGuid().ToString("N");
            var formDocumentTypeId = Guid.NewGuid().ToString("N");

            var currentTime = DateTime.UtcNow;

            // Insert default document types
            migrationBuilder.InsertData(
                table: "DocumentTypes",
                columns: new[] { "Id", "Name", "Description", "CreatedBy", "CreatedTime", "LastUpdatedBy", "LastUpdatedTime", "DeletedBy", "DeletedTime" },
                values: new object[,]
                {
                    { defaultDocumentTypeId, "General Document", "Default document type for general documents", "system", currentTime, null, null, null, null },
                    { policyDocumentTypeId, "Policy", "Company policies and guidelines", "system", currentTime, null, null, null, null },
                    { procedureDocumentTypeId, "Procedure", "Standard operating procedures", "system", currentTime, null, null, null, null },
                    { manualDocumentTypeId, "Manual", "User manuals and documentation", "system", currentTime, null, null, null, null },
                    { formDocumentTypeId, "Form", "Forms and templates", "system", currentTime, null, null, null, null }
                });

            // Update existing DocumentFiles to use the default document type
            migrationBuilder.Sql($@"
                UPDATE ""DocumentFiles""
                SET ""DocumentTypeId"" = '{defaultDocumentTypeId}'
                WHERE ""DocumentTypeId"" IS NULL OR ""DocumentTypeId"" = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentFiles_DocumentTypes_DocumentTypeId",
                table: "DocumentFiles");

            migrationBuilder.DropTable(
                name: "DocumentTypes");

            migrationBuilder.DropIndex(
                name: "IX_DocumentFiles_DocumentTypeId",
                table: "DocumentFiles");

            migrationBuilder.DropColumn(
                name: "DocumentTypeId",
                table: "DocumentFiles");

            // Note: We don't restore the DocumentTypeId values in DocumentFiles as this would break referential integrity
            // The Down migration assumes that the DocumentTypeId foreign key constraint will be removed
        }
    }
}
