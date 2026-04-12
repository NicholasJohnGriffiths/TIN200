IF COL_LENGTH('dbo.Question', 'DisplayPreviousYear') IS NULL
BEGIN
    ALTER TABLE [dbo].[Question]
    ADD [DisplayPreviousYear] [bit] NULL;
END;

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Question')
      AND name = N'DisplayPreviousYear'
)
BEGIN
    UPDATE [dbo].[Question]
    SET [DisplayPreviousYear] = 0
    WHERE [DisplayPreviousYear] IS NULL;
END;
