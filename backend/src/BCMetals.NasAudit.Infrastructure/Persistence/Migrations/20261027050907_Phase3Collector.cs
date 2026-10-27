using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1825, CA1861

namespace BCMetals.NasAudit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3Collector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collector_checkpoints",
                schema: "nas_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cursor = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    BootId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastEventUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collector_checkpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_collector_checkpoints_event_sources_EventSourceId",
                        column: x => x.EventSourceId,
                        principalSchema: "nas_audit",
                        principalTable: "event_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "collector_heartbeats",
                schema: "nas_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastEventUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    QueueDepth = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collector_heartbeats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_batches",
                schema: "nas_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Accepted = table.Column<int>(type: "integer", nullable: false),
                    Duplicates = table.Column<int>(type: "integer", nullable: false),
                    Rejected = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedCursor = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rejected_ingestion_events",
                schema: "nas_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cursor = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    BootId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    EventTimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RawMessage = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false),
                    DeduplicationKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rejected_ingestion_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_collector_checkpoints_CollectorId",
                schema: "nas_audit",
                table: "collector_checkpoints",
                column: "CollectorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_collector_checkpoints_EventSourceId",
                schema: "nas_audit",
                table: "collector_checkpoints",
                column: "EventSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_collector_heartbeats_CollectorId",
                schema: "nas_audit",
                table: "collector_heartbeats",
                column: "CollectorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_batches_CollectorId_BatchId",
                schema: "nas_audit",
                table: "ingestion_batches",
                columns: new[] { "CollectorId", "BatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_batches_ReceivedAtUtc",
                schema: "nas_audit",
                table: "ingestion_batches",
                column: "ReceivedAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_rejected_ingestion_events_DeduplicationKey",
                schema: "nas_audit",
                table: "rejected_ingestion_events",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rejected_ingestion_events_ReceivedAtUtc",
                schema: "nas_audit",
                table: "rejected_ingestion_events",
                column: "ReceivedAtUtc",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collector_checkpoints",
                schema: "nas_audit");

            migrationBuilder.DropTable(
                name: "collector_heartbeats",
                schema: "nas_audit");

            migrationBuilder.DropTable(
                name: "ingestion_batches",
                schema: "nas_audit");

            migrationBuilder.DropTable(
                name: "rejected_ingestion_events",
                schema: "nas_audit");
        }
    }
}
