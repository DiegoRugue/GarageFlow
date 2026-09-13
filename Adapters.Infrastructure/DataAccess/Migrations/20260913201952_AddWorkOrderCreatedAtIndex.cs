using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarageFlow.Adapters.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_CreatedAt",
                table: "WorkOrders",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_CreatedAt",
                table: "WorkOrders");
        }
    }
}
