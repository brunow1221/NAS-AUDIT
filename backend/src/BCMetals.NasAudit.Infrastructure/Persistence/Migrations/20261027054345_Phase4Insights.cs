using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace BCMetals.NasAudit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4Insights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_audit_events_DestinationPath",
                schema: "nas_audit",
                table: "audit_events",
                column: "DestinationPath")
                .Annotation("Npgsql:IndexMethod", "hash");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_FileExtension_EventTimestampUtc",
                schema: "nas_audit",
                table: "audit_events",
                columns: new[] { "FileExtension", "EventTimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_FilePath",
                schema: "nas_audit",
                table: "audit_events",
                column: "FilePath")
                .Annotation("Npgsql:IndexMethod", "hash");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_SourcePath",
                schema: "nas_audit",
                table: "audit_events",
                column: "SourcePath")
                .Annotation("Npgsql:IndexMethod", "hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_events_DestinationPath",
                schema: "nas_audit",
                table: "audit_events");

            migrationBuilder.DropIndex(
                name: "IX_audit_events_FileExtension_EventTimestampUtc",
                schema: "nas_audit",
                table: "audit_events");

            migrationBuilder.DropIndex(
                name: "IX_audit_events_FilePath",
                schema: "nas_audit",
                table: "audit_events");

            migrationBuilder.DropIndex(
                name: "IX_audit_events_SourcePath",
                schema: "nas_audit",
                table: "audit_events");
        }
    }
}
