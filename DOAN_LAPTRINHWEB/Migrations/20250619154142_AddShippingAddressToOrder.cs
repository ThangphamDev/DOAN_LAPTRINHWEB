using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DOAN_LAPTRINHWEB.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingAddressToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "shipping_address_id",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "Addresses",
                type: "datetime",
                nullable: false,
                defaultValueSql: "(getdate())");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "Addresses",
                type: "datetime",
                nullable: false,
                defaultValueSql: "(getdate())");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_shipping_address_id",
                table: "Orders",
                column: "shipping_address_id");

            migrationBuilder.AddForeignKey(
                name: "FK__Orders__shipping_address_id",
                table: "Orders",
                column: "shipping_address_id",
                principalTable: "Addresses",
                principalColumn: "address_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK__Orders__shipping_address_id",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_shipping_address_id",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "shipping_address_id",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "Addresses");
        }
    }
}
