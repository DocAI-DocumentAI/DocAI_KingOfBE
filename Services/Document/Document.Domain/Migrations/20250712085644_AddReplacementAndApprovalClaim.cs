using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddReplacementAndApprovalClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSubmitted",
                table: "DocumentVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedBy",
                table: "DocumentVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReplaced",
                table: "DocumentFiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReplacementId",
                table: "DocumentFiles",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApprovalClaims",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DocumentVersionId = table.Column<string>(type: "text", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClaimedBy = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_ApprovalClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalClaims_DocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalClaims_DocumentVersionId",
                table: "ApprovalClaims",
                column: "DocumentVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalClaims");

            migrationBuilder.DropColumn(
                name: "LastSubmitted",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "SubmittedBy",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "IsReplaced",
                table: "DocumentFiles");

            migrationBuilder.DropColumn(
                name: "ReplacementId",
                table: "DocumentFiles");
        }
    }
}
