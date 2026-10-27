using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable
#pragma warning disable CA1825, CA1861 // Código generado por EF Core.

namespace BCMetals.NasAudit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "nas_audit");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "nas_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "nas_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "event_sources",
                schema: "nas_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "import_jobs",
                schema: "nas_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RowsProcessed = table.Column<int>(type: "integer", nullable: false),
                    RowsInserted = table.Column<int>(type: "integer", nullable: false),
                    RowsDuplicate = table.Column<int>(type: "integer", nullable: false),
                    RowsRejected = table.Column<int>(type: "integer", nullable: false),
                    Warnings = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "nas_audit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "nas_audit",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "nas_audit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "nas_audit",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "nas_audit",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "nas_audit",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "nas_audit",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "nas_audit",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "nas_audit",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "nas_audit",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "nas_audit",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "nas_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventTimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SmbUser = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ClientIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    InterpretedAction = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    InterpretationConfidence = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OperationResult = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ShareName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FilePath = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    SourcePath = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    DestinationPath = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    FileExtension = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ObjectType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IsAuxiliaryStream = table.Column<bool>(type: "boolean", nullable: false),
                    IsRecyclePath = table.Column<bool>(type: "boolean", nullable: false),
                    RawMessage = table.Column<string>(type: "text", nullable: false),
                    EventSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalCursor = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    BootId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DeduplicationKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ParserVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DataMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_events_event_sources_EventSourceId",
                        column: x => x.EventSourceId,
                        principalSchema: "nas_audit",
                        principalTable: "event_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "nas_audit",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "nas_audit",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "nas_audit",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "nas_audit",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "nas_audit",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "nas_audit",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "nas_audit",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_DeduplicationKey",
                schema: "nas_audit",
                table: "audit_events",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_EventSourceId",
                schema: "nas_audit",
                table: "audit_events",
                column: "EventSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_EventTimestampUtc",
                schema: "nas_audit",
                table: "audit_events",
                column: "EventTimestampUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_InterpretedAction_EventTimestampUtc",
                schema: "nas_audit",
                table: "audit_events",
                columns: new[] { "InterpretedAction", "EventTimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_ShareName_EventTimestampUtc",
                schema: "nas_audit",
                table: "audit_events",
                columns: new[] { "ShareName", "EventTimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_SmbUser_EventTimestampUtc",
                schema: "nas_audit",
                table: "audit_events",
                columns: new[] { "SmbUser", "EventTimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_event_sources_Name",
                schema: "nas_audit",
                table: "event_sources",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_jobs_ContentHash",
                schema: "nas_audit",
                table: "import_jobs",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_import_jobs_StartedAtUtc",
                schema: "nas_audit",
                table: "import_jobs",
                column: "StartedAtUtc",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "nas_audit");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "nas_audit");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "nas_audit");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "nas_audit");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "nas_audit");

            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "nas_audit");

            migrationBuilder.DropTable(
                name: "import_jobs",
                schema: "nas_audit");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "nas_audit");

            migrationBuilder.DropTable(
                name: "AspNetUsers",
                schema: "nas_audit");

            migrationBuilder.DropTable(
                name: "event_sources",
                schema: "nas_audit");
        }
    }
}
