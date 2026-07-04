using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarageFlow.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class EstimateServiceLineExecution : Migration
    {
        private static readonly string[] ServiceLineServiceIdCompletedAtIndexColumns = ["ServiceId", "CompletedAt"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "WorkOrderEstimateServiceLines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "WorkOrderEstimateServiceLines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "WorkOrderEstimateServiceLines",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderEstimateServiceLines_CompletedAt",
                table: "WorkOrderEstimateServiceLines",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderEstimateServiceLines_ServiceId",
                table: "WorkOrderEstimateServiceLines",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderEstimateServiceLines_ServiceId_CompletedAt",
                table: "WorkOrderEstimateServiceLines",
                columns: ServiceLineServiceIdCompletedAtIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkOrderEstimateServiceLines_CompletedAt",
                table: "WorkOrderEstimateServiceLines");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrderEstimateServiceLines_ServiceId",
                table: "WorkOrderEstimateServiceLines");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrderEstimateServiceLines_ServiceId_CompletedAt",
                table: "WorkOrderEstimateServiceLines");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "WorkOrderEstimateServiceLines");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "WorkOrderEstimateServiceLines");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "WorkOrderEstimateServiceLines");
        }
    }
}
