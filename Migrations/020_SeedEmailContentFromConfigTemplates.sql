-- Migration: Seed first two EmailContent rows from Config templates
-- 1) Survey email template
-- 2) Survey reminder email template

IF OBJECT_ID('dbo.EmailContent', 'U') IS NULL
BEGIN
    RAISERROR('Table dbo.EmailContent was not found. Run 019_CreateEmailContentTable.sql first.', 16, 1);
    RETURN;
END

DECLARE @configId INT;
DECLARE @surveySubject VARCHAR(255);
DECLARE @surveyTemplate TEXT;
DECLARE @reminderSubject VARCHAR(255);
DECLARE @reminderTemplate TEXT;

SELECT TOP (1)
    @configId = c.Id,
    @surveySubject = c.SurveyEmailSubject,
    @surveyTemplate = c.SurveyEmailTemplate,
    @reminderSubject = c.SurveyReminderEmailSubject,
    @reminderTemplate = c.SurveyReminderEmailTemplate
FROM dbo.Config c
ORDER BY c.Id;

IF @configId IS NULL
BEGIN
    RAISERROR('No rows found in dbo.Config. Cannot seed EmailContent.', 16, 1);
    RETURN;
END

IF EXISTS (
    SELECT 1
    FROM dbo.EmailContent ec
    WHERE ec.Name = 'Default Survey Email'
)
BEGIN
    UPDATE dbo.EmailContent
    SET
        Name = 'Survey Email (from Config)',
        Subject = NULLIF(LTRIM(RTRIM(@surveySubject)), ''),
        Template = NULLIF(LTRIM(RTRIM(CAST(@surveyTemplate AS VARCHAR(MAX)))), ''),
        Active = 1,
        UpdatedUtc = SYSUTCDATETIME()
    WHERE Name = 'Default Survey Email';
END
ELSE IF NOT EXISTS (
    SELECT 1
    FROM dbo.EmailContent ec
    WHERE ec.Name = 'Survey Email (from Config)'
)
BEGIN
    INSERT INTO dbo.EmailContent (Name, Subject, Template, Active, CreatedUtc, UpdatedUtc)
    VALUES (
        'Survey Email (from Config)',
        NULLIF(LTRIM(RTRIM(@surveySubject)), ''),
        NULLIF(LTRIM(RTRIM(CAST(@surveyTemplate AS VARCHAR(MAX)))), ''),
        1,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
END

IF NOT EXISTS (
    SELECT 1
    FROM dbo.EmailContent ec
    WHERE ec.Name = 'Survey Reminder Email (from Config)'
)
BEGIN
    INSERT INTO dbo.EmailContent (Name, Subject, Template, Active, CreatedUtc, UpdatedUtc)
    VALUES (
        'Survey Reminder Email (from Config)',
        NULLIF(LTRIM(RTRIM(@reminderSubject)), ''),
        NULLIF(LTRIM(RTRIM(CAST(@reminderTemplate AS VARCHAR(MAX)))), ''),
        1,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
END
