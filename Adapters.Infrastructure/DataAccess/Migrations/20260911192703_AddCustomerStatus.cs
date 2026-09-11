using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarageFlow.Adapters.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Customers",
                type: "character varying(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Customers_Status",
                table: "Customers",
                sql: "\"Status\" IN ('Active', 'Suspended')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Customers_Status",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Customers");
        }
    }
}
