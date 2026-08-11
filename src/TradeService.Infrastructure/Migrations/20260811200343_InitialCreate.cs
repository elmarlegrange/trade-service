using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "FxRates",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pair = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    RateDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FxRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prices",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Isin = table.Column<string>(type: "char(12)", nullable: false),
                    PriceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "USD")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Isin = table.Column<string>(type: "char(12)", nullable: false),
                    Side = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AsOf = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_FxRates_Pair_Date",
                schema: "dbo",
                table: "FxRates",
                columns: new[] { "Pair", "RateDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Prices_Isin_Date",
                schema: "dbo",
                table: "Prices",
                columns: new[] { "Isin", "PriceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trades_AccountId_TradeDate",
                schema: "dbo",
                table: "Trades",
                columns: new[] { "AccountId", "TradeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Trades_ExternalRef_AsOf",
                schema: "dbo",
                table: "Trades",
                columns: new[] { "ExternalRef", "AsOf" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FxRates",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Prices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Trades",
                schema: "dbo");
        }
    }
}
