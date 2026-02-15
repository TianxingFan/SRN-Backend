using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTxHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TxHash",
                table: "Artifacts",
                type: "varchar(66)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TxHash",
                table: "Artifacts");
        }
    }
}
