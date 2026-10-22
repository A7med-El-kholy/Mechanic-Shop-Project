using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MechanicShop.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeesAndWorkOrderScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ScheduledDate",
                table: "WorkOrders",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<int>(
                name: "Spot",
                table: "WorkOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartsAtMinute",
                table: "WorkOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianId",
                table: "WorkOrders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_ScheduledDate",
                table: "WorkOrders",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_TechnicianId",
                table: "WorkOrders",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_FullName",
                table: "Employees",
                column: "FullName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Employees_TechnicianId",
                table: "WorkOrders",
                column: "TechnicianId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Employees_TechnicianId",
                table: "WorkOrders");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_ScheduledDate",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_TechnicianId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "ScheduledDate",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "Spot",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "StartsAtMinute",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "TechnicianId",
                table: "WorkOrders");
        }
    }
}
