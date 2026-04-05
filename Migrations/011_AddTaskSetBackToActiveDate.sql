IF COL_LENGTH('dbo.Task', 'SetBackToActiveDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[Task]
    ADD [SetBackToActiveDate] date NULL;
END
GO
