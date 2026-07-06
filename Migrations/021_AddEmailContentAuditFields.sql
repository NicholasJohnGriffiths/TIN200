-- Migration: Add audit fields to EmailContent

IF OBJECT_ID('dbo.EmailContent', 'U') IS NULL
BEGIN
    RAISERROR('Table dbo.EmailContent was not found. Run 019_CreateEmailContentTable.sql first.', 16, 1);
    RETURN;
END

IF COL_LENGTH('dbo.EmailContent', 'CreatedBy') IS NULL
BEGIN
    ALTER TABLE dbo.EmailContent
        ADD CreatedBy VARCHAR(255) NULL;
END

IF COL_LENGTH('dbo.EmailContent', 'UpdatedBy') IS NULL
BEGIN
    ALTER TABLE dbo.EmailContent
        ADD UpdatedBy VARCHAR(255) NULL;
END

IF COL_LENGTH('dbo.EmailContent', 'CreatedBy') IS NOT NULL
   AND COL_LENGTH('dbo.EmailContent', 'UpdatedBy') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
        UPDATE dbo.EmailContent
        SET CreatedBy = ISNULL(CreatedBy, ''Migration''),
            UpdatedBy = ISNULL(UpdatedBy, ''Migration'')
        WHERE CreatedBy IS NULL OR UpdatedBy IS NULL;';
END
