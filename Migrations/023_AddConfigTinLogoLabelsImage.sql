IF COL_LENGTH('dbo.Config', 'TinLogoLabelsImageId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Config]
    ADD [TinLogoLabelsImageId] [int] NULL;
END;
GO

IF OBJECT_ID('dbo.FK_Config_Image_TinLogoLabelsImageId', 'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[Config] WITH CHECK
    ADD CONSTRAINT [FK_Config_Image_TinLogoLabelsImageId]
        FOREIGN KEY ([TinLogoLabelsImageId]) REFERENCES [dbo].[Image]([Id]);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Config_TinLogoLabelsImageId'
      AND object_id = OBJECT_ID('dbo.Config'))
BEGIN
    CREATE INDEX [IX_Config_TinLogoLabelsImageId]
        ON [dbo].[Config]([TinLogoLabelsImageId]);
END;
GO
