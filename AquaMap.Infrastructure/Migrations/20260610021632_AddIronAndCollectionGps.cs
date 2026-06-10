using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaMap.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIronAndCollectionGps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CollectionLatitude",
                table: "WaterAnalyses",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CollectionLongitude",
                table: "WaterAnalyses",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Iron",
                table: "WaterAnalyses",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPendingSync",
                table: "WaterAnalyses",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CollectionLatitude",
                table: "WaterAnalyses");

            migrationBuilder.DropColumn(
                name: "CollectionLongitude",
                table: "WaterAnalyses");

            migrationBuilder.DropColumn(
                name: "Iron",
                table: "WaterAnalyses");

            migrationBuilder.DropColumn(
                name: "IsPendingSync",
                table: "WaterAnalyses");
        }
    }
}
