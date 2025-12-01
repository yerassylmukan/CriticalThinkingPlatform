using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApi.Rag.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherIdToTopic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TeacherId",
                table: "Topics",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "Topics");
        }
    }
}
