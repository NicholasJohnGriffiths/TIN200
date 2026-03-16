-- Migration: Add cascade delete from Company -> CompanySurvey -> Answer
-- Deleting a Company row will automatically delete its CompanySurvey rows,
-- and deleting a CompanySurvey row will automatically delete its Answer rows.

-- Step 1: Drop existing FK on CompanySurvey.CompanyId (currently RESTRICT)
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_CompanySurvey_TIN200'
      AND parent_object_id = OBJECT_ID('dbo.CompanySurvey')
)
BEGIN
    ALTER TABLE [dbo].[CompanySurvey] DROP CONSTRAINT [FK_CompanySurvey_TIN200];
END

-- Step 2: Re-add FK with CASCADE DELETE
ALTER TABLE [dbo].[CompanySurvey]
    ADD CONSTRAINT [FK_CompanySurvey_TIN200]
    FOREIGN KEY ([CompanyId]) REFERENCES [dbo].[Company]([Id])
    ON DELETE CASCADE;

-- Step 3: Drop existing FK on Answer.CompanySurveyId if present (currently RESTRICT or unnamed)
DECLARE @fkName NVARCHAR(256);
SELECT @fkName = fk.name
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
WHERE fk.parent_object_id = OBJECT_ID('dbo.Answer')
  AND c.name = 'CompanySurveyId';

IF @fkName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE [dbo].[Answer] DROP CONSTRAINT [' + @fkName + ']');
END

-- Step 4: Add FK on Answer.CompanySurveyId with CASCADE DELETE
ALTER TABLE [dbo].[Answer]
    ADD CONSTRAINT [FK_Answer_CompanySurvey]
    FOREIGN KEY ([CompanySurveyId]) REFERENCES [dbo].[CompanySurvey]([Id])
    ON DELETE CASCADE;
