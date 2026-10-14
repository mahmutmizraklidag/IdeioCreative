BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014135239_ServiceFaqs'
)
BEGIN
    CREATE TABLE [ServiceFaqs] (
        [Id] int NOT NULL IDENTITY,
        [ServiceId] int NOT NULL,
        [Question] nvarchar(max) NOT NULL,
        [Answer] nvarchar(max) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsPublished] bit NOT NULL,
        CONSTRAINT [PK_ServiceFaqs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ServiceFaqs_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014135239_ServiceFaqs'
)
BEGIN
    CREATE INDEX [IX_ServiceFaqs_ServiceId_SortOrder] ON [ServiceFaqs] ([ServiceId], [SortOrder]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014135239_ServiceFaqs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014135239_ServiceFaqs', N'8.0.28');
END;
GO

COMMIT;
GO

