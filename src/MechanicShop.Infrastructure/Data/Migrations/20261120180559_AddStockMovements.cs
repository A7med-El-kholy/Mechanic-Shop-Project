using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MechanicShop.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Delta = table.Column<int>(type: "int", nullable: false),
                    QuantityBefore = table.Column<int>(type: "int", nullable: false),
                    QuantityAfter = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                    table.ForeignKey(
                        name: "FK_StockMovements_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PartId_RecordedAt",
                table: "StockMovements",
                columns: new[] { "PartId", "RecordedAt" });

            migrationBuilder.Sql("""
                INSERT INTO [StockMovements] ([Id], [PartId], [Delta], [QuantityBefore], [QuantityAfter], [Reason], [ActorUserId], [RecordedAt])
                SELECT NEWID(), [Id], [Quantity], 0, [Quantity], N'Opening balance (migration)', NULL, SYSDATETIMEOFFSET()
                FROM [Parts]
                WHERE [Quantity] > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockMovements");
        }
    }
}
