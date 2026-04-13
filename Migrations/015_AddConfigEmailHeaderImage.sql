IF COL_LENGTH('dbo.Config', 'EmailHeaderImageId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Config]
    ADD [EmailHeaderImageId] [int] NULL;
END;
GO

IF OBJECT_ID('dbo.FK_Config_Image_EmailHeaderImageId', 'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[Config] WITH CHECK
    ADD CONSTRAINT [FK_Config_Image_EmailHeaderImageId]
        FOREIGN KEY ([EmailHeaderImageId]) REFERENCES [dbo].[Image]([Id]);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Config_EmailHeaderImageId'
      AND object_id = OBJECT_ID('dbo.Config'))
BEGIN
    CREATE INDEX [IX_Config_EmailHeaderImageId]
        ON [dbo].[Config]([EmailHeaderImageId]);
END;
GO
