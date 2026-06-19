using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RotationsPlus.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRefund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderRefundId",
                schema: "payments",
                table: "payments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderRefundId",
                schema: "payments",
                table: "payments");
        }
    }
}
