using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Leases_DateRange",
                table: "Leases",
                sql: "[StartDate] < [EndDate]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Leases_DueDayOfMonth",
                table: "Leases",
                sql: "[DueDayOfMonth] BETWEEN 1 AND 28");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Leases_RentAmount",
                table: "Leases",
                sql: "[RentAmount] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Leases_DateRange",
                table: "Leases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Leases_DueDayOfMonth",
                table: "Leases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Leases_RentAmount",
                table: "Leases");
        }
    }
}
