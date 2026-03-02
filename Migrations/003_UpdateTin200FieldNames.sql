-- Migration: Normalize TIN200 field names to final schema
-- Created: 2026-03-02

-- After this script completes, run 001_CreatePowerBIViews.sql to ensure
-- all analytics views are recreated against the normalized column names.

SET NOCOUNT ON;

IF OBJECT_ID('dbo.TIN200', 'U') IS NULL
BEGIN
    RAISERROR('Table dbo.TIN200 was not found.', 16, 1);
    RETURN;
END;

IF COL_LENGTH('dbo.TIN200', 'CEOFirstName') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'CEO First Name ') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[CEO First Name ]', 'CEOFirstName', 'COLUMN';
    ELSE IF COL_LENGTH('dbo.TIN200', 'CEO First Name') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[CEO First Name]', 'CEOFirstName', 'COLUMN';
    ELSE IF COL_LENGTH('dbo.TIN200', 'CeoFirstName') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[CeoFirstName]', 'CEOFirstName', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'CEOLastName') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'CEO Last Name ') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[CEO Last Name ]', 'CEOLastName', 'COLUMN';
    ELSE IF COL_LENGTH('dbo.TIN200', 'CEO Last Name') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[CEO Last Name]', 'CEOLastName', 'COLUMN';
    ELSE IF COL_LENGTH('dbo.TIN200', 'CeoLastName') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[CeoLastName]', 'CEOLastName', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'Email') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'Email ') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[Email ]', 'Email', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'ExternalID') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'External ID') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[External ID]', 'ExternalID', 'COLUMN';
    ELSE IF COL_LENGTH('dbo.TIN200', 'ExternalId') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[ExternalId]', 'ExternalID', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'CompanyName') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'Company Name') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[Company Name]', 'CompanyName', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'CompanyDescription') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'Company Description') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[Company Description]', 'CompanyDescription', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'FYE2025') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'FYE 2025') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[FYE 2025]', 'FYE2025', 'COLUMN';
    ELSE IF COL_LENGTH('dbo.TIN200', 'Fye2025') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[Fye2025]', 'FYE2025', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'FYE2024') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'FYE 2024') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[FYE 2024]', 'FYE2024', 'COLUMN';
    ELSE IF COL_LENGTH('dbo.TIN200', 'Fye2024') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[Fye2024]', 'FYE2024', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'FYE2023') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'FYE 2023') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[FYE 2023]', 'FYE2023', 'COLUMN';
    ELSE IF COL_LENGTH('dbo.TIN200', 'Fye2023') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[Fye2023]', 'FYE2023', 'COLUMN';
END;

IF COL_LENGTH('dbo.TIN200', 'FinancialYear') IS NULL
BEGIN
    IF COL_LENGTH('dbo.TIN200', 'Financial Year') IS NOT NULL
        EXEC sp_rename 'dbo.TIN200.[Financial Year]', 'FinancialYear', 'COLUMN';
END;

SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'TIN200'
ORDER BY ORDINAL_POSITION;
