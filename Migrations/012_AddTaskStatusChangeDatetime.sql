IF COL_LENGTH('dbo.Task', 'StatusChangeDatetime') IS NULL
BEGIN
    ALTER TABLE [dbo].[Task]
    ADD [StatusChangeDatetime] datetime NULL;
END
GO

UPDATE [dbo].[Task]
SET [StatusChangeDatetime] = ISNULL([CompletedDatetime], [CreatedDatetime])
WHERE [StatusChangeDatetime] IS NULL;
GO
