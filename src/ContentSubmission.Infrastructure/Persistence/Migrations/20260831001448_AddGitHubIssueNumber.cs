using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentSubmission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubIssueNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GitHubIssueNumber",
                table: "Submissions",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GitHubIssueNumber",
                table: "Submissions");
        }
    }
}
