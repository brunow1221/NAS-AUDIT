using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1825, CA1861

namespace BCMetals.NasAudit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5Operations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "nas_audit",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                schema: "nas_audit",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastLoginAtUtc",
                schema: "nas_audit",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastPasswordChangedAtUtc",
                schema: "nas_audit",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                schema: "nas_audit",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "maintenance_runs",
                schema: "nas_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AffectedRows = table.Column<long>(type: "bigint", nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_collector_heartbeats_ObservedAtUtc",
                schema: "nas_audit",
                table: "collector_heartbeats",
                column: "ObservedAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_DataMode_EventTimestampUtc",
                schema: "nas_audit",
                table: "audit_events",
                columns: new[] { "DataMode", "EventTimestampUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_DataMode_InterpretedAction_EventTimestampUtc",
                schema: "nas_audit",
                table: "audit_events",
                columns: new[] { "DataMode", "InterpretedAction", "EventTimestampUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_DataMode_SmbUser_EventTimestampUtc",
                schema: "nas_audit",
                table: "audit_events",
                columns: new[] { "DataMode", "SmbUser", "EventTimestampUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_IsEnabled",
                schema: "nas_audit",
                table: "AspNetUsers",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_runs_Kind_StartedAtUtc",
                schema: "nas_audit",
                table: "maintenance_runs",
                columns: new[] { "Kind", "StartedAtUtc" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "maintenance_runs",
                schema: "nas_audit");

            migrationBuilder.DropIndex(
                name: "IX_collector_heartbeats_ObservedAtUtc",
                schema: "nas_audit",
                table: "collector_heartbeats");

            migrationBuilder.DropIndex(
                name: "IX_audit_events_DataMode_EventTimestampUtc",
                schema: "nas_audit",
                table: "audit_events");

            migrationBuilder.DropIndex(
                name: "IX_audit_events_DataMode_InterpretedAction_EventTimestampUtc",
                schema: "nas_audit",
                table: "audit_events");

            migrationBuilder.DropIndex(
                name: "IX_audit_events_DataMode_SmbUser_EventTimestampUtc",
                schema: "nas_audit",
                table: "audit_events");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_IsEnabled",
                schema: "nas_audit",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                schema: "nas_audit",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastLoginAtUtc",
                schema: "nas_audit",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastPasswordChangedAtUtc",
                schema: "nas_audit",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                schema: "nas_audit",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "nas_audit",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);
        }
    }
}
