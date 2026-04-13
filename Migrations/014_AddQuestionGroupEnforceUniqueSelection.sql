IF COL_LENGTH('dbo.QuestionGroup', 'EnforceUniqueSelection') IS NULL
BEGIN
    ALTER TABLE [dbo].[QuestionGroup]
    ADD [EnforceUniqueSelection] [bit] NOT NULL
        CONSTRAINT [DF_QuestionGroup_EnforceUniqueSelection] DEFAULT (0);
END;
GO

UPDATE [dbo].[QuestionGroup]
SET [EnforceUniqueSelection] = 0
WHERE [EnforceUniqueSelection] IS NULL;
GO
