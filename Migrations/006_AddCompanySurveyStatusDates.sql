IF COL_LENGTH('dbo.CompanySurvey', 'SavedDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[CompanySurvey]
    ADD [SavedDate] [datetime2] NULL;
END;
GO

IF COL_LENGTH('dbo.CompanySurvey', 'SubmittedDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[CompanySurvey]
    ADD [SubmittedDate] [datetime2] NULL;
END;
GO

IF COL_LENGTH('dbo.CompanySurvey', 'RequestedDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[CompanySurvey]
    ADD [RequestedDate] [datetime2] NULL;
END;
GO
