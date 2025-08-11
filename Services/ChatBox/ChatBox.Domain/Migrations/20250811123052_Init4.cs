using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatBox.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Init4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "AIConfigurations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AIConfigurations",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
