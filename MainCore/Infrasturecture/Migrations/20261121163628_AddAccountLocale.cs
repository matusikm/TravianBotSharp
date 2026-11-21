using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MainCore.Infrasturecture.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountLocale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "Accounts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "Accounts");
        }
    }
}
