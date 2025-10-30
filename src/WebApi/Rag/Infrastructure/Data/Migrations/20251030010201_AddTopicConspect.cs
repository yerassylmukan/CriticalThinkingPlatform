using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApi.Rag.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTopicConspect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Conspect",
                table: "Topics",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Conspect",
                table: "Topics");
        }
    }
}
