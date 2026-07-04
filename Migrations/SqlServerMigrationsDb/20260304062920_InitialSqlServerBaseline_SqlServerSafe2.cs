using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BlazorApp.Migrations.SqlServerMigrationsDb
{
    /// <inheritdoc />
    public partial class InitialSqlServerBaseline_SqlServerSafe2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdminUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AdminEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TargetUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TargetEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiActionHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModelId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiActionHistoryEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExportPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsGlobalDefault = table.Column<bool>(type: "bit", nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportPresets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExportTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PresetKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PageWidthMm = table.Column<int>(type: "int", nullable: false),
                    PageHeightMm = table.Column<int>(type: "int", nullable: false),
                    MarginTopMm = table.Column<int>(type: "int", nullable: false),
                    MarginRightMm = table.Column<int>(type: "int", nullable: false),
                    MarginBottomMm = table.Column<int>(type: "int", nullable: false),
                    MarginLeftMm = table.Column<int>(type: "int", nullable: false),
                    FontFamily = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyFontSizePt = table.Column<int>(type: "int", nullable: false),
                    LineHeight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ParagraphSpacingPt = table.Column<int>(type: "int", nullable: false),
                    HeaderEnabled = table.Column<bool>(type: "bit", nullable: false),
                    HeaderLeft = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeaderCenter = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeaderRight = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FooterEnabled = table.Column<bool>(type: "bit", nullable: false),
                    FooterLeft = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FooterCenter = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FooterRight = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PageNumbersEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PageNumberStart = table.Column<int>(type: "int", nullable: false),
                    TocEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TocDepth = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutlineTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TemplateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutlineTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.PlanId);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Subtitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Genre = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultExportSettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromptPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BuiltinActionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TemplateText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptPresets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchIndexEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DocumentId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProjectId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SectionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PageId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchIndexEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StripeEventLogs",
                columns: table => new
                {
                    StripeEventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReceivedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StripeEventLogs", x => x.StripeEventId);
                });

            migrationBuilder.CreateTable(
                name: "TokenAdjustments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DeltaTokens = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdjustedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdjustedByEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenAdjustments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsageAggregates",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TotalInputTokens = table.Column<int>(type: "int", nullable: false),
                    TotalOutputTokens = table.Column<int>(type: "int", nullable: false),
                    TotalCostMicros = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageAggregates", x => new { x.UserId, x.PeriodStartUtc, x.PeriodEndUtc, x.Kind });
                });

            migrationBuilder.CreateTable(
                name: "UsageEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    CostMicros = table.Column<long>(type: "bigint", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserEntitlements",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PlanKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubscriptionStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AiMonthlyTokenBudget = table.Column<int>(type: "int", nullable: false),
                    AiTokensUsedThisPeriod = table.Column<int>(type: "int", nullable: false),
                    PeriodStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StripeCustomerId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StripeSubscriptionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StripePriceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentPeriodEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelAtPeriodEnd = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEntitlements", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "UserEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HasOnboarded = table.Column<bool>(type: "bit", nullable: false),
                    HasCompletedOnboarding = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OnboardingStep = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    OnboardingStartedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OnboardingCompletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PrimaryWritingIntent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "AiActionAppliedEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HistoryEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AppliedToPageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppliedToSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppliedToDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BeforeContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UndoneAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiActionAppliedEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiActionAppliedEvents_AiActionHistoryEntries_HistoryEntryId",
                        column: x => x.HistoryEntryId,
                        principalTable: "AiActionHistoryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanEntitlements",
                columns: table => new
                {
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanEntitlements", x => new { x.PlanId, x.Key });
                    table.ForeignKey(
                        name: "FK_PlanEntitlements_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "PlanId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserPlanAssignments",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPlanAssignments", x => new { x.UserId, x.PlanId });
                    table.ForeignKey(
                        name: "FK_UserPlanAssignments_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "PlanId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentKind = table.Column<int>(type: "int", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TranslationGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUnixSeconds = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUnixSeconds = table.Column<long>(type: "bigint", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectGoals",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DailyTargetWords = table.Column<int>(type: "int", nullable: false),
                    WeeklyTargetWords = table.Column<int>(type: "int", nullable: false),
                    Timezone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectGoals", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_ProjectGoals_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMilestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetWords = table.Column<int>(type: "int", nullable: true),
                    TargetNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMilestones_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectProgressDaily",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    WordsDelta = table.Column<int>(type: "int", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectProgressDaily", x => new { x.ProjectId, x.Date });
                    table.ForeignKey(
                        name: "FK_ProjectProgressDaily_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectProgressEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Date = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    WordsDelta = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectProgressEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectProgressEvents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WritingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    WordsDelta = table.Column<int>(type: "int", nullable: false),
                    StartWordCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WritingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WritingSessions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BibleSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BibleType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastRefreshUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastRefreshSourceHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastRefreshStatsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastRefreshCursorJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BibleSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BibleSnapshots_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentGlossaryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Term = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NormalizedTerm = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentGlossaryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentGlossaryEntries_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentOutlines",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Outline = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentOutlines", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_DocumentOutlines_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentSynopses",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Logline = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Premise = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Theme = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProtagonistArc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CentralConflict = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stakes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Setting = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EndingIntent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OpenQuestions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSynopses", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_DocumentSynopses_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectExportSettings",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DefaultPresetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OverridesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectExportSettings", x => new { x.DocumentId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ProjectExportSettings_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectExportSettings_ExportPresets_DefaultPresetId",
                        column: x => x.DefaultPresetId,
                        principalTable: "ExportPresets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NarrativePurpose = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TranslationGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sections_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentOutlineNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LinkedSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentOutlineNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentOutlineNodes_DocumentOutlineNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DocumentOutlineNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentOutlineNodes_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentOutlineNodes_Sections_LinkedSectionId",
                        column: x => x.LinkedSectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pages_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pages_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NodeType = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    LinkedSectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WordCountCache = table.Column<int>(type: "int", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectNodes_ProjectNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ProjectNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectNodes_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectNodes_Sections_LinkedSectionId",
                        column: x => x.LinkedSectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SectionNotes",
                columns: table => new
                {
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotesText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionNotes", x => x.SectionId);
                    table.ForeignKey(
                        name: "FK_SectionNotes_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SectionSceneCards",
                columns: table => new
                {
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NarrativePurpose = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NarrativeIntent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NarrativeRole = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmotionalBeat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KeyEvents = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OpenQuestions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PovCharacterId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlaceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimelineEventId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeRef = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TagsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferencesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionSceneCards", x => x.SectionId);
                    table.ForeignKey(
                        name: "FK_SectionSceneCards_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PageAnnotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AnchorFrom = table.Column<int>(type: "int", nullable: false),
                    AnchorTo = table.Column<int>(type: "int", nullable: false),
                    AnchorText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageAnnotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageAnnotations_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PageAnnotations_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PageNotes",
                columns: table => new
                {
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageNotes", x => x.PageId);
                    table.ForeignKey(
                        name: "FK_PageNotes_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PageQualityIssueDismissals",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssueKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DismissedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageQualityIssueDismissals", x => new { x.UserId, x.PageId, x.IssueKey });
                    table.ForeignKey(
                        name: "FK_PageQualityIssueDismissals_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PageQualityIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IssueKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RuleId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Suggestion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnchorText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartOffset = table.Column<int>(type: "int", nullable: false),
                    EndOffset = table.Column<int>(type: "int", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageQualityIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageQualityIssues_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PageQualityIssues_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PageVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentCompressed = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ContentTextHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SizeBytes = table.Column<int>(type: "int", nullable: false),
                    WordCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageVersions_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SceneAnnotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SceneNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AnchorFrom = table.Column<int>(type: "int", nullable: false),
                    AnchorTo = table.Column<int>(type: "int", nullable: false),
                    AnchorText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneAnnotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SceneAnnotations_ProjectNodes_SceneNodeId",
                        column: x => x.SceneNodeId,
                        principalTable: "ProjectNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SceneCards",
                columns: table => new
                {
                    SceneNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NarrativePurpose = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NarrativeIntent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NarrativeRole = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmotionalBeat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KeyEvents = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OpenQuestions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PovCharacterId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlaceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimelineEventId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeRef = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TagsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferencesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneCards", x => x.SceneNodeId);
                    table.ForeignKey(
                        name: "FK_SceneCards_ProjectNodes_SceneNodeId",
                        column: x => x.SceneNodeId,
                        principalTable: "ProjectNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SceneContents",
                columns: table => new
                {
                    SceneNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneContents", x => x.SceneNodeId);
                    table.ForeignKey(
                        name: "FK_SceneContents_ProjectNodes_SceneNodeId",
                        column: x => x.SceneNodeId,
                        principalTable: "ProjectNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SceneNotes",
                columns: table => new
                {
                    SceneNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotesText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneNotes", x => x.SceneNodeId);
                    table.ForeignKey(
                        name: "FK_SceneNotes_ProjectNodes_SceneNodeId",
                        column: x => x.SceneNodeId,
                        principalTable: "ProjectNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SceneQualityIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SceneNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IssueKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RuleId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Suggestion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnchorText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartOffset = table.Column<int>(type: "int", nullable: false),
                    EndOffset = table.Column<int>(type: "int", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneQualityIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SceneQualityIssues_ProjectNodes_SceneNodeId",
                        column: x => x.SceneNodeId,
                        principalTable: "ProjectNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SceneVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SceneNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentCompressed = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ContentTextHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SizeBytes = table.Column<int>(type: "int", nullable: false),
                    WordCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SceneVersions_ProjectNodes_SceneNodeId",
                        column: x => x.SceneNodeId,
                        principalTable: "ProjectNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "PlanId", "IsActive", "Key", "Name" },
                values: new object[,]
                {
                    { new Guid("5f4d2c6f-98fd-4a26-9c0f-0a2a1f2d7c4b"), true, "free", "Free" },
                    { new Guid("6d1d34ef-2a0f-4b24-8b3f-7f3f4a4b9f0b"), true, "professional", "Professional" },
                    { new Guid("83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5"), true, "standard", "Standard" }
                });

            migrationBuilder.InsertData(
                table: "UserProfiles",
                columns: new[] { "UserId", "CreatedUtc", "DisplayName", "HasCompletedOnboarding", "HasOnboarded", "OnboardingCompletedUtc", "OnboardingStartedUtc", "PrimaryWritingIntent", "UpdatedUtc" },
                values: new object[] { "seed-system", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", true, true, null, null, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "PlanEntitlements",
                columns: new[] { "Key", "PlanId", "Value" },
                values: new object[,]
                {
                    { "ai.enabled", new Guid("5f4d2c6f-98fd-4a26-9c0f-0a2a1f2d7c4b"), "false" },
                    { "ai.images.cover", new Guid("5f4d2c6f-98fd-4a26-9c0f-0a2a1f2d7c4b"), "false" },
                    { "ai.monthly_tokens", new Guid("5f4d2c6f-98fd-4a26-9c0f-0a2a1f2d7c4b"), "0" },
                    { "export.pdf", new Guid("5f4d2c6f-98fd-4a26-9c0f-0a2a1f2d7c4b"), "false" },
                    { "history.enabled", new Guid("5f4d2c6f-98fd-4a26-9c0f-0a2a1f2d7c4b"), "true" },
                    { "history.max_versions", new Guid("5f4d2c6f-98fd-4a26-9c0f-0a2a1f2d7c4b"), "5" },
                    { "ai.enabled", new Guid("6d1d34ef-2a0f-4b24-8b3f-7f3f4a4b9f0b"), "true" },
                    { "ai.images.cover", new Guid("6d1d34ef-2a0f-4b24-8b3f-7f3f4a4b9f0b"), "true" },
                    { "ai.monthly_tokens", new Guid("6d1d34ef-2a0f-4b24-8b3f-7f3f4a4b9f0b"), "1000000" },
                    { "export.pdf", new Guid("6d1d34ef-2a0f-4b24-8b3f-7f3f4a4b9f0b"), "true" },
                    { "history.enabled", new Guid("6d1d34ef-2a0f-4b24-8b3f-7f3f4a4b9f0b"), "true" },
                    { "history.retention_days", new Guid("6d1d34ef-2a0f-4b24-8b3f-7f3f4a4b9f0b"), "30" },
                    { "ai.enabled", new Guid("83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5"), "true" },
                    { "ai.images.cover", new Guid("83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5"), "false" },
                    { "ai.monthly_tokens", new Guid("83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5"), "200000" },
                    { "export.pdf", new Guid("83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5"), "true" },
                    { "history.enabled", new Guid("83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5"), "true" },
                    { "history.retention_days", new Guid("83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5"), "30" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditEvents_Action",
                table: "AdminAuditEvents",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditEvents_AdminUserId",
                table: "AdminAuditEvents",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditEvents_OccurredAtUtc",
                table: "AdminAuditEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditEvents_TargetUserId",
                table: "AdminAuditEvents",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionAppliedEvents_AppliedAt",
                table: "AiActionAppliedEvents",
                column: "AppliedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionAppliedEvents_HistoryEntryId",
                table: "AiActionAppliedEvents",
                column: "HistoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionAppliedEvents_OwnerUserId",
                table: "AiActionAppliedEvents",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionAppliedEvents_UndoneAt",
                table: "AiActionAppliedEvents",
                column: "UndoneAt");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionHistoryEntries_ActionKey",
                table: "AiActionHistoryEntries",
                column: "ActionKey");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionHistoryEntries_CreatedAt",
                table: "AiActionHistoryEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionHistoryEntries_DocumentId",
                table: "AiActionHistoryEntries",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionHistoryEntries_OwnerUserId",
                table: "AiActionHistoryEntries",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BibleSnapshots_DocumentId_BibleType",
                table: "BibleSnapshots",
                columns: new[] { "DocumentId", "BibleType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BibleSnapshots_LastRefreshUtc",
                table: "BibleSnapshots",
                column: "LastRefreshUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentGlossaryEntries_DocumentId",
                table: "DocumentGlossaryEntries",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentGlossaryEntries_NormalizedTerm",
                table: "DocumentGlossaryEntries",
                column: "NormalizedTerm");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutlineNodes_DocumentId_ParentId_Order",
                table: "DocumentOutlineNodes",
                columns: new[] { "DocumentId", "ParentId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutlineNodes_LinkedSectionId",
                table: "DocumentOutlineNodes",
                column: "LinkedSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOutlineNodes_ParentId",
                table: "DocumentOutlineNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DeletedAtUtc",
                table: "Documents",
                column: "DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentKind",
                table: "Documents",
                column: "DocumentKind");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_IsArchived",
                table: "Documents",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OwnerUserId_UpdatedAtUnixSeconds",
                table: "Documents",
                columns: new[] { "OwnerUserId", "UpdatedAtUnixSeconds" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId",
                table: "Documents",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId_DocumentKind",
                table: "Documents",
                columns: new[] { "ProjectId", "DocumentKind" },
                unique: true,
                filter: "\"DocumentKind\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId_UpdatedAtUnixSeconds",
                table: "Documents",
                columns: new[] { "ProjectId", "UpdatedAtUnixSeconds" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSynopses_UpdatedAt",
                table: "DocumentSynopses",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExportPresets_OwnerUserId",
                table: "ExportPresets",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportPresets_OwnerUserId_IsGlobalDefault",
                table: "ExportPresets",
                columns: new[] { "OwnerUserId", "IsGlobalDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_ExportPresets_UpdatedAt",
                table: "ExportPresets",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExportTemplates_OwnerUserId",
                table: "ExportTemplates",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportTemplates_OwnerUserId_PresetKey",
                table: "ExportTemplates",
                columns: new[] { "OwnerUserId", "PresetKey" });

            migrationBuilder.CreateIndex(
                name: "IX_OutlineTemplates_OwnerUserId",
                table: "OutlineTemplates",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OutlineTemplates_UpdatedUtc",
                table: "OutlineTemplates",
                column: "UpdatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PageAnnotations_CreatedAt",
                table: "PageAnnotations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PageAnnotations_DocumentId",
                table: "PageAnnotations",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PageAnnotations_Kind",
                table: "PageAnnotations",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_PageAnnotations_PageId",
                table: "PageAnnotations",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_PageAnnotations_Status",
                table: "PageAnnotations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PageQualityIssueDismissals_PageId",
                table: "PageQualityIssueDismissals",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_PageQualityIssues_ContentHash",
                table: "PageQualityIssues",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_PageQualityIssues_DocumentId",
                table: "PageQualityIssues",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PageQualityIssues_IssueKey",
                table: "PageQualityIssues",
                column: "IssueKey");

            migrationBuilder.CreateIndex(
                name: "IX_PageQualityIssues_PageId",
                table: "PageQualityIssues",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_PageQualityIssues_Scope",
                table: "PageQualityIssues",
                column: "Scope");

            migrationBuilder.CreateIndex(
                name: "IX_Pages_DocumentId",
                table: "Pages",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Pages_SectionId_OrderIndex",
                table: "Pages",
                columns: new[] { "SectionId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_PageVersions_CreatedAt",
                table: "PageVersions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PageVersions_DocumentId",
                table: "PageVersions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PageVersions_PageId",
                table: "PageVersions",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Key",
                table: "Plans",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExportSettings_DefaultPresetId",
                table: "ProjectExportSettings",
                column: "DefaultPresetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExportSettings_UserId",
                table: "ProjectExportSettings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestones_ProjectId",
                table: "ProjectMilestones",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestones_Status",
                table: "ProjectMilestones",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectNodes_LinkedSectionId",
                table: "ProjectNodes",
                column: "LinkedSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectNodes_ParentId",
                table: "ProjectNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectNodes_ProjectId_ParentId_OrderIndex",
                table: "ProjectNodes",
                columns: new[] { "ProjectId", "ParentId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectProgressEvents_ProjectId",
                table: "ProjectProgressEvents",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectProgressEvents_ProjectId_EventKey",
                table: "ProjectProgressEvents",
                columns: new[] { "ProjectId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerUserId",
                table: "Projects",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_UpdatedUtc",
                table: "Projects",
                column: "UpdatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PromptPresets_OwnerUserId",
                table: "PromptPresets",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptPresets_OwnerUserId_Kind",
                table: "PromptPresets",
                columns: new[] { "OwnerUserId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptPresets_OwnerUserId_ProjectId",
                table: "PromptPresets",
                columns: new[] { "OwnerUserId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptPresets_UpdatedUtc",
                table: "PromptPresets",
                column: "UpdatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SceneAnnotations_CreatedAt",
                table: "SceneAnnotations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SceneAnnotations_Kind",
                table: "SceneAnnotations",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_SceneAnnotations_SceneNodeId",
                table: "SceneAnnotations",
                column: "SceneNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_SceneAnnotations_Status",
                table: "SceneAnnotations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SceneQualityIssues_ContentHash",
                table: "SceneQualityIssues",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_SceneQualityIssues_IssueKey",
                table: "SceneQualityIssues",
                column: "IssueKey");

            migrationBuilder.CreateIndex(
                name: "IX_SceneQualityIssues_SceneNodeId",
                table: "SceneQualityIssues",
                column: "SceneNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_SceneQualityIssues_Scope",
                table: "SceneQualityIssues",
                column: "Scope");

            migrationBuilder.CreateIndex(
                name: "IX_SceneVersions_CreatedAt",
                table: "SceneVersions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SceneVersions_SceneNodeId",
                table: "SceneVersions",
                column: "SceneNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_SearchIndexEntries_DocumentId",
                table: "SearchIndexEntries",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SearchIndexEntries_EntityType_EntityId_DocumentId",
                table: "SearchIndexEntries",
                columns: new[] { "EntityType", "EntityId", "DocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchIndexEntries_ProjectId",
                table: "SearchIndexEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_DocumentId_OrderIndex",
                table: "Sections",
                columns: new[] { "DocumentId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_StripeEventLogs_ReceivedUtc",
                table: "StripeEventLogs",
                column: "ReceivedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TokenAdjustments_OccurredAtUtc",
                table: "TokenAdjustments",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TokenAdjustments_UserId",
                table: "TokenAdjustments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserEvents_CreatedUtc",
                table: "UserEvents",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UserEvents_EventName",
                table: "UserEvents",
                column: "EventName");

            migrationBuilder.CreateIndex(
                name: "IX_UserEvents_UserId",
                table: "UserEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPlanAssignments_PlanId",
                table: "UserPlanAssignments",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_HasCompletedOnboarding",
                table: "UserProfiles",
                column: "HasCompletedOnboarding");

            migrationBuilder.CreateIndex(
                name: "IX_WritingSessions_ProjectId_StartedUtc",
                table: "WritingSessions",
                columns: new[] { "ProjectId", "StartedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditEvents");

            migrationBuilder.DropTable(
                name: "AiActionAppliedEvents");

            migrationBuilder.DropTable(
                name: "BibleSnapshots");

            migrationBuilder.DropTable(
                name: "DocumentGlossaryEntries");

            migrationBuilder.DropTable(
                name: "DocumentOutlineNodes");

            migrationBuilder.DropTable(
                name: "DocumentOutlines");

            migrationBuilder.DropTable(
                name: "DocumentSynopses");

            migrationBuilder.DropTable(
                name: "ExportTemplates");

            migrationBuilder.DropTable(
                name: "OutlineTemplates");

            migrationBuilder.DropTable(
                name: "PageAnnotations");

            migrationBuilder.DropTable(
                name: "PageNotes");

            migrationBuilder.DropTable(
                name: "PageQualityIssueDismissals");

            migrationBuilder.DropTable(
                name: "PageQualityIssues");

            migrationBuilder.DropTable(
                name: "PageVersions");

            migrationBuilder.DropTable(
                name: "PlanEntitlements");

            migrationBuilder.DropTable(
                name: "ProjectExportSettings");

            migrationBuilder.DropTable(
                name: "ProjectGoals");

            migrationBuilder.DropTable(
                name: "ProjectMilestones");

            migrationBuilder.DropTable(
                name: "ProjectProgressDaily");

            migrationBuilder.DropTable(
                name: "ProjectProgressEvents");

            migrationBuilder.DropTable(
                name: "PromptPresets");

            migrationBuilder.DropTable(
                name: "SceneAnnotations");

            migrationBuilder.DropTable(
                name: "SceneCards");

            migrationBuilder.DropTable(
                name: "SceneContents");

            migrationBuilder.DropTable(
                name: "SceneNotes");

            migrationBuilder.DropTable(
                name: "SceneQualityIssues");

            migrationBuilder.DropTable(
                name: "SceneVersions");

            migrationBuilder.DropTable(
                name: "SearchIndexEntries");

            migrationBuilder.DropTable(
                name: "SectionNotes");

            migrationBuilder.DropTable(
                name: "SectionSceneCards");

            migrationBuilder.DropTable(
                name: "StripeEventLogs");

            migrationBuilder.DropTable(
                name: "TokenAdjustments");

            migrationBuilder.DropTable(
                name: "UsageAggregates");

            migrationBuilder.DropTable(
                name: "UsageEvents");

            migrationBuilder.DropTable(
                name: "UserEntitlements");

            migrationBuilder.DropTable(
                name: "UserEvents");

            migrationBuilder.DropTable(
                name: "UserPlanAssignments");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "WritingSessions");

            migrationBuilder.DropTable(
                name: "AiActionHistoryEntries");

            migrationBuilder.DropTable(
                name: "Pages");

            migrationBuilder.DropTable(
                name: "ExportPresets");

            migrationBuilder.DropTable(
                name: "ProjectNodes");

            migrationBuilder.DropTable(
                name: "Plans");

            migrationBuilder.DropTable(
                name: "Sections");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
