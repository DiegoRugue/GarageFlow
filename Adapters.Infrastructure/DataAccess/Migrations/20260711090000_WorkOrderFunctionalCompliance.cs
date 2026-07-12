using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarageFlow.Adapters.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrderFunctionalCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "WorkOrders" SET "Status" = 'Received' WHERE "Status" = 'Created';
                UPDATE "WorkOrders" SET "Status" = 'InProgress' WHERE "Status" = 'Approved';
                """);

            migrationBuilder.CreateTable(
                name: "WorkOrderIntakeRequests",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderIntakeRequests", x => x.RequestId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderIntakeRequests_WorkOrderId",
                table: "WorkOrderIntakeRequests",
                column: "WorkOrderId");

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "VehicleBrands"
                        GROUP BY upper("Name")
                        HAVING count(*) > 1)
                    THEN
                        RAISE EXCEPTION 'Cannot enforce case-insensitive duplicate protection: VehicleBrands contains case-insensitive duplicate names.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "VehicleColors"
                        GROUP BY upper("Name")
                        HAVING count(*) > 1)
                    THEN
                        RAISE EXCEPTION 'Cannot enforce case-insensitive duplicate protection: VehicleColors contains case-insensitive duplicate names.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "VehicleModels"
                        GROUP BY "VehicleBrandId", upper("Name")
                        HAVING count(*) > 1)
                    THEN
                        RAISE EXCEPTION 'Cannot enforce case-insensitive duplicate protection: VehicleModels contains case-insensitive duplicate names within a brand.';
                    END IF;
                END $$;

                CREATE UNIQUE INDEX "UX_VehicleBrands_Name_CaseInsensitive"
                    ON "VehicleBrands" (upper("Name"));
                CREATE UNIQUE INDEX "UX_VehicleColors_Name_CaseInsensitive"
                    ON "VehicleColors" (upper("Name"));
                CREATE UNIQUE INDEX "UX_VehicleModels_Brand_Name_CaseInsensitive"
                    ON "VehicleModels" ("VehicleBrandId", upper("Name"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "UX_VehicleBrands_Name_CaseInsensitive";
                DROP INDEX IF EXISTS "UX_VehicleColors_Name_CaseInsensitive";
                DROP INDEX IF EXISTS "UX_VehicleModels_Brand_Name_CaseInsensitive";
                UPDATE "WorkOrders" SET "Status" = 'Created' WHERE "Status" = 'Received';
                """);

            migrationBuilder.DropTable(
                name: "WorkOrderIntakeRequests");
        }
    }
}
