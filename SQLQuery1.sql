CREATE TABLE [dbo].[Recipes] (
    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Title] NVARCHAR(MAX) NOT NULL,
    [ShortDescription] NVARCHAR(MAX) NULL,
    [Instructions] NVARCHAR(MAX) NULL,
    [IngredientsJson] NVARCHAR(MAX) NULL,
    [ImageUrl] NVARCHAR(MAX) NULL,
    [DateAdded] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
    [ApplicationUserId] NVARCHAR(450) NULL,
    CONSTRAINT [FK_Recipes_AspNetUsers_ApplicationUserId]
        FOREIGN KEY ([ApplicationUserId]) REFERENCES [dbo].[AspNetUsers] ([Id])
);
CREATE INDEX [IX_Recipes_ApplicationUserId] ON [dbo].[Recipes]([ApplicationUserId]);
INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES ('20251107165505_CreateRecipesTableProperly2', '8.0.11');
