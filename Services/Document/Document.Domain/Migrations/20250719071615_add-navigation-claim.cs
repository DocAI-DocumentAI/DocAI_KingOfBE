using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document.Domain.Migrations
{
    /// <inheritdoc />
    public partial class addnavigationclaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ApprovalClaims",
                table: "ApprovalClaims");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalClaims_DocumentVersionId",
                table: "ApprovalClaims");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ApprovalClaims");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ApprovalClaims");

            migrationBuilder.DropColumn(
                name: "DeletedTime",
                table: "ApprovalClaims");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalClaimDocumentVersionId",
                table: "DocumentVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApprovalClaims",
                table: "ApprovalClaims",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_ApprovalClaimDocumentVersionId",
                table: "DocumentVersions",
                column: "ApprovalClaimDocumentVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentVersions_ApprovalClaims_ApprovalClaimDocumentVersio~",
                table: "DocumentVersions",
                column: "ApprovalClaimDocumentVersionId",
                principalTable: "ApprovalClaims",
                principalColumn: "DocumentVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentVersions_ApprovalClaims_ApprovalClaimDocumentVersio~",
                table: "DocumentVersions");

            migrationBuilder.DropIndex(
                name: "IX_DocumentVersions_ApprovalClaimDocumentVersionId",
                table: "DocumentVersions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApprovalClaims",
                table: "ApprovalClaims");

            migrationBuilder.DropColumn(
                name: "ApprovalClaimDocumentVersionId",
                table: "DocumentVersions");

            migrationBuilder.AddColumn<string>(
                name: "Id",
                table: "ApprovalClaims",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "ApprovalClaims",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedTime",
                table: "ApprovalClaims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApprovalClaims",
                table: "ApprovalClaims",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalClaims_DocumentVersionId",
                table: "ApprovalClaims",
                column: "DocumentVersionId");
        }
    }
}
