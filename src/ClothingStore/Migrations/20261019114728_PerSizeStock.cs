using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClothingStore.Migrations
{
    /// <inheritdoc />
    public partial class PerSizeStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductSizes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Size = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSizes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSizes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSizes_ProductId_Size",
                table: "ProductSizes",
                columns: new[] { "ProductId", "Size" },
                unique: true);

            // Move existing stock into per-size rows: split each product's total stock
            // evenly across its old comma-separated sizes (extra units go to the first sizes).
            migrationBuilder.Sql(@"
                WITH RECURSIVE split(pid, stock, cnt, size, rest, idx) AS (
                    SELECT Id, Stock, length(Sizes) - length(replace(Sizes, ',', '')) + 1, '', Sizes || ',', 0
                    FROM Products WHERE Sizes IS NOT NULL AND trim(Sizes) <> ''
                    UNION ALL
                    SELECT pid, stock, cnt, trim(substr(rest, 1, instr(rest, ',') - 1)), substr(rest, instr(rest, ',') + 1), idx + 1
                    FROM split WHERE rest <> ''
                )
                INSERT OR IGNORE INTO ProductSizes (ProductId, Size, Quantity, SortOrder)
                SELECT pid, size, stock / cnt + CASE WHEN idx - 1 < stock % cnt THEN 1 ELSE 0 END, idx - 1
                FROM split WHERE idx > 0 AND size <> '';");

            // Products that had no sizes get a single 'One Size' row with all their stock.
            migrationBuilder.Sql(@"
                INSERT INTO ProductSizes (ProductId, Size, Quantity, SortOrder)
                SELECT Id, 'One Size', Stock, 0 FROM Products
                WHERE Id NOT IN (SELECT ProductId FROM ProductSizes);");

            // Product totals now always equal the sum of their sizes.
            migrationBuilder.Sql(@"
                UPDATE Products SET Stock = (SELECT COALESCE(SUM(Quantity), 0) FROM ProductSizes WHERE ProductId = Products.Id);");

            migrationBuilder.DropColumn(
                name: "Sizes",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductSizes");

            migrationBuilder.AddColumn<string>(
                name: "Sizes",
                table: "Products",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
