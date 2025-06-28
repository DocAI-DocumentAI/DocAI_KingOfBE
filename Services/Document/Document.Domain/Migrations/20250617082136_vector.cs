using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document.Domain.Migrations
{
    /// <inheritdoc />
    public partial class vector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentChunks");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            //migrationBuilder.AlterColumn<int>(
            //    name: "Status",
            //    table: "DocumentFiles",
            //    type: "integer",
            //    nullable: false,
            //    oldClrType: typeof(string),
            //    oldType: "text");

            migrationBuilder.Sql(@"
    ALTER TABLE ""DocumentFiles""
    ALTER COLUMN ""Status"" TYPE integer USING ""Status""::integer;
");


            migrationBuilder.AddColumn<string>(
                name: "DepartmentId",
                table: "DocumentFiles",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "DocumentFiles");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DocumentFiles",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateTable(
                name: "DocumentChunks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DocumentId = table.Column<string>(type: "text", nullable: false),
                    ChunkOrder = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentChunks_DocumentFiles_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "DocumentFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentChunks_DocumentId",
                table: "DocumentChunks",
                column: "DocumentId");
        }
    }
}
