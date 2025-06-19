using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DOAN_LAPTRINHWEB.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDefaultToAddress1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_default",
                table: "Addresses",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_default",
                table: "Addresses");
        }
    }
}
