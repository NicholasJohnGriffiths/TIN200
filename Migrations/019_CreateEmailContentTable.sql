-- Migration: Create EmailContent table for reusable survey send/reminder templates

IF OBJECT_ID('dbo.EmailContent', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailContent
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmailContent PRIMARY KEY,
        Name VARCHAR(255) NOT NULL,
        Subject VARCHAR(255) NULL,
        Template TEXT NULL,
        Active BIT NOT NULL CONSTRAINT DF_EmailContent_Active DEFAULT (1),
        CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_EmailContent_CreatedUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedUtc DATETIME2 NOT NULL CONSTRAINT DF_EmailContent_UpdatedUtc DEFAULT (SYSUTCDATETIME())
    );

    CREATE UNIQUE INDEX UX_EmailContent_Name ON dbo.EmailContent(Name);
END

IF NOT EXISTS (
    SELECT 1
    FROM dbo.EmailContent
)
BEGIN
    INSERT INTO dbo.EmailContent (Name, Subject, Template, Active, CreatedUtc, UpdatedUtc)
    SELECT TOP 1
        'Default Survey Email',
        NULLIF(LTRIM(RTRIM(SurveyEmailSubject)), ''),
        NULLIF(LTRIM(RTRIM(SurveyEmailTemplate)), ''),
        1,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    FROM dbo.Config
    ORDER BY Id;
END
