IF COL_LENGTH('dbo.Question', 'GroupDescription') IS NULL
BEGIN
    ALTER TABLE [dbo].[Question]
    ADD [GroupDescription] [varchar](max) NULL;
END;
GO
