using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarageFlow.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrdersModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderEstimates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderEstimates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderEstimates_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderEstimateInventoryLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimateId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    DescriptionSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderEstimateInventoryLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderEstimateInventoryLines_WorkOrderEstimates_Estimate~",
                        column: x => x.EstimateId,
                        principalTable: "WorkOrderEstimates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderEstimateServiceLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DescriptionSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderEstimateServiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderEstimateServiceLines_WorkOrderEstimates_EstimateId",
                        column: x => x.EstimateId,
                        principalTable: "WorkOrderEstimates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_CustomerId",
                table: "Users",
                column: "CustomerId",
                unique: true,
                filter: "\"CustomerId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderEstimateInventoryLines_EstimateId",
                table: "WorkOrderEstimateInventoryLines",
                column: "EstimateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderEstimates_WorkOrderId_Status",
                table: "WorkOrderEstimates",
                columns: new[] { "WorkOrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderEstimateServiceLines_EstimateId",
                table: "WorkOrderEstimateServiceLines",
                column: "EstimateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_CustomerId",
                table: "WorkOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_VehicleId",
                table: "WorkOrders",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Customers_CustomerId",
                table: "Users",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Customers_CustomerId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "WorkOrderEstimateInventoryLines");

            migrationBuilder.DropTable(
                name: "WorkOrderEstimateServiceLines");

            migrationBuilder.DropTable(
                name: "WorkOrderEstimates");

            migrationBuilder.DropTable(
                name: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_Users_CustomerId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Users");
        }
    }
}
