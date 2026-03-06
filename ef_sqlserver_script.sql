IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [AdminAuditEvents] (
    [Id] bigint NOT NULL IDENTITY,
    [OccurredAtUtc] datetime2 NOT NULL,
    [AdminUserId] nvarchar(128) NOT NULL,
    [AdminEmail] nvarchar(max) NULL,
    [Action] nvarchar(128) NOT NULL,
    [TargetUserId] nvarchar(128) NULL,
    [TargetEmail] nvarchar(max) NULL,
    [DetailsJson] nvarchar(max) NULL,
    CONSTRAINT [PK_AdminAuditEvents] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AiActionHistoryEntries] (
    [Id] uniqueidentifier NOT NULL,
    [OwnerUserId] nvarchar(128) NOT NULL,
    [DocumentId] uniqueidentifier NULL,
    [SectionId] uniqueidentifier NULL,
    [PageId] uniqueidentifier NULL,
    [ActionKey] nvarchar(128) NOT NULL,
    [ProviderId] nvarchar(max) NULL,
    [ModelId] nvarchar(max) NULL,
    [RequestJson] nvarchar(max) NOT NULL,
    [ResultJson] nvarchar(max) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AiActionHistoryEntries] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [ExportPresets] (
    [Id] uniqueidentifier NOT NULL,
    [OwnerUserId] nvarchar(128) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [IsGlobalDefault] bit NOT NULL,
    [SettingsJson] nvarchar(max) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_ExportPresets] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [ExportTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [OwnerUserId] nvarchar(128) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [PresetKey] nvarchar(64) NULL,
    [PageWidthMm] int NOT NULL,
    [PageHeightMm] int NOT NULL,
    [MarginTopMm] int NOT NULL,
    [MarginRightMm] int NOT NULL,
    [MarginBottomMm] int NOT NULL,
    [MarginLeftMm] int NOT NULL,
    [FontFamily] nvarchar(max) NOT NULL,
    [BodyFontSizePt] int NOT NULL,
    [LineHeight] decimal(5,2) NOT NULL,
    [ParagraphSpacingPt] int NOT NULL,
    [HeaderEnabled] bit NOT NULL,
    [HeaderLeft] nvarchar(max) NULL,
    [HeaderCenter] nvarchar(max) NULL,
    [HeaderRight] nvarchar(max) NULL,
    [FooterEnabled] bit NOT NULL,
    [FooterLeft] nvarchar(max) NULL,
    [FooterCenter] nvarchar(max) NULL,
    [FooterRight] nvarchar(max) NULL,
    [PageNumbersEnabled] bit NOT NULL,
    [PageNumberStart] int NOT NULL,
    [TocEnabled] bit NOT NULL,
    [TocDepth] int NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_ExportTemplates] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [OutlineTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [OwnerUserId] nvarchar(128) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [TemplateJson] nvarchar(max) NOT NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_OutlineTemplates] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Plans] (
    [PlanId] uniqueidentifier NOT NULL,
    [Key] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Plans] PRIMARY KEY ([PlanId])
);
GO

CREATE TABLE [Projects] (
    [Id] uniqueidentifier NOT NULL,
    [OwnerUserId] nvarchar(128) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Subtitle] nvarchar(max) NULL,
    [AuthorName] nvarchar(max) NULL,
    [Language] nvarchar(max) NULL,
    [Genre] nvarchar(max) NULL,
    [DefaultExportSettingsJson] nvarchar(max) NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Projects] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [PromptPresets] (
    [Id] uniqueidentifier NOT NULL,
    [OwnerUserId] nvarchar(128) NOT NULL,
    [ProjectId] uniqueidentifier NULL,
    [Name] nvarchar(max) NOT NULL,
    [Category] nvarchar(max) NULL,
    [Kind] nvarchar(64) NOT NULL,
    [BuiltinActionId] nvarchar(max) NULL,
    [TemplateText] nvarchar(max) NULL,
    [ParametersJson] nvarchar(max) NOT NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_PromptPresets] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [SearchIndexEntries] (
    [Id] bigint NOT NULL IDENTITY,
    [EntityType] nvarchar(32) NOT NULL,
    [EntityId] nvarchar(64) NOT NULL,
    [DocumentId] nvarchar(64) NOT NULL,
    [ProjectId] nvarchar(64) NOT NULL,
    [SectionId] nvarchar(64) NULL,
    [PageId] nvarchar(64) NULL,
    [Title] nvarchar(max) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [UpdatedAt] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_SearchIndexEntries] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [StripeEventLogs] (
    [StripeEventId] nvarchar(100) NOT NULL,
    [Type] nvarchar(100) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [ReceivedUtc] datetime2 NOT NULL,
    [ProcessedUtc] datetime2 NULL,
    [Error] nvarchar(2000) NULL,
    [UserId] nvarchar(100) NULL,
    CONSTRAINT [PK_StripeEventLogs] PRIMARY KEY ([StripeEventId])
);
GO

CREATE TABLE [TokenAdjustments] (
    [Id] bigint NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [DeltaTokens] int NOT NULL,
    [Reason] nvarchar(max) NOT NULL,
    [AdjustedBy] nvarchar(max) NOT NULL,
    [AdjustedByEmail] nvarchar(max) NULL,
    [OccurredAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_TokenAdjustments] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [UsageAggregates] (
    [UserId] nvarchar(450) NOT NULL,
    [PeriodStartUtc] datetime2 NOT NULL,
    [PeriodEndUtc] datetime2 NOT NULL,
    [Kind] nvarchar(450) NOT NULL,
    [TotalInputTokens] int NOT NULL,
    [TotalOutputTokens] int NOT NULL,
    [TotalCostMicros] bigint NOT NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_UsageAggregates] PRIMARY KEY ([UserId], [PeriodStartUtc], [PeriodEndUtc], [Kind])
);
GO

CREATE TABLE [UsageEvents] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] nvarchar(max) NOT NULL,
    [Kind] nvarchar(max) NOT NULL,
    [Provider] nvarchar(max) NOT NULL,
    [Model] nvarchar(max) NOT NULL,
    [InputTokens] int NOT NULL,
    [OutputTokens] int NOT NULL,
    [CostMicros] bigint NULL,
    [DocumentId] uniqueidentifier NULL,
    [SectionId] uniqueidentifier NULL,
    [TimestampUtc] datetime2 NOT NULL,
    [CorrelationId] uniqueidentifier NULL,
    CONSTRAINT [PK_UsageEvents] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [UserEntitlements] (
    [UserId] nvarchar(450) NOT NULL,
    [PlanKey] nvarchar(max) NOT NULL,
    [SubscriptionStatus] nvarchar(max) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [AiMonthlyTokenBudget] int NOT NULL,
    [AiTokensUsedThisPeriod] int NOT NULL,
    [PeriodStartUtc] datetimeoffset NOT NULL,
    [StripeCustomerId] nvarchar(max) NULL,
    [StripeSubscriptionId] nvarchar(max) NULL,
    [StripePriceId] nvarchar(max) NULL,
    [CurrentPeriodEndUtc] datetimeoffset NULL,
    [CancelAtPeriodEnd] bit NOT NULL DEFAULT CAST(0 AS bit),
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_UserEntitlements] PRIMARY KEY ([UserId])
);
GO

CREATE TABLE [UserEvents] (
    [Id] bigint NOT NULL IDENTITY,
    [UserId] nvarchar(128) NOT NULL,
    [EventName] nvarchar(128) NOT NULL,
    [MetadataJson] nvarchar(max) NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_UserEvents] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [UserProfiles] (
    [UserId] nvarchar(450) NOT NULL,
    [DisplayName] nvarchar(max) NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [HasOnboarded] bit NOT NULL,
    [HasCompletedOnboarding] bit NOT NULL DEFAULT CAST(0 AS bit),
    [OnboardingStep] int NOT NULL DEFAULT 0,
    [OnboardingStartedUtc] datetimeoffset NULL,
    [OnboardingCompletedUtc] datetimeoffset NULL,
    [PrimaryWritingIntent] nvarchar(max) NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_UserProfiles] PRIMARY KEY ([UserId])
);
GO

CREATE TABLE [AiActionAppliedEvents] (
    [Id] uniqueidentifier NOT NULL,
    [OwnerUserId] nvarchar(128) NOT NULL,
    [HistoryEntryId] uniqueidentifier NOT NULL,
    [AppliedAt] datetimeoffset NOT NULL,
    [AppliedToPageId] uniqueidentifier NULL,
    [AppliedToSectionId] uniqueidentifier NULL,
    [AppliedToDocumentId] uniqueidentifier NULL,
    [BeforeContent] nvarchar(max) NULL,
    [AfterContent] nvarchar(max) NULL,
    [UndoneAt] datetimeoffset NULL,
    CONSTRAINT [PK_AiActionAppliedEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AiActionAppliedEvents_AiActionHistoryEntries_HistoryEntryId] FOREIGN KEY ([HistoryEntryId]) REFERENCES [AiActionHistoryEntries] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [PlanEntitlements] (
    [PlanId] uniqueidentifier NOT NULL,
    [Key] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_PlanEntitlements] PRIMARY KEY ([PlanId], [Key]),
    CONSTRAINT [FK_PlanEntitlements_Plans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [Plans] ([PlanId]) ON DELETE CASCADE
);
GO

CREATE TABLE [UserPlanAssignments] (
    [UserId] nvarchar(450) NOT NULL,
    [PlanId] uniqueidentifier NOT NULL,
    [AssignedUtc] datetime2 NOT NULL,
    [AssignedBy] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_UserPlanAssignments] PRIMARY KEY ([UserId], [PlanId]),
    CONSTRAINT [FK_UserPlanAssignments_Plans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [Plans] ([PlanId]) ON DELETE CASCADE
);
GO

CREATE TABLE [Documents] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [OwnerUserId] nvarchar(128) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [DocumentKind] int NOT NULL,
    [LanguageCode] nvarchar(max) NULL,
    [TranslationGroupId] uniqueidentifier NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    [CreatedAtUnixSeconds] bigint NOT NULL,
    [UpdatedAtUnixSeconds] bigint NOT NULL,
    [IsArchived] bit NOT NULL,
    [ArchivedAt] datetimeoffset NULL,
    [DeletedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_Documents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Documents_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ProjectGoals] (
    [ProjectId] uniqueidentifier NOT NULL,
    [DailyTargetWords] int NOT NULL,
    [WeeklyTargetWords] int NOT NULL,
    [Timezone] nvarchar(max) NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_ProjectGoals] PRIMARY KEY ([ProjectId]),
    CONSTRAINT [FK_ProjectGoals_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ProjectMilestones] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [TargetWords] int NULL,
    [TargetNodeId] uniqueidentifier NULL,
    [Status] int NOT NULL,
    [CompletedUtc] datetimeoffset NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_ProjectMilestones] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProjectMilestones_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ProjectProgressDaily] (
    [ProjectId] uniqueidentifier NOT NULL,
    [Date] nvarchar(10) NOT NULL,
    [WordsDelta] int NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_ProjectProgressDaily] PRIMARY KEY ([ProjectId], [Date]),
    CONSTRAINT [FK_ProjectProgressDaily_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ProjectProgressEvents] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [EventKey] nvarchar(450) NOT NULL,
    [Date] nvarchar(10) NOT NULL,
    [WordsDelta] int NOT NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_ProjectProgressEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProjectProgressEvents_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WritingSessions] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [StartedUtc] datetime2 NOT NULL,
    [EndedUtc] datetime2 NULL,
    [DurationSeconds] int NOT NULL,
    [WordsDelta] int NOT NULL,
    [StartWordCount] int NOT NULL,
    [Notes] nvarchar(max) NULL,
    CONSTRAINT [PK_WritingSessions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WritingSessions_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [BibleSnapshots] (
    [Id] uniqueidentifier NOT NULL,
    [DocumentId] uniqueidentifier NOT NULL,
    [BibleType] nvarchar(450) NOT NULL,
    [SchemaVersion] int NOT NULL,
    [ContentJson] nvarchar(max) NOT NULL,
    [CreatedUtc] datetimeoffset NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    [LastRefreshUtc] datetimeoffset NULL,
    [LastRefreshSourceHash] nvarchar(max) NOT NULL,
    [LastRefreshStatsJson] nvarchar(max) NOT NULL,
    [LastRefreshCursorJson] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_BibleSnapshots] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BibleSnapshots_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [DocumentGlossaryEntries] (
    [Id] uniqueidentifier NOT NULL,
    [DocumentId] uniqueidentifier NOT NULL,
    [Term] nvarchar(max) NOT NULL,
    [NormalizedTerm] nvarchar(256) NOT NULL,
    [Notes] nvarchar(max) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_DocumentGlossaryEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DocumentGlossaryEntries_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [DocumentOutlines] (
    [DocumentId] uniqueidentifier NOT NULL,
    [Outline] nvarchar(max) NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_DocumentOutlines] PRIMARY KEY ([DocumentId]),
    CONSTRAINT [FK_DocumentOutlines_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [DocumentSynopses] (
    [DocumentId] uniqueidentifier NOT NULL,
    [Logline] nvarchar(max) NOT NULL,
    [Premise] nvarchar(max) NOT NULL,
    [Theme] nvarchar(max) NOT NULL,
    [ProtagonistArc] nvarchar(max) NOT NULL,
    [CentralConflict] nvarchar(max) NOT NULL,
    [Stakes] nvarchar(max) NOT NULL,
    [Setting] nvarchar(max) NOT NULL,
    [EndingIntent] nvarchar(max) NOT NULL,
    [OpenQuestions] nvarchar(max) NOT NULL,
    [Notes] nvarchar(max) NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_DocumentSynopses] PRIMARY KEY ([DocumentId]),
    CONSTRAINT [FK_DocumentSynopses_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ProjectExportSettings] (
    [DocumentId] uniqueidentifier NOT NULL,
    [UserId] nvarchar(128) NOT NULL,
    [DefaultPresetId] uniqueidentifier NULL,
    [OverridesJson] nvarchar(max) NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_ProjectExportSettings] PRIMARY KEY ([DocumentId], [UserId]),
    CONSTRAINT [FK_ProjectExportSettings_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProjectExportSettings_ExportPresets_DefaultPresetId] FOREIGN KEY ([DefaultPresetId]) REFERENCES [ExportPresets] ([Id]) ON DELETE SET NULL
);
GO

CREATE TABLE [Sections] (
    [Id] uniqueidentifier NOT NULL,
    [DocumentId] uniqueidentifier NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [NarrativePurpose] nvarchar(max) NULL,
    [LanguageCode] nvarchar(max) NULL,
    [TranslationGroupId] uniqueidentifier NULL,
    [OrderIndex] int NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Sections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Sections_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [DocumentOutlineNodes] (
    [Id] uniqueidentifier NOT NULL,
    [DocumentId] uniqueidentifier NOT NULL,
    [ParentId] uniqueidentifier NULL,
    [Order] int NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Notes] nvarchar(max) NULL,
    [MetadataJson] nvarchar(max) NULL,
    [LinkedSectionId] uniqueidentifier NULL,
    CONSTRAINT [PK_DocumentOutlineNodes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DocumentOutlineNodes_DocumentOutlineNodes_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [DocumentOutlineNodes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_DocumentOutlineNodes_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_DocumentOutlineNodes_Sections_LinkedSectionId] FOREIGN KEY ([LinkedSectionId]) REFERENCES [Sections] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Pages] (
    [Id] uniqueidentifier NOT NULL,
    [DocumentId] uniqueidentifier NOT NULL,
    [SectionId] uniqueidentifier NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [OrderIndex] int NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Pages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Pages_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Pages_Sections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [Sections] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ProjectNodes] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [ParentId] uniqueidentifier NULL,
    [NodeType] int NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [OrderIndex] int NOT NULL,
    [LinkedSectionId] uniqueidentifier NULL,
    [MetadataJson] nvarchar(max) NULL,
    [WordCountCache] int NOT NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_ProjectNodes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProjectNodes_ProjectNodes_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [ProjectNodes] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProjectNodes_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProjectNodes_Sections_LinkedSectionId] FOREIGN KEY ([LinkedSectionId]) REFERENCES [Sections] ([Id]) ON DELETE SET NULL
);
GO

CREATE TABLE [SectionNotes] (
    [SectionId] uniqueidentifier NOT NULL,
    [NotesText] nvarchar(max) NOT NULL,
    [UpdatedAtUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_SectionNotes] PRIMARY KEY ([SectionId]),
    CONSTRAINT [FK_SectionNotes_Sections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [Sections] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SectionSceneCards] (
    [SectionId] uniqueidentifier NOT NULL,
    [NarrativePurpose] nvarchar(max) NULL,
    [EmotionalBeat] nvarchar(max) NULL,
    [KeyEvents] nvarchar(max) NULL,
    [OpenQuestions] nvarchar(max) NULL,
    [PovCharacterId] nvarchar(max) NULL,
    [PlaceId] nvarchar(max) NULL,
    [TimelineEventId] nvarchar(max) NULL,
    [TimeRef] nvarchar(120) NULL,
    [TagsJson] nvarchar(max) NULL,
    [ReferencesJson] nvarchar(max) NULL,
    [UpdatedUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_SectionSceneCards] PRIMARY KEY ([SectionId]),
    CONSTRAINT [FK_SectionSceneCards_Sections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [Sections] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [PageAnnotations] (
    [Id] uniqueidentifier NOT NULL,
    [DocumentId] uniqueidentifier NOT NULL,
    [PageId] uniqueidentifier NOT NULL,
    [Kind] nvarchar(450) NOT NULL,
    [Status] nvarchar(450) NOT NULL,
    [AnchorFrom] int NOT NULL,
    [AnchorTo] int NOT NULL,
    [AnchorText] nvarchar(max) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [AuthorUserId] nvarchar(max) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ResolvedAt] datetimeoffset NULL,
    CONSTRAINT [PK_PageAnnotations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PageAnnotations_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PageAnnotations_Pages_PageId] FOREIGN KEY ([PageId]) REFERENCES [Pages] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [PageNotes] (
    [PageId] uniqueidentifier NOT NULL,
    [Notes] nvarchar(max) NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_PageNotes] PRIMARY KEY ([PageId]),
    CONSTRAINT [FK_PageNotes_Pages_PageId] FOREIGN KEY ([PageId]) REFERENCES [Pages] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [PageQualityIssueDismissals] (
    [UserId] nvarchar(128) NOT NULL,
    [PageId] uniqueidentifier NOT NULL,
    [IssueKey] nvarchar(128) NOT NULL,
    [DismissedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_PageQualityIssueDismissals] PRIMARY KEY ([UserId], [PageId], [IssueKey]),
    CONSTRAINT [FK_PageQualityIssueDismissals_Pages_PageId] FOREIGN KEY ([PageId]) REFERENCES [Pages] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [PageQualityIssues] (
    [Id] uniqueidentifier NOT NULL,
    [DocumentId] uniqueidentifier NOT NULL,
    [PageId] uniqueidentifier NOT NULL,
    [Scope] nvarchar(450) NOT NULL,
    [IssueKey] nvarchar(128) NOT NULL,
    [RuleId] nvarchar(max) NOT NULL,
    [Kind] nvarchar(max) NOT NULL,
    [Severity] nvarchar(max) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [Suggestion] nvarchar(max) NULL,
    [AnchorText] nvarchar(max) NULL,
    [StartOffset] int NOT NULL,
    [EndOffset] int NOT NULL,
    [ContentHash] nvarchar(450) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_PageQualityIssues] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PageQualityIssues_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PageQualityIssues_Pages_PageId] FOREIGN KEY ([PageId]) REFERENCES [Pages] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [PageVersions] (
    [Id] uniqueidentifier NOT NULL,
    [PageId] uniqueidentifier NOT NULL,
    [DocumentId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [Reason] nvarchar(max) NOT NULL,
    [ContentCompressed] varbinary(max) NOT NULL,
    [ContentTextHash] nvarchar(max) NOT NULL,
    [SizeBytes] int NOT NULL,
    [WordCount] int NOT NULL,
    CONSTRAINT [PK_PageVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PageVersions_Pages_PageId] FOREIGN KEY ([PageId]) REFERENCES [Pages] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SceneAnnotations] (
    [Id] uniqueidentifier NOT NULL,
    [SceneNodeId] uniqueidentifier NOT NULL,
    [Kind] nvarchar(450) NOT NULL,
    [Status] nvarchar(450) NOT NULL,
    [AnchorFrom] int NOT NULL,
    [AnchorTo] int NOT NULL,
    [AnchorText] nvarchar(max) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [AuthorUserId] nvarchar(max) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ResolvedAt] datetimeoffset NULL,
    CONSTRAINT [PK_SceneAnnotations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SceneAnnotations_ProjectNodes_SceneNodeId] FOREIGN KEY ([SceneNodeId]) REFERENCES [ProjectNodes] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SceneCards] (
    [SceneNodeId] uniqueidentifier NOT NULL,
    [NarrativePurpose] nvarchar(max) NULL,
    [EmotionalBeat] nvarchar(max) NULL,
    [KeyEvents] nvarchar(max) NULL,
    [OpenQuestions] nvarchar(max) NULL,
    [PovCharacterId] nvarchar(max) NULL,
    [PlaceId] nvarchar(max) NULL,
    [TimelineEventId] nvarchar(max) NULL,
    [TimeRef] nvarchar(120) NULL,
    [TagsJson] nvarchar(max) NULL,
    [ReferencesJson] nvarchar(max) NULL,
    [UpdatedAtUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_SceneCards] PRIMARY KEY ([SceneNodeId]),
    CONSTRAINT [FK_SceneCards_ProjectNodes_SceneNodeId] FOREIGN KEY ([SceneNodeId]) REFERENCES [ProjectNodes] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SceneContents] (
    [SceneNodeId] uniqueidentifier NOT NULL,
    [ContentJson] nvarchar(max) NOT NULL,
    [LanguageCode] nvarchar(max) NULL,
    [UpdatedAtUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_SceneContents] PRIMARY KEY ([SceneNodeId]),
    CONSTRAINT [FK_SceneContents_ProjectNodes_SceneNodeId] FOREIGN KEY ([SceneNodeId]) REFERENCES [ProjectNodes] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SceneNotes] (
    [SceneNodeId] uniqueidentifier NOT NULL,
    [NotesText] nvarchar(max) NOT NULL,
    [UpdatedAtUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_SceneNotes] PRIMARY KEY ([SceneNodeId]),
    CONSTRAINT [FK_SceneNotes_ProjectNodes_SceneNodeId] FOREIGN KEY ([SceneNodeId]) REFERENCES [ProjectNodes] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SceneQualityIssues] (
    [Id] uniqueidentifier NOT NULL,
    [SceneNodeId] uniqueidentifier NOT NULL,
    [Scope] nvarchar(450) NOT NULL,
    [IssueKey] nvarchar(128) NOT NULL,
    [RuleId] nvarchar(max) NOT NULL,
    [Kind] nvarchar(max) NOT NULL,
    [Severity] nvarchar(max) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [Suggestion] nvarchar(max) NULL,
    [AnchorText] nvarchar(max) NULL,
    [StartOffset] int NOT NULL,
    [EndOffset] int NOT NULL,
    [ContentHash] nvarchar(450) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_SceneQualityIssues] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SceneQualityIssues_ProjectNodes_SceneNodeId] FOREIGN KEY ([SceneNodeId]) REFERENCES [ProjectNodes] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SceneVersions] (
    [Id] uniqueidentifier NOT NULL,
    [SceneNodeId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [Reason] nvarchar(max) NOT NULL,
    [ContentCompressed] varbinary(max) NOT NULL,
    [ContentTextHash] nvarchar(max) NOT NULL,
    [SizeBytes] int NOT NULL,
    [WordCount] int NOT NULL,
    CONSTRAINT [PK_SceneVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SceneVersions_ProjectNodes_SceneNodeId] FOREIGN KEY ([SceneNodeId]) REFERENCES [ProjectNodes] ([Id]) ON DELETE CASCADE
);
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PlanId', N'IsActive', N'Key', N'Name') AND [object_id] = OBJECT_ID(N'[Plans]'))
    SET IDENTITY_INSERT [Plans] ON;
INSERT INTO [Plans] ([PlanId], [IsActive], [Key], [Name])
VALUES ('5f4d2c6f-98fd-4a26-9c0f-0a2a1f2d7c4b', CAST(1 AS bit), N'free', N'Free'),
('6d1d34ef-2a0f-4b24-8b3f-7f3f4a4b9f0b', CAST(1 AS bit), N'professional', N'Professional'),
('83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5', CAST(1 AS bit), N'standard', N'Standard');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PlanId', N'IsActive', N'Key', N'Name') AND [object_id] = OBJECT_ID(N'[Plans]'))
    SET IDENTITY_INSERT [Plans] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'UserId', N'CreatedUtc', N'DisplayName', N'HasCompletedOnboarding', N'HasOnboarded', N'OnboardingCompletedUtc', N'OnboardingStartedUtc', N'PrimaryWritingIntent', N'UpdatedUtc') AND [object_id] = OBJECT_ID(N'[UserProfiles]'))
    SET IDENTITY_INSERT [UserProfiles] ON;
INSERT INTO [UserProfiles] ([UserId], [CreatedUtc], [DisplayName], [HasCompletedOnboarding], [HasOnboarded], [OnboardingCompletedUtc], [OnboardingStartedUtc], [PrimaryWritingIntent], [UpdatedUtc])
VALUES (N'seed-system', '2025-01-01T00:00:00.0000000Z', N'System', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, NULL, '2025-01-01T00:00:00.0000000Z');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'UserId', N'CreatedUtc', N'DisplayName', N'HasCompletedOnboarding', N'HasOnboarded', N'OnboardingCompletedUtc', N'OnboardingStartedUtc', N'PrimaryWritingIntent', N'UpdatedUtc') AND [object_id] = OBJECT_ID(N'[UserProfiles]'))
    SET IDENTITY_INSERT [UserProfiles] OFF;
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Key', N'PlanId', N'Value') AND [object_id] = OBJECT_ID(N'[PlanEntitlements]'))
    SET IDENTITY_INSERT [PlanEntitlements] ON;
INSERT INTO [PlanEntitlements] ([Key], [PlanId], [Value])
VALUES (N'ai.enabled', '5f4d2c6f-98fd-4a26-9c0f-0a2a1f2d7c4b', N'false'),
(N'ai.images.cover', '5f4d2c6f-98fd-4a26-9c0f-0a2a1f2d7c4b', N'false'),
(N'ai.monthly_tokens', '5f4d2c6f-98fd-4a26-9c0f-0a2a1f2d7c4b', N'0'),
(N'export.pdf', '5f4d2c6f-98fd-4a26-9c0f-0a2a1f2d7c4b', N'false'),
(N'history.enabled', '5f4d2c6f-98fd-4a26-9c0f-0a2a1f2d7c4b', N'true'),
(N'history.max_versions', '5f4d2c6f-98fd-4a26-9c0f-0a2a1f2d7c4b', N'5'),
(N'ai.enabled', '6d1d34ef-2a0f-4b24-8b3f-7f3f4a4b9f0b', N'true'),
(N'ai.images.cover', '6d1d34ef-2a0f-4b24-8b3f-7f3f4a4b9f0b', N'true'),
(N'ai.monthly_tokens', '6d1d34ef-2a0f-4b24-8b3f-7f3f4a4b9f0b', N'1000000'),
(N'export.pdf', '6d1d34ef-2a0f-4b24-8b3f-7f3f4a4b9f0b', N'true'),
(N'history.enabled', '6d1d34ef-2a0f-4b24-8b3f-7f3f4a4b9f0b', N'true'),
(N'history.retention_days', '6d1d34ef-2a0f-4b24-8b3f-7f3f4a4b9f0b', N'30'),
(N'ai.enabled', '83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5', N'true'),
(N'ai.images.cover', '83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5', N'false'),
(N'ai.monthly_tokens', '83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5', N'200000'),
(N'export.pdf', '83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5', N'true'),
(N'history.enabled', '83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5', N'true'),
(N'history.retention_days', '83d8f8f0-6d2f-4d68-b7df-4192dce1a6f5', N'30');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Key', N'PlanId', N'Value') AND [object_id] = OBJECT_ID(N'[PlanEntitlements]'))
    SET IDENTITY_INSERT [PlanEntitlements] OFF;
GO

CREATE INDEX [IX_AdminAuditEvents_Action] ON [AdminAuditEvents] ([Action]);
GO

CREATE INDEX [IX_AdminAuditEvents_AdminUserId] ON [AdminAuditEvents] ([AdminUserId]);
GO

CREATE INDEX [IX_AdminAuditEvents_OccurredAtUtc] ON [AdminAuditEvents] ([OccurredAtUtc]);
GO

CREATE INDEX [IX_AdminAuditEvents_TargetUserId] ON [AdminAuditEvents] ([TargetUserId]);
GO

CREATE INDEX [IX_AiActionAppliedEvents_AppliedAt] ON [AiActionAppliedEvents] ([AppliedAt]);
GO

CREATE INDEX [IX_AiActionAppliedEvents_HistoryEntryId] ON [AiActionAppliedEvents] ([HistoryEntryId]);
GO

CREATE INDEX [IX_AiActionAppliedEvents_OwnerUserId] ON [AiActionAppliedEvents] ([OwnerUserId]);
GO

CREATE INDEX [IX_AiActionAppliedEvents_UndoneAt] ON [AiActionAppliedEvents] ([UndoneAt]);
GO

CREATE INDEX [IX_AiActionHistoryEntries_ActionKey] ON [AiActionHistoryEntries] ([ActionKey]);
GO

CREATE INDEX [IX_AiActionHistoryEntries_CreatedAt] ON [AiActionHistoryEntries] ([CreatedAt]);
GO

CREATE INDEX [IX_AiActionHistoryEntries_DocumentId] ON [AiActionHistoryEntries] ([DocumentId]);
GO

CREATE INDEX [IX_AiActionHistoryEntries_OwnerUserId] ON [AiActionHistoryEntries] ([OwnerUserId]);
GO

CREATE UNIQUE INDEX [IX_BibleSnapshots_DocumentId_BibleType] ON [BibleSnapshots] ([DocumentId], [BibleType]);
GO

CREATE INDEX [IX_BibleSnapshots_LastRefreshUtc] ON [BibleSnapshots] ([LastRefreshUtc]);
GO

CREATE INDEX [IX_DocumentGlossaryEntries_DocumentId] ON [DocumentGlossaryEntries] ([DocumentId]);
GO

CREATE INDEX [IX_DocumentGlossaryEntries_NormalizedTerm] ON [DocumentGlossaryEntries] ([NormalizedTerm]);
GO

CREATE INDEX [IX_DocumentOutlineNodes_DocumentId_ParentId_Order] ON [DocumentOutlineNodes] ([DocumentId], [ParentId], [Order]);
GO

CREATE INDEX [IX_DocumentOutlineNodes_LinkedSectionId] ON [DocumentOutlineNodes] ([LinkedSectionId]);
GO

CREATE INDEX [IX_DocumentOutlineNodes_ParentId] ON [DocumentOutlineNodes] ([ParentId]);
GO

CREATE INDEX [IX_Documents_DeletedAtUtc] ON [Documents] ([DeletedAtUtc]);
GO

CREATE INDEX [IX_Documents_DocumentKind] ON [Documents] ([DocumentKind]);
GO

CREATE INDEX [IX_Documents_IsArchived] ON [Documents] ([IsArchived]);
GO

CREATE INDEX [IX_Documents_OwnerUserId_UpdatedAtUnixSeconds] ON [Documents] ([OwnerUserId], [UpdatedAtUnixSeconds]);
GO

CREATE INDEX [IX_Documents_ProjectId] ON [Documents] ([ProjectId]);
GO

CREATE UNIQUE INDEX [IX_Documents_ProjectId_DocumentKind] ON [Documents] ([ProjectId], [DocumentKind]) WHERE "DocumentKind" = 0;
GO

CREATE INDEX [IX_Documents_ProjectId_UpdatedAtUnixSeconds] ON [Documents] ([ProjectId], [UpdatedAtUnixSeconds]);
GO

CREATE INDEX [IX_DocumentSynopses_UpdatedAt] ON [DocumentSynopses] ([UpdatedAt]);
GO

CREATE INDEX [IX_ExportPresets_OwnerUserId] ON [ExportPresets] ([OwnerUserId]);
GO

CREATE INDEX [IX_ExportPresets_OwnerUserId_IsGlobalDefault] ON [ExportPresets] ([OwnerUserId], [IsGlobalDefault]);
GO

CREATE INDEX [IX_ExportPresets_UpdatedAt] ON [ExportPresets] ([UpdatedAt]);
GO

CREATE INDEX [IX_ExportTemplates_OwnerUserId] ON [ExportTemplates] ([OwnerUserId]);
GO

CREATE INDEX [IX_ExportTemplates_OwnerUserId_PresetKey] ON [ExportTemplates] ([OwnerUserId], [PresetKey]);
GO

CREATE INDEX [IX_OutlineTemplates_OwnerUserId] ON [OutlineTemplates] ([OwnerUserId]);
GO

CREATE INDEX [IX_OutlineTemplates_UpdatedUtc] ON [OutlineTemplates] ([UpdatedUtc]);
GO

CREATE INDEX [IX_PageAnnotations_CreatedAt] ON [PageAnnotations] ([CreatedAt]);
GO

CREATE INDEX [IX_PageAnnotations_DocumentId] ON [PageAnnotations] ([DocumentId]);
GO

CREATE INDEX [IX_PageAnnotations_Kind] ON [PageAnnotations] ([Kind]);
GO

CREATE INDEX [IX_PageAnnotations_PageId] ON [PageAnnotations] ([PageId]);
GO

CREATE INDEX [IX_PageAnnotations_Status] ON [PageAnnotations] ([Status]);
GO

CREATE INDEX [IX_PageQualityIssueDismissals_PageId] ON [PageQualityIssueDismissals] ([PageId]);
GO

CREATE INDEX [IX_PageQualityIssues_ContentHash] ON [PageQualityIssues] ([ContentHash]);
GO

CREATE INDEX [IX_PageQualityIssues_DocumentId] ON [PageQualityIssues] ([DocumentId]);
GO

CREATE INDEX [IX_PageQualityIssues_IssueKey] ON [PageQualityIssues] ([IssueKey]);
GO

CREATE INDEX [IX_PageQualityIssues_PageId] ON [PageQualityIssues] ([PageId]);
GO

CREATE INDEX [IX_PageQualityIssues_Scope] ON [PageQualityIssues] ([Scope]);
GO

CREATE INDEX [IX_Pages_DocumentId] ON [Pages] ([DocumentId]);
GO

CREATE INDEX [IX_Pages_SectionId_OrderIndex] ON [Pages] ([SectionId], [OrderIndex]);
GO

CREATE INDEX [IX_PageVersions_CreatedAt] ON [PageVersions] ([CreatedAt]);
GO

CREATE INDEX [IX_PageVersions_DocumentId] ON [PageVersions] ([DocumentId]);
GO

CREATE INDEX [IX_PageVersions_PageId] ON [PageVersions] ([PageId]);
GO

CREATE UNIQUE INDEX [IX_Plans_Key] ON [Plans] ([Key]);
GO

CREATE INDEX [IX_ProjectExportSettings_DefaultPresetId] ON [ProjectExportSettings] ([DefaultPresetId]);
GO

CREATE INDEX [IX_ProjectExportSettings_UserId] ON [ProjectExportSettings] ([UserId]);
GO

CREATE INDEX [IX_ProjectMilestones_ProjectId] ON [ProjectMilestones] ([ProjectId]);
GO

CREATE INDEX [IX_ProjectMilestones_Status] ON [ProjectMilestones] ([Status]);
GO

CREATE INDEX [IX_ProjectNodes_LinkedSectionId] ON [ProjectNodes] ([LinkedSectionId]);
GO

CREATE INDEX [IX_ProjectNodes_ParentId] ON [ProjectNodes] ([ParentId]);
GO

CREATE INDEX [IX_ProjectNodes_ProjectId_ParentId_OrderIndex] ON [ProjectNodes] ([ProjectId], [ParentId], [OrderIndex]);
GO

CREATE INDEX [IX_ProjectProgressEvents_ProjectId] ON [ProjectProgressEvents] ([ProjectId]);
GO

CREATE UNIQUE INDEX [IX_ProjectProgressEvents_ProjectId_EventKey] ON [ProjectProgressEvents] ([ProjectId], [EventKey]);
GO

CREATE INDEX [IX_Projects_OwnerUserId] ON [Projects] ([OwnerUserId]);
GO

CREATE INDEX [IX_Projects_UpdatedUtc] ON [Projects] ([UpdatedUtc]);
GO

CREATE INDEX [IX_PromptPresets_OwnerUserId] ON [PromptPresets] ([OwnerUserId]);
GO

CREATE INDEX [IX_PromptPresets_OwnerUserId_Kind] ON [PromptPresets] ([OwnerUserId], [Kind]);
GO

CREATE INDEX [IX_PromptPresets_OwnerUserId_ProjectId] ON [PromptPresets] ([OwnerUserId], [ProjectId]);
GO

CREATE INDEX [IX_PromptPresets_UpdatedUtc] ON [PromptPresets] ([UpdatedUtc]);
GO

CREATE INDEX [IX_SceneAnnotations_CreatedAt] ON [SceneAnnotations] ([CreatedAt]);
GO

CREATE INDEX [IX_SceneAnnotations_Kind] ON [SceneAnnotations] ([Kind]);
GO

CREATE INDEX [IX_SceneAnnotations_SceneNodeId] ON [SceneAnnotations] ([SceneNodeId]);
GO

CREATE INDEX [IX_SceneAnnotations_Status] ON [SceneAnnotations] ([Status]);
GO

CREATE INDEX [IX_SceneQualityIssues_ContentHash] ON [SceneQualityIssues] ([ContentHash]);
GO

CREATE INDEX [IX_SceneQualityIssues_IssueKey] ON [SceneQualityIssues] ([IssueKey]);
GO

CREATE INDEX [IX_SceneQualityIssues_SceneNodeId] ON [SceneQualityIssues] ([SceneNodeId]);
GO

CREATE INDEX [IX_SceneQualityIssues_Scope] ON [SceneQualityIssues] ([Scope]);
GO

CREATE INDEX [IX_SceneVersions_CreatedAt] ON [SceneVersions] ([CreatedAt]);
GO

CREATE INDEX [IX_SceneVersions_SceneNodeId] ON [SceneVersions] ([SceneNodeId]);
GO

CREATE INDEX [IX_SearchIndexEntries_DocumentId] ON [SearchIndexEntries] ([DocumentId]);
GO

CREATE UNIQUE INDEX [IX_SearchIndexEntries_EntityType_EntityId_DocumentId] ON [SearchIndexEntries] ([EntityType], [EntityId], [DocumentId]);
GO

CREATE INDEX [IX_SearchIndexEntries_ProjectId] ON [SearchIndexEntries] ([ProjectId]);
GO

CREATE INDEX [IX_Sections_DocumentId_OrderIndex] ON [Sections] ([DocumentId], [OrderIndex]);
GO

CREATE INDEX [IX_StripeEventLogs_ReceivedUtc] ON [StripeEventLogs] ([ReceivedUtc]);
GO

CREATE INDEX [IX_TokenAdjustments_OccurredAtUtc] ON [TokenAdjustments] ([OccurredAtUtc]);
GO

CREATE INDEX [IX_TokenAdjustments_UserId] ON [TokenAdjustments] ([UserId]);
GO

CREATE INDEX [IX_UserEvents_CreatedUtc] ON [UserEvents] ([CreatedUtc]);
GO

CREATE INDEX [IX_UserEvents_EventName] ON [UserEvents] ([EventName]);
GO

CREATE INDEX [IX_UserEvents_UserId] ON [UserEvents] ([UserId]);
GO

CREATE INDEX [IX_UserPlanAssignments_PlanId] ON [UserPlanAssignments] ([PlanId]);
GO

CREATE INDEX [IX_UserProfiles_HasCompletedOnboarding] ON [UserProfiles] ([HasCompletedOnboarding]);
GO

CREATE INDEX [IX_WritingSessions_ProjectId_StartedUtc] ON [WritingSessions] ([ProjectId], [StartedUtc]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260304060632_InitialSqlServerBaseline_20260304', N'8.0.4');
GO

COMMIT;
GO

