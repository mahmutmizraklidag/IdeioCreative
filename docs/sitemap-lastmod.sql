BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014110443_SeoPageModifications'
)
BEGIN
    CREATE TABLE [SeoPageModifications] (
        [Id] bigint NOT NULL IDENTITY,
        [PagePath] nvarchar(max) NOT NULL,
        [LastModifiedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_SeoPageModifications] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014110443_SeoPageModifications'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014110443_SeoPageModifications', N'8.0.28');
END;
GO

COMMIT;
GO

