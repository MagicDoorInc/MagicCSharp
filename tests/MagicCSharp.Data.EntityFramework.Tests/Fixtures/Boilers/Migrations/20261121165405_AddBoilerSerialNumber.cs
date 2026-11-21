using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures.Boilers.Migrations
{
    /// <inheritdoc />
    public partial class AddBoilerSerialNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "serial_number",
                table: "boilers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "serial_number",
                table: "boilers");
        }
    }
}
