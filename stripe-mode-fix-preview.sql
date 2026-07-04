BEGIN TRANSACTION;
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserEntitlements]') AND [c].[name] = N'StripeCustomerId');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [UserEntitlements] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [UserEntitlements] ALTER COLUMN [StripeCustomerId] nvarchar(450) NULL;
GO

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserEntitlements]') AND [c].[name] = N'StripeSubscriptionId');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [UserEntitlements] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [UserEntitlements] ALTER COLUMN [StripeSubscriptionId] nvarchar(450) NULL;
GO

ALTER TABLE [UserEntitlements] ADD [StripeMode] nvarchar(16) NULL;
GO

ALTER TABLE [StripeEventLogs] ADD [StripeMode] nvarchar(16) NOT NULL DEFAULT N'legacy';
GO

CREATE INDEX [IX_UserEntitlements_StripeMode_StripeCustomerId] ON [UserEntitlements] ([StripeMode], [StripeCustomerId]);
GO

CREATE INDEX [IX_UserEntitlements_StripeMode_StripeSubscriptionId] ON [UserEntitlements] ([StripeMode], [StripeSubscriptionId]);
GO

CREATE INDEX [IX_StripeEventLogs_StripeMode_ReceivedUtc] ON [StripeEventLogs] ([StripeMode], [ReceivedUtc]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260322210507_AddStripeModeToUserEntitlementsSqlServer', N'8.0.4');
GO

COMMIT;
GO

