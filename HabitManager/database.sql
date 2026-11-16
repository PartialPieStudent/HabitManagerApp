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
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116141656_InitialCreate'
)
BEGIN
    CREATE TABLE [Habits] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [Frequency] nvarchar(20) NOT NULL,
        [DaysOfWeek] nvarchar(100) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Habits] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116141656_InitialCreate'
)
BEGIN
    CREATE TABLE [HabitCompletions] (
        [Id] uniqueidentifier NOT NULL,
        [HabitId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        CONSTRAINT [PK_HabitCompletions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HabitCompletions_Habits_HabitId] FOREIGN KEY ([HabitId]) REFERENCES [Habits] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116141656_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_HabitCompletions_HabitId_Date] ON [HabitCompletions] ([HabitId], [Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116141656_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Habits_UserId] ON [Habits] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116141656_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261116141656_InitialCreate', N'9.0.23');
END;

COMMIT;
GO

