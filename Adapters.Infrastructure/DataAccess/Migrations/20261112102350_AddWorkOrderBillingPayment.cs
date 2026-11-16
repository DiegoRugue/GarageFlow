using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarageFlow.Adapters.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderBillingPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BillingPaymentRevision",
                table: "WorkOrders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutUrl",
                table: "WorkOrders",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentConfirmedAt",
                table: "WorkOrders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingPaymentRevision",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "CheckoutUrl",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "PaymentConfirmedAt",
                table: "WorkOrders");
        }
    }
}
