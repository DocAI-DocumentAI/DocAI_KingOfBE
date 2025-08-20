using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatBox.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Init9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplyToNewChats",
                table: "UserPreferences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ApplyToNewChats",
                table: "UserPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
